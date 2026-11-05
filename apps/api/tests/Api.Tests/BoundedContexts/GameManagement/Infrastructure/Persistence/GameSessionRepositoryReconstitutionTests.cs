using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// #4613: gli eventi che <see cref="GameSessionRepository"/> consegna al collector.
/// </summary>
/// <remarks>
/// <para>
/// <c>MapToDomain</c> ricostruiva la riga con il costruttore pubblico di <c>GameSession</c>, che solleva
/// <c>GameSessionCreatedEvent</c>: ogni aggregato letto portava già un «creata», e <c>UpdateAsync</c> lo consegnava
/// al collector insieme all'evento vero (avvio, pausa, ripresa, fine, abbandono, nuovo giocatore).
/// </para>
/// <para>
/// I test di lettura prendono le colonne dal modello EF (<see cref="ModelDrivenRow"/>): una colonna aggiunta domani
/// all'entità senza mapping li fa fallire con il suo nome.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4613")]
public sealed class GameSessionRepositoryReconstitutionTests : IDisposable
{
    private const string PlayersJson =
        "[{\"PlayerName\":\"Alice\",\"PlayerOrder\":3,\"Color\":\"Red\"},{\"PlayerName\":\"Bruno\",\"PlayerOrder\":5,\"Color\":null}]";

    private const string PlayersView = "Alice/3/Red|Bruno/5/";

    private static readonly IReadOnlyDictionary<string, (Func<GameSessionEntity, object?> Stored, Func<GameSession, object?> Rebuilt)> Views =
        new Dictionary<string, (Func<GameSessionEntity, object?>, Func<GameSession, object?>)>(StringComparer.Ordinal)
        {
            // L'aggregato porta lo stato come value object, la riga come stringa.
            [nameof(GameSessionEntity.Status)] = (e => e.Status, s => s.Status.Value),

            // L'aggregato porta i giocatori come lista, la riga come JSON.
            [nameof(GameSessionEntity.PlayersJson)] = (_ => PlayersView, s => string.Join("|", s.Players.Select(p => $"{p.PlayerName}/{p.PlayerOrder}/{p.Color}"))),
        };

    private readonly MeepleAiDbContext _db;

    public GameSessionRepositoryReconstitutionTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"GameSessionRepo_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private (GameSessionRepository Repository, DomainEventCollector Collector) CreateRepository()
    {
        var collector = new DomainEventCollector();
        return (new GameSessionRepository(_db, collector), collector);
    }

    // Stato diverso da Setup, che è quello che il costruttore darebbe; "Paused" rientra in tutti i filtri «attive».
    private Task<GameSessionEntity> SeedEveryColumnAsync(string status = "Paused") =>
        ModelDrivenRow.SeedEveryColumnAsync<GameSessionEntity>(_db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(GameSessionEntity.Status)] = status,
            [nameof(GameSessionEntity.PlayersJson)] = PlayersJson,
        });

    // Il percorso di lettura e lo stato con cui la sua query trova la riga.
    public static TheoryData<string, string> ReadPaths => new()
    {
        { nameof(GameSessionRepository.GetByIdAsync), "Paused" },
        { nameof(GameSessionRepository.GetAllAsync), "Paused" },
        { nameof(GameSessionRepository.FindActiveByGameIdAsync), "Paused" },
        { nameof(GameSessionRepository.FindByGameIdAsync), "Paused" },
        { nameof(GameSessionRepository.FindActiveAsync), "Paused" },
        { nameof(GameSessionRepository.FindOldestActiveByUserIdAsync), "Paused" },
        { nameof(GameSessionRepository.FindHistoryAsync), "Abandoned" },
    };

    // Manca FindByPlayerNameAsync: usa EF.Functions.ILike, che il provider InMemory non traduce.
    private static async Task<GameSession> ReadAsync(GameSessionRepository repository, string readPath, GameSessionEntity seeded) => readPath switch
    {
        nameof(GameSessionRepository.GetByIdAsync) => (await repository.GetByIdAsync(seeded.Id))!,
        nameof(GameSessionRepository.GetAllAsync) => (await repository.GetAllAsync()).Single(),
        nameof(GameSessionRepository.FindActiveByGameIdAsync) => (await repository.FindActiveByGameIdAsync(seeded.GameId)).Single(),
        nameof(GameSessionRepository.FindByGameIdAsync) => (await repository.FindByGameIdAsync(seeded.GameId)).Single(),
        nameof(GameSessionRepository.FindActiveAsync) => (await repository.FindActiveAsync(seeded.CreatedByUserId!.Value)).Single(),
        nameof(GameSessionRepository.FindOldestActiveByUserIdAsync) => (await repository.FindOldestActiveByUserIdAsync(seeded.CreatedByUserId!.Value, limit: 5)).Single(),
        nameof(GameSessionRepository.FindHistoryAsync) => (await repository.FindHistoryAsync()).Single(),
        _ => throw new ArgumentOutOfRangeException(nameof(readPath), readPath, null),
    };

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReconstitutesEveryColumnOfTheRow(string readPath, string status)
    {
        var seeded = await SeedEveryColumnAsync(status);

        var session = await ReadAsync(CreateRepository().Repository, readPath, seeded);

        var lost = ModelDrivenRow.LostColumns(_db, seeded, session, Views, columnsToSkip: []);
        string.Join(Environment.NewLine, lost).Should().BeEmpty(
            "every column of the row must come back on the aggregate as it was stored");
    }

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReturnsAnAggregateWithNoPendingEvents(string readPath, string status)
    {
        var seeded = await SeedEveryColumnAsync(status);

        var session = await ReadAsync(CreateRepository().Repository, readPath, seeded);

        session.DomainEvents.Should().BeEmpty(
            "a row that is read was created long ago: rebuilding it must not raise GameSessionCreatedEvent, " +
            "or UpdateAsync hands it to the collector next to the event of the real change");
    }

    [Fact]
    public async Task UpdateAsync_AfterResume_CollectsOnlyTheResumedEvent()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        session!.Resume();
        await repository.UpdateAsync(session);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<GameSessionResumedEvent>();
    }

    [Fact]
    public async Task UpdateAsync_AfterAddPlayer_CollectsOnlyThePlayerAddedEvent()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        session!.AddPlayer(new SessionPlayer("Carla", 7));
        await repository.UpdateAsync(session);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<PlayerAddedToSessionEvent>();
    }

    [Fact]
    public async Task UpdateAsync_AfterComplete_CollectsOnlyTheCompletedEvent()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        session!.Complete("Alice");
        await repository.UpdateAsync(session);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<GameSessionCompletedEvent>();
    }

    [Fact]
    public async Task UpdateAsync_AfterMarkCorrelatedComplete_CollectsNothing()
    {
        // La sessione «ombra» della saga LiveSession → GameSession si chiude con MarkCorrelatedComplete proprio per non
        // sollevare eventi (vedi il commento del metodo): il «creata» che portava la riga letta vanificava la scelta.
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        session!.MarkCorrelatedComplete(TimeProvider.System);
        await repository.UpdateAsync(session);

        collector.PeekEvents().Should().BeEmpty("the bookkeeping close raises no event, and the rebuilt session must not add one");
    }

    [Fact]
    public async Task UpdateAsync_WithNoChange_CollectsNothing()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        await repository.UpdateAsync(session!);

        collector.PeekEvents().Should().BeEmpty("nothing happened to the session, so there is nothing to record");
    }

    [Fact]
    public async Task UpdateAsync_WithNoChange_LeavesEveryColumnAsItWas()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, _) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        await repository.UpdateAsync(session!);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var after = await _db.GameSessions.AsNoTracking().SingleAsync(s => s.Id == seeded.Id);
        var changed = ModelDrivenRow.Columns<GameSessionEntity>(_db)
            .Select(c => (Column: c.Name, Before: c.PropertyInfo!.GetValue(seeded), After: c.PropertyInfo.GetValue(after)))
            .Where(x => x.Column != nameof(GameSessionEntity.PlayersJson) && !Equals(x.Before, x.After))
            .Select(x => $"{x.Column}: «{x.Before}» → «{x.After}»");
        string.Join(Environment.NewLine, changed).Should().BeEmpty("a session read and saved unchanged must not lose a column");
    }

    [Fact]
    public async Task AddAsync_NewSession_CollectsTheCreatedEvent()
    {
        var (repository, collector) = CreateRepository();
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), [new SessionPlayer("Alice", 1)], Guid.NewGuid());

        await repository.AddAsync(session);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<GameSessionCreatedEvent>();
    }
}
