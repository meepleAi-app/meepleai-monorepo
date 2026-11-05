using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// #4744, at the level of the two commands that decide from what they read: the guest's answer and the organizer's
/// withdrawal. A competing write is injected between the handler's read and its save, on a second connection, so the
/// save is refused for real by PostgreSQL's <c>xmin</c>. The unit tests of the handlers pin what they decide on the
/// second look; these prove that the second look happens on a real <c>DbContext</c> (the refused entity is forgotten,
/// the row is read again, the right thing is written) and that nothing the other writer did is lost.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4744")]
public sealed class GameNightInvitationHandlerConcurrencyTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly SharedTestcontainersFixture _fixture;
    private readonly List<MeepleAiDbContext> _contexts = [];
    private string _databaseName = null!;
    private string _connectionString = null!;
    private Guid _organizerId;
    private Guid _nightId;

    public GameNightInvitationHandlerConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"invitation_handler_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _organizerId = Guid.NewGuid();
        _nightId = Guid.NewGuid();

        var db = NewContext();
        await db.Database.MigrateAsync();
        db.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = _nightId,
            OrganizerId = _organizerId,
            Title = "Friday Catan",
            ScheduledAt = Now.AddDays(2),
            Location = "Home",
            MaxPlayers = 6,
            GameIdsJson = "[]",
            Status = "Published",
            CreatedAt = Now,
            UpdatedAt = Now
        });
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var context in _contexts)
        {
            await context.DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private MeepleAiDbContext NewContext()
    {
        var context = _fixture.CreateDbContext(_connectionString);
        _contexts.Add(context);
        return context;
    }

    private static GameNightInvitationRepository RepositoryOn(MeepleAiDbContext db)
    {
        var collector = new Mock<IDomainEventCollector>();
        collector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());
        return new GameNightInvitationRepository(db, collector.Object, NullLogger<GameNightInvitationRepository>.Instance);
    }

    private async Task<GameNightInvitation> SeedPendingInvitationAsync()
    {
        var db = NewContext();
        var invitation = GameNightInvitation.Create(_nightId, "guest@example.com", Now.AddDays(7), _organizerId, Now);
        await RepositoryOn(db).AddAsync(invitation, CancellationToken.None);
        await db.SaveChangesAsync();
        return invitation;
    }

    private async Task<GameNightInvitationEntity> RowAsync(Guid id)
    {
        var db = NewContext();
        return await db.GameNightInvitations.AsNoTracking().SingleAsync(i => i.Id == id);
    }

    /// <summary>A write by another request, on its own connection, through the same repository the handlers use.</summary>
    private async Task AnotherRequestAsync(Guid invitationId, Action<GameNightInvitation> change)
    {
        var db = NewContext();
        var repository = RepositoryOn(db);
        var invitation = await repository.GetByIdAsync(invitationId, CancellationToken.None);
        change(invitation!);
        await repository.UpdateAsync(invitation!, CancellationToken.None);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The real repository of the handler's own context, with one addition: the first read is followed by
    /// <paramref name="afterTheFirstRead"/>, i.e. the competing write lands exactly between read and save.
    /// </summary>
    private Mock<IGameNightInvitationRepository> RacingRepository(
        MeepleAiDbContext db, Func<Task> afterTheFirstRead, GameNightInvitationRepository? repository = null)
    {
        var real = repository ?? RepositoryOn(db);
        var raced = false;
        var wrapper = new Mock<IGameNightInvitationRepository>();

        async Task<GameNightInvitation?> ReadThenRace(Task<GameNightInvitation?> read)
        {
            var invitation = await read;
            if (!raced)
            {
                raced = true;
                await afterTheFirstRead();
            }

            return invitation;
        }

        wrapper
            .Setup(r => r.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string token, CancellationToken ct) => ReadThenRace(real.GetByTokenAsync(token, ct)));
        wrapper
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken ct) => ReadThenRace(real.GetByIdAsync(id, ct)));
        wrapper
            .Setup(r => r.UpdateAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()))
            .Returns((GameNightInvitation invitation, CancellationToken ct) => real.UpdateAsync(invitation, ct));
        return wrapper;
    }

    private (RespondToGameNightInvitationByTokenCommandHandler Handler, Mock<IDomainEventCollector> Events) RespondHandler(
        Func<Task> afterTheFirstRead)
    {
        var db = NewContext();
        var events = new Mock<IDomainEventCollector>();
        var handler = new RespondToGameNightInvitationByTokenCommandHandler(
            RacingRepository(db, afterTheFirstRead).Object,
            new Mock<IGameNightEventRepository>().Object,
            new EfCoreUnitOfWork(db),
            events.Object,
            new FakeTimeProvider(Now.AddHours(1)));
        return (handler, events);
    }

    private (CancelGameNightInvitationCommandHandler Handler, Mock<IDomainEventCollector> Events) WithdrawHandler(
        Func<Task> afterTheFirstRead)
    {
        var db = NewContext();
        var night = new Mock<IGameNightEventRepository>();
        night
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameNightEvent.Create(_organizerId, "Friday Catan", Now.AddDays(2)));
        var events = new Mock<IDomainEventCollector>();
        var handler = new CancelGameNightInvitationCommandHandler(
            RacingRepository(db, afterTheFirstRead).Object,
            night.Object,
            new EfCoreUnitOfWork(db),
            events.Object,
            new Mock<HybridCache>().Object,
            new FakeTimeProvider(Now.AddHours(1)),
            NullLogger<CancelGameNightInvitationCommandHandler>.Instance);
        return (handler, events);
    }

    private static RespondToGameNightInvitationByTokenCommand Accept(GameNightInvitation invitation) =>
        new(invitation.Token, GameNightInvitationStatus.Accepted, ResponderUserId: null);

    private Task Withdraw(CancelGameNightInvitationCommandHandler handler, GameNightInvitation invitation) =>
        handler.Handle(
            new CancelGameNightInvitationCommand(_nightId, invitation.Id, _organizerId),
            CancellationToken.None);

    [Fact(DisplayName = "Inviti: la risposta arriva mentre l'organizzatrice ritira, la risposta trova l'invito ritirato (410) e il ritiro resta")]
    public async Task Respond_WhenTheWithdrawalLandsBetweenReadAndSave_IsGone_AndTheWithdrawalStands()
    {
        var seeded = await SeedPendingInvitationAsync();
        var (handler, events) = RespondHandler(
            () => AnotherRequestAsync(seeded.Id, i => i.Cancel(Now.AddMinutes(30))));

        var act = () => handler.Handle(Accept(seeded), CancellationToken.None);

        await act.Should().ThrowAsync<GoneException>();
        (await RowAsync(seeded.Id)).Status.Should().Be(nameof(GameNightInvitationStatus.Cancelled),
            "prima del token di concorrenza la risposta riscriveva la riga intera e rimetteva in gioco un invito ritirato");
        events.Verify(e => e.Clear(), Times.Once);
    }

    [Fact(DisplayName = "Inviti: la risposta arriva mentre un marcatore sposta la riga, la risposta passa al secondo tentativo senza perdere il marcatore")]
    public async Task Respond_WhenAMarkLandsBetweenReadAndSave_IsRetried_AndKeepsTheMark()
    {
        var seeded = await SeedPendingInvitationAsync();
        var (handler, events) = RespondHandler(async () =>
            await RepositoryOn(NewContext()).MarkReminder24hSentAsync(seeded.Id, Now.AddMinutes(10), CancellationToken.None));

        await handler.Handle(Accept(seeded), CancellationToken.None);

        var row = await RowAsync(seeded.Id);
        row.Status.Should().Be(nameof(GameNightInvitationStatus.Accepted), "il secondo tentativo scrive la risposta");
        row.Reminder24hSentAt.Should().Be(Now.AddMinutes(10),
            "il secondo tentativo parte da una lettura nuova: il marcatore scritto nel frattempo c'è ancora");
        events.Verify(e => e.Clear(), Times.Once);
    }

    [Fact(DisplayName = "Inviti: il ritiro arriva mentre l'ospite rifiuta, il ritiro trova un invito concluso (409) e il «no» resta")]
    public async Task Withdraw_WhenTheGuestDeclinesBetweenReadAndSave_IsAConflict_AndTheDeclineStands()
    {
        var seeded = await SeedPendingInvitationAsync();
        var (handler, _) = WithdrawHandler(
            () => AnotherRequestAsync(seeded.Id, i => i.Decline(null, Now.AddMinutes(30), "Marco")));

        var act = () => Withdraw(handler, seeded);

        await act.Should().ThrowAsync<ConflictException>();
        (await RowAsync(seeded.Id)).Status.Should().Be(nameof(GameNightInvitationStatus.Declined),
            "prima del token di concorrenza il ritiro riscriveva la riga e trasformava il «no» dell'ospite in «ritirato»");
    }

    [Fact(DisplayName = "Inviti: il ritiro arriva mentre l'ospite accetta, il ritiro passa al secondo tentativo sulla riga nuova")]
    public async Task Withdraw_WhenTheGuestAcceptsBetweenReadAndSave_IsRetried_AndWithdrawsTheAcceptedInvitation()
    {
        var seeded = await SeedPendingInvitationAsync();
        var (handler, events) = WithdrawHandler(
            () => AnotherRequestAsync(seeded.Id, i => i.Accept(null, Now.AddMinutes(30), "Marco")));

        await Withdraw(handler, seeded);

        var row = await RowAsync(seeded.Id);
        row.Status.Should().Be(nameof(GameNightInvitationStatus.Cancelled));
        row.RespondedByName.Should().Be("Marco", "la risposta dell'ospite non è stata cancellata, è stata ritirata dopo");
        events.Verify(e => e.Clear(), Times.Once);
    }

    /// <summary>
    /// A context like production's: a real event collector and the outbox mode of staging and production. (The
    /// fixture's own contexts have a collector with no events, so they never write a log or an outbox row.)
    /// </summary>
    private (MeepleAiDbContext Db, DomainEventCollector Collector) OutboxOnlyContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_connectionString, o => o.UseVector())
            .Options;
        var collector = new DomainEventCollector();
        var db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            collector,
            domainEventOutboxOptions: Options.Create(new DomainEventOutboxOptions { Mode = DomainEventDispatchMode.OutboxOnly }));
        _contexts.Add(db);
        return (db, collector);
    }

    [Fact(DisplayName = "Inviti: una risposta ritentata mette in coda il suo evento una volta sola")]
    public async Task Respond_WhenTheFirstSaveIsRefused_QueuesItsEventOnce_NotOncePerAttempt()
    {
        // The refused attempt had already added its log and outbox rows to the change tracker (they are added before
        // the save). Left there, the retry inserted them next to the second attempt's: the same answer, published
        // twice by the outbox processor.
        var seeded = await SeedPendingInvitationAsync();
        var (db, collector) = OutboxOnlyContext();
        var repository = new GameNightInvitationRepository(db, collector, NullLogger<GameNightInvitationRepository>.Instance);
        var handler = new RespondToGameNightInvitationByTokenCommandHandler(
            RacingRepository(
                db,
                // A mark: it moves the row version but does not change what the answer decides.
                async () => await RepositoryOn(NewContext()).MarkReminder24hSentAsync(seeded.Id, Now.AddMinutes(10), CancellationToken.None),
                repository).Object,
            new Mock<IGameNightEventRepository>().Object,
            new EfCoreUnitOfWork(db),
            collector,
            new FakeTimeProvider(Now.AddHours(1)));

        await handler.Handle(Accept(seeded), CancellationToken.None);

        var verify = NewContext();
        (await verify.GameNightInvitations.AsNoTracking().SingleAsync(i => i.Id == seeded.Id))
            .Status.Should().Be(nameof(GameNightInvitationStatus.Accepted));
        (await verify.DomainEventOutbox.AsNoTracking().CountAsync(o => o.EventType.Contains("InvitationResponded")))
            .Should().Be(1, "one answer, one queued event: the refused attempt's rows must not survive into the retry");
        collector.PeekEvents().Should().BeEmpty("the successful save drained the collector");
    }
}

