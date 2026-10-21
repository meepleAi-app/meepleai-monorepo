using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
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

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// #4613: gli eventi che <see cref="AgentSessionRepository"/> consegna al collector.
/// </summary>
/// <remarks>
/// <para>
/// <c>KnowledgeBaseMappers.ToDomain</c> ricostruiva la riga con il costruttore pubblico di <c>AgentSession</c>, che
/// solleva <c>AgentSessionCreatedEvent</c>: ogni aggregato letto portava già un «creata», e <c>UpdateAsync</c> lo
/// consegnava al collector insieme all'evento vero (stato, definizione, configurazione, fine).
/// </para>
/// <para>
/// I test di lettura prendono le colonne dal modello EF (<see cref="ModelDrivenRow"/>): una colonna aggiunta domani
/// all'entità senza mapping li fa fallire con il suo nome.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4613")]
public sealed class AgentSessionRepositoryReconstitutionTests : IDisposable
{
    private static readonly Guid ActivePlayer = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid ScoredPlayer = Guid.Parse("99999999-8888-7777-6666-555555555555");

    private static readonly string GameStateJson =
        $"{{\"currentTurn\":3,\"activePlayer\":\"{ActivePlayer}\",\"playerScores\":{{\"{ScoredPlayer}\":12.5}},\"gamePhase\":\"fase\",\"lastAction\":\"mossa\"}}";

    private static readonly string GameStateView = string.Create(CultureInfo.InvariantCulture, $"3/{ActivePlayer}/fase/mossa/{ScoredPlayer}={12.5m}");

    private static readonly IReadOnlyDictionary<string, (Func<AgentSessionEntity, object?> Stored, Func<AgentSession, object?> Rebuilt)> Views =
        new Dictionary<string, (Func<AgentSessionEntity, object?>, Func<AgentSession, object?>)>(StringComparer.Ordinal)
        {
            // L'aggregato porta lo stato come value object, la riga come JSON.
            [nameof(AgentSessionEntity.CurrentGameStateJson)] = (_ => GameStateView, s =>
                $"{s.CurrentGameState.CurrentTurn}/{s.CurrentGameState.ActivePlayer}/{s.CurrentGameState.GamePhase}/{s.CurrentGameState.LastAction}/" +
                string.Join(",", s.CurrentGameState.PlayerScores.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.Key}={p.Value}")))),
        };

    private readonly MeepleAiDbContext _db;

    public AgentSessionRepositoryReconstitutionTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"AgentSessionRepo_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private (AgentSessionRepository Repository, DomainEventCollector Collector) CreateRepository()
    {
        var collector = new DomainEventCollector();
        return (new AgentSessionRepository(_db, collector), collector);
    }

    // IsActive: il costruttore darebbe true, quindi la sentinella e' false; il percorso «attive» filtra su true.
    private Task<AgentSessionEntity> SeedEveryColumnAsync(bool active = true) =>
        ModelDrivenRow.SeedEveryColumnAsync<AgentSessionEntity>(_db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(AgentSessionEntity.IsActive)] = active,
            [nameof(AgentSessionEntity.CurrentGameStateJson)] = GameStateJson,
        });

    // Il percorso di lettura e se la sua query trova una riga non attiva.
    public static TheoryData<string, bool> ReadPaths => new()
    {
        { nameof(AgentSessionRepository.GetByIdAsync), false },
        { nameof(AgentSessionRepository.GetByUserIdAsync), false },
        { nameof(AgentSessionRepository.GetActiveByGameSessionAsync), true },
    };

    private static async Task<AgentSession> ReadAsync(AgentSessionRepository repository, string readPath, AgentSessionEntity seeded) => readPath switch
    {
        nameof(AgentSessionRepository.GetByIdAsync) => (await repository.GetByIdAsync(seeded.Id))!,
        nameof(AgentSessionRepository.GetByUserIdAsync) => (await repository.GetByUserIdAsync(seeded.UserId)).Single(),
        nameof(AgentSessionRepository.GetActiveByGameSessionAsync) => (await repository.GetActiveByGameSessionAsync(seeded.GameSessionId)).Single(),
        _ => throw new ArgumentOutOfRangeException(nameof(readPath), readPath, null),
    };

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReconstitutesEveryColumnOfTheRow(string readPath, bool active)
    {
        var seeded = await SeedEveryColumnAsync(active);

        var session = await ReadAsync(CreateRepository().Repository, readPath, seeded);

        var lost = ModelDrivenRow.LostColumns(_db, seeded, session, Views, columnsToSkip: []);
        string.Join(Environment.NewLine, lost).Should().BeEmpty(
            "every column of the row must come back on the aggregate as it was stored");
    }

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReturnsAnAggregateWithNoPendingEvents(string readPath, bool active)
    {
        var seeded = await SeedEveryColumnAsync(active);

        var session = await ReadAsync(CreateRepository().Repository, readPath, seeded);

        session.DomainEvents.Should().BeEmpty(
            "a row that is read was created long ago: rebuilding it must not raise AgentSessionCreatedEvent, " +
            "or UpdateAsync hands it to the collector next to the event of the real change");
    }

    [Fact]
    public async Task UpdateAsync_AfterUpdateGameState_CollectsOnlyTheStateUpdatedEvent()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        session!.UpdateGameState(GameState.Initial(ActivePlayer));
        await repository.UpdateAsync(session);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<AgentSessionStateUpdatedEvent>();
    }

    [Fact]
    public async Task UpdateAsync_AfterEnd_CollectsOnlyTheEndedEvent()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        session!.End();
        await repository.UpdateAsync(session);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<AgentSessionEndedEvent>();
    }

    [Fact]
    public async Task UpdateAsync_AfterUpdateConfig_CollectsOnlyTheConfigUpdatedEvent()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        session!.UpdateConfig(AgentConfig.Default());
        await repository.UpdateAsync(session);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<AgentSessionConfigUpdatedEvent>();
    }

    [Fact]
    public async Task UpdateAsync_AfterUpdateAgentDefinition_CollectsOnlyTheDefinitionChangedEvent()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        session!.UpdateAgentDefinition(Guid.NewGuid());
        await repository.UpdateAsync(session);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<AgentSessionDefinitionChangedEvent>();
    }

    [Fact]
    public async Task UpdateAsync_WithNoChange_CollectsNothing()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        await repository.UpdateAsync(session!);

        collector.PeekEvents().Should().BeEmpty("nothing happened to the agent session, so there is nothing to record");
    }

    [Fact]
    public async Task UpdateAsync_AfterEnd_PersistsTheEndAndLeavesTheOtherColumnsAsTheyWere()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, _) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        session!.End();
        await repository.UpdateAsync(session);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var after = await _db.Set<AgentSessionEntity>().AsNoTracking().SingleAsync(s => s.Id == seeded.Id);
        var changedColumns = ModelDrivenRow.Columns<AgentSessionEntity>(_db)
            .Where(c => !Equals(Normalized(c.Name, c.PropertyInfo!.GetValue(seeded)), Normalized(c.Name, c.PropertyInfo.GetValue(after))))
            .Select(c => c.Name)
            .Order(StringComparer.Ordinal);
        changedColumns.Should().Equal(nameof(AgentSessionEntity.EndedAt), nameof(AgentSessionEntity.IsActive));
        after.IsActive.Should().BeFalse();
        after.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_AfterUpdateAgentDefinition_PersistsTheNewDefinition()
    {
        // Non e' una colonna del ricostruire ma del salvare: UpdateAsync scrive a mano tre colonne, e
        // AgentDefinitionId non era fra queste. UpdateAgentSessionDefinitionCommand cambiava l'aggregato, emetteva
        // l'evento e non cambiava la riga.
        var seeded = await SeedEveryColumnAsync();
        var (repository, _) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);
        var newDefinition = Guid.NewGuid();

        session!.UpdateAgentDefinition(newDefinition);
        await repository.UpdateAsync(session);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var after = await _db.Set<AgentSessionEntity>().AsNoTracking().SingleAsync(s => s.Id == seeded.Id);
        after.AgentDefinitionId.Should().Be(newDefinition);
    }

    // UpdateAsync riscrive lo stato dal value object, quindi con la formattazione del serializzatore: si confronta il contenuto.
    private static object? Normalized(string column, object? value) =>
        string.Equals(column, nameof(AgentSessionEntity.CurrentGameStateJson), StringComparison.Ordinal)
            ? GameState.FromJson((string)value!).ToJson()
            : value;

    [Fact]
    public async Task AddAsync_NewSession_CollectsTheCreatedEvent()
    {
        var (repository, collector) = CreateRepository();
        var session = new AgentSession(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), GameState.Initial(ActivePlayer));

        await repository.AddAsync(session);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<AgentSessionCreatedEvent>();
    }
}
