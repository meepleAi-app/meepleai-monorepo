using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// #4947 on a real PostgreSQL: the seat decision is a read-then-write, and two answers for the last seat must not
/// both read it free. The unit tests pin what the handlers do with the lock; these prove the lock is a lock. The
/// second guest's answer is launched from inside the first one's seat count, on its own connection, exactly between
/// the first one's read and its write: with the lock it waits for the first to commit and then finds the table
/// full; without it, it would finish within the wait and the night would sit one more than its seats.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4947")]
public sealed class GameNightSeatLockPostgresTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>How long the first answer waits for the second before committing: long enough for an unlocked second
    /// answer to finish on a local database, short enough not to stretch the suite.</summary>
    private static readonly TimeSpan HoldWhileTheOtherTries = TimeSpan.FromMilliseconds(1500);

    private readonly SharedTestcontainersFixture _fixture;
    private readonly List<MeepleAiDbContext> _contexts = [];
    private string _databaseName = null!;
    private string _connectionString = null!;
    private Guid _organizerId;
    private Guid _nightId;

    public GameNightSeatLockPostgresTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"seat_lock_{Guid.NewGuid():N}";
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
            MaxPlayers = 2,
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

    private static IDomainEventCollector QuietCollector()
    {
        var collector = new Mock<IDomainEventCollector>();
        collector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());
        return collector.Object;
    }

    private static GameNightInvitationRepository InvitationsOn(MeepleAiDbContext db) =>
        new(db, QuietCollector(), NullLogger<GameNightInvitationRepository>.Instance);

    private static GameNightEventRepository NightsOn(MeepleAiDbContext db) =>
        new(db, QuietCollector(), NullLogger<GameNightEventRepository>.Instance);

    private async Task<GameNightInvitation> SeedInvitationAsync(string email, bool accepted)
    {
        var db = NewContext();
        var invitation = GameNightInvitation.Create(_nightId, email, Now.AddDays(7), _organizerId, Now);
        if (accepted)
        {
            invitation.Accept(null, Now.AddMinutes(1));
        }

        await InvitationsOn(db).AddAsync(invitation, CancellationToken.None);
        await db.SaveChangesAsync();
        return invitation;
    }

    private async Task<int> AcceptedRowsAsync()
    {
        var db = NewContext();
        return await db.GameNightInvitations.AsNoTracking()
            .CountAsync(i => i.GameNightId == _nightId && i.Status == nameof(GameNightInvitationStatus.Accepted));
    }

    private RespondToGameNightInvitationByTokenCommandHandler PlainHandler(MeepleAiDbContext db) =>
        new(InvitationsOn(db), NightsOn(db), new EfCoreUnitOfWork(db), QuietCollector(), new FakeTimeProvider(Now.AddHours(1)));

    /// <summary>
    /// The real invitation repository of the handler's context, with one addition: the first seat count is followed
    /// by <paramref name="afterTheFirstCount"/>, i.e. the competing answer lands between this answer's read and write.
    /// </summary>
    private static IGameNightInvitationRepository CountingThenRacing(MeepleAiDbContext db, Func<Task> afterTheFirstCount)
    {
        var real = InvitationsOn(db);
        var raced = false;
        var wrapper = new Mock<IGameNightInvitationRepository>();
        wrapper
            .Setup(r => r.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string token, CancellationToken ct) => real.GetByTokenAsync(token, ct));
        wrapper
            .Setup(r => r.CountAcceptedByGameNightIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid nightId, CancellationToken ct) =>
            {
                var count = await real.CountAcceptedByGameNightIdAsync(nightId, ct);
                if (!raced)
                {
                    raced = true;
                    await afterTheFirstCount();
                }

                return count;
            });
        wrapper
            .Setup(r => r.UpdateAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()))
            .Returns((GameNightInvitation invitation, CancellationToken ct) => real.UpdateAsync(invitation, ct));
        return wrapper.Object;
    }

    private static RespondToGameNightInvitationByTokenCommand Accept(GameNightInvitation invitation) =>
        new(invitation.Token, GameNightInvitationStatus.Accepted, ResponderUserId: null);

    [Fact(DisplayName = "Posti: due ospiti accettano l'ultimo posto insieme, uno entra e l'altro trova il tavolo pieno (409 game_night_full)")]
    public async Task TwoGuestsAcceptTheLastSeatTogether_OneSits_TheOtherFindsTheTableFull()
    {
        // Two seats, one already taken by a guest: one seat left and two pending guests who both want it.
        await SeedInvitationAsync("seated@example.com", accepted: true);
        var first = await SeedInvitationAsync("first@example.com", accepted: false);
        var second = await SeedInvitationAsync("second@example.com", accepted: false);

        Task? secondAnswer = null;
        var secondWasStillWaitingWhenTheFirstCommitted = false;
        var firstDb = NewContext();
        var firstHandler = new RespondToGameNightInvitationByTokenCommandHandler(
            CountingThenRacing(firstDb, async () =>
            {
                // The second guest answers now, on their own connection, while the first holds the lock and has not
                // written yet. Not awaited to completion: it cannot finish before the first commits.
                secondAnswer = PlainHandler(NewContext()).Handle(Accept(second), CancellationToken.None);
                var finished = await Task.WhenAny(secondAnswer, Task.Delay(HoldWhileTheOtherTries));
                secondWasStillWaitingWhenTheFirstCommitted = finished != secondAnswer;
            }),
            NightsOn(firstDb),
            new EfCoreUnitOfWork(firstDb),
            QuietCollector(),
            new FakeTimeProvider(Now.AddHours(1)));

        await firstHandler.Handle(Accept(first), CancellationToken.None);

        secondAnswer.Should().NotBeNull("the second answer was launched from inside the first one's seat count");
        var act = () => secondAnswer!;
        var refused = await act.Should().ThrowAsync<ConflictException>();
        refused.Which.ErrorCode.Should().Be(GameNightSeats.FullCode);

        secondWasStillWaitingWhenTheFirstCommitted.Should().BeTrue(
            "the lock made the second answer wait for the first to commit; without it the second finishes within the wait and sits too");
        (await AcceptedRowsAsync()).Should().Be(2, "two seats, two accepted guests, whatever the order");

        var rows = await NewContext().GameNightInvitations.AsNoTracking()
            .Where(i => i.GameNightId == _nightId).ToDictionaryAsync(i => i.Email, i => i.Status);
        rows["first@example.com"].Should().Be(nameof(GameNightInvitationStatus.Accepted));
        rows["second@example.com"].Should().Be(nameof(GameNightInvitationStatus.Pending), "refused, not written");
    }

    [Fact(DisplayName = "Posti: il lucchetto fuori da una transazione è rifiutato, non rilasciato in silenzio")]
    public async Task LockSeats_OutsideATransaction_Refuses()
    {
        var db = NewContext();

        var act = () => NightsOn(db).LockSeatsAsync(_nightId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ExecuteInTransactionAsync*");
    }

    [Fact(DisplayName = "Posti: il lucchetto dentro la transazione della unit of work si prende e si rilascia con essa")]
    public async Task LockSeats_InsideTheUnitOfWorkTransaction_IsTakenAndReleasedWithIt()
    {
        var first = NewContext();
        var second = NewContext();
        var unitOfWork = new EfCoreUnitOfWork(first);
        var secondCouldTakeItWhileTheFirstHeldIt = false;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await NightsOn(first).LockSeatsAsync(_nightId, ct);

            var secondTry = new EfCoreUnitOfWork(second).ExecuteInTransactionAsync(
                innerCt => NightsOn(second).LockSeatsAsync(_nightId, innerCt), ct);
            var finished = await Task.WhenAny(secondTry, Task.Delay(HoldWhileTheOtherTries, ct));
            secondCouldTakeItWhileTheFirstHeldIt = finished == secondTry;
            _ = secondTry.ContinueWith(_ => { }, TaskScheduler.Default); // observed below
        }, CancellationToken.None);

        secondCouldTakeItWhileTheFirstHeldIt.Should().BeFalse("the lock is held until the first transaction commits");

        // Released with the commit: a third taker is not kept waiting.
        var third = NewContext();
        var thirdTry = new EfCoreUnitOfWork(third).ExecuteInTransactionAsync(
            ct => NightsOn(third).LockSeatsAsync(_nightId, ct), CancellationToken.None);
        var thirdFinished = await Task.WhenAny(thirdTry, Task.Delay(HoldWhileTheOtherTries));
        thirdFinished.Should().Be(thirdTry, "nothing holds the lock any more");
    }
}
