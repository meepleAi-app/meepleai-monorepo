using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
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
/// #4613: gli eventi che <see cref="ChatSessionRepository"/> consegna al collector.
/// </summary>
/// <remarks>
/// <para>
/// <c>CreateSessionFromEntity</c> ricostruiva la riga con il costruttore pubblico di <c>ChatSession</c>, che solleva
/// <c>ChatSessionCreatedEvent</c>: ogni aggregato letto portava già un «creata», e <c>UpdateAsync</c> lo consegnava
/// al collector insieme all'evento vero (messaggio aggiunto, archiviazione). Quell'evento è nel registro degli
/// alias (<c>chat.session.created</c>): finisce anche in <c>domain_event_logs</c>, da cui legge il feed attività.
/// </para>
/// <para>
/// I test di lettura prendono le colonne dal modello EF (<see cref="ModelDrivenRow"/>): una colonna aggiunta domani
/// all'entità senza mapping li fa fallire con il suo nome.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4613")]
public sealed class ChatSessionRepositoryReconstitutionTests : IDisposable
{
    private static readonly Guid MessageId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static readonly DateTime MessageTimestamp = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static readonly string MessagesJson =
        $"[{{\"Id\":\"{MessageId}\",\"Content\":\"ciao\",\"Role\":\"user\",\"Timestamp\":\"2026-01-02T03:04:05Z\",\"SequenceNumber\":0}}]";

    private static readonly string MessagesView = $"{MessageId}/ciao/user/0/{MessageTimestamp:O}";

    private static readonly IReadOnlyDictionary<string, (Func<ChatSessionEntity, object?> Stored, Func<ChatSession, object?> Rebuilt)> Views =
        new Dictionary<string, (Func<ChatSessionEntity, object?>, Func<ChatSession, object?>)>(StringComparer.Ordinal)
        {
            // L'aggregato porta i messaggi come lista, la riga come JSON.
            [nameof(ChatSessionEntity.MessagesJson)] = (_ => MessagesView, s => string.Join("|", s.Messages.Select(m => $"{m.Id}/{m.Content}/{m.Role}/{m.SequenceNumber}/{m.Timestamp:O}"))),
        };

    private readonly MeepleAiDbContext _db;

    public ChatSessionRepositoryReconstitutionTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ChatSessionRepo_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private (ChatSessionRepository Repository, DomainEventCollector Collector) CreateRepository()
    {
        var collector = new DomainEventCollector();
        return (new ChatSessionRepository(_db, collector), collector);
    }

    // IsArchived: il costruttore darebbe false, quindi la sentinella è true; i percorsi «elenco» filtrano su false.
    private Task<ChatSessionEntity> SeedEveryColumnAsync(bool archived = false) =>
        ModelDrivenRow.SeedEveryColumnAsync<ChatSessionEntity>(_db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ChatSessionEntity.IsArchived)] = archived,
            [nameof(ChatSessionEntity.MessagesJson)] = MessagesJson,
        });

    // Percorso di lettura, se la sua query trova una riga archiviata, e se ricostruisce i messaggi: gli elenchi no,
    // per scelta («Maps to domain without loading full messages»).
    public static TheoryData<string, bool, bool> ReadPaths => new()
    {
        { nameof(ChatSessionRepository.GetByIdAsync), true, true },
        { nameof(ChatSessionRepository.GetByIdWithPaginatedMessagesAsync), true, true },
        { nameof(ChatSessionRepository.GetByUserAndGameAsync), false, false },
        { nameof(ChatSessionRepository.GetByUserIdAsync), false, false },
        { nameof(ChatSessionRepository.GetRecentByUserIdAsync), false, false },
    };

    private static async Task<ChatSession> ReadAsync(ChatSessionRepository repository, string readPath, ChatSessionEntity seeded) => readPath switch
    {
        nameof(ChatSessionRepository.GetByIdAsync) => (await repository.GetByIdAsync(seeded.Id))!,
        nameof(ChatSessionRepository.GetByIdWithPaginatedMessagesAsync) => (await repository.GetByIdWithPaginatedMessagesAsync(seeded.Id))!,
        nameof(ChatSessionRepository.GetByUserAndGameAsync) => (await repository.GetByUserAndGameAsync(seeded.UserId, seeded.GameId)).Single(),
        nameof(ChatSessionRepository.GetByUserIdAsync) => (await repository.GetByUserIdAsync(seeded.UserId)).Single(),
        nameof(ChatSessionRepository.GetRecentByUserIdAsync) => (await repository.GetRecentByUserIdAsync(seeded.UserId)).Single(),
        _ => throw new ArgumentOutOfRangeException(nameof(readPath), readPath, null),
    };

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReconstitutesEveryColumnOfTheRow(string readPath, bool archived, bool carriesMessages)
    {
        var seeded = await SeedEveryColumnAsync(archived);

        var session = await ReadAsync(CreateRepository().Repository, readPath, seeded);

        var skipped = carriesMessages ? Array.Empty<string>() : [nameof(ChatSessionEntity.MessagesJson)];
        var lost = ModelDrivenRow.LostColumns(_db, seeded, session, Views, skipped);
        string.Join(Environment.NewLine, lost).Should().BeEmpty(
            "every column of the row must come back on the aggregate as it was stored");
    }

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReturnsAnAggregateWithNoPendingEvents(string readPath, bool archived, bool carriesMessages)
    {
        _ = carriesMessages;
        var seeded = await SeedEveryColumnAsync(archived);

        var session = await ReadAsync(CreateRepository().Repository, readPath, seeded);

        session.DomainEvents.Should().BeEmpty(
            "a row that is read was created long ago: rebuilding it must not raise ChatSessionCreatedEvent, " +
            "or UpdateAsync hands it to the collector next to the event of the real change");
    }

    [Fact]
    public async Task UpdateAsync_AfterAddMessage_CollectsOnlyTheMessageAddedEvent()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        session!.AddUserMessage("un'altra domanda");
        await repository.UpdateAsync(session);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<ChatSessionMessageAddedEvent>();
    }

    [Fact]
    public async Task UpdateAsync_AfterArchive_CollectsOnlyTheArchivedEvent()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        session!.Archive();
        await repository.UpdateAsync(session);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<ChatSessionArchivedEvent>();
    }

    [Fact]
    public async Task UpdateAsync_AfterRename_CollectsNothing()
    {
        // SetTitle raises no event: renaming a chat is not something the domain announces.
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        session!.SetTitle("titolo nuovo");
        await repository.UpdateAsync(session);

        collector.PeekEvents().Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WithNoChange_CollectsNothing()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        await repository.UpdateAsync(session!);

        collector.PeekEvents().Should().BeEmpty("nothing happened to the chat, so there is nothing to record");
    }

    [Fact]
    public async Task UpdateAsync_AfterAddMessage_KeepsTheStoredMessageAndAddsTheNewOne()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, _) = CreateRepository();
        var session = await repository.GetByIdAsync(seeded.Id);

        session!.AddUserMessage("un'altra domanda");
        await repository.UpdateAsync(session);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var reloaded = await repository.GetByIdAsync(seeded.Id);
        reloaded!.Messages.Select(m => m.Content).Should().Equal("ciao", "un'altra domanda");
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

        var after = await _db.Set<ChatSessionEntity>().AsNoTracking().SingleAsync(s => s.Id == seeded.Id);
        var changed = ModelDrivenRow.Columns<ChatSessionEntity>(_db)
            .Select(c => (Column: c.Name, Before: c.PropertyInfo!.GetValue(seeded), After: c.PropertyInfo.GetValue(after)))
            .Where(x => x.Column != nameof(ChatSessionEntity.MessagesJson) && !Equals(x.Before, x.After))
            .Select(x => $"{x.Column}: «{x.Before}» → «{x.After}»");
        string.Join(Environment.NewLine, changed).Should().BeEmpty("a chat read and saved unchanged must not lose a column");
    }

    [Fact]
    public async Task AddAsync_NewSession_CollectsTheCreatedEvent()
    {
        var (repository, collector) = CreateRepository();
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "titolo");

        await repository.AddAsync(session);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<ChatSessionCreatedEvent>();
    }
}
