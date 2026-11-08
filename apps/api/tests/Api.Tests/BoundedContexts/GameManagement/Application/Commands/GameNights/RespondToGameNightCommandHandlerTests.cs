using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// #4921 — a guest who accepted an email invitation while logged in has no RSVP row, and until now
/// answering from the app was a 404: the email link was their only way. The answer goes to the
/// invitation they answered, with its rules; the seat check counts members and guests together.
/// #4947 — the seat check runs under the night's seat lock, inside the transaction that writes the
/// answer, and the night is read again under it.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4921")]
public sealed class RespondToGameNightCommandHandlerTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameNightEventRepository> _nights = new();
    private readonly Mock<IGameNightInvitationRepository> _invitations = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDomainEventCollector> _events = new();
    private readonly RespondToGameNightCommandHandler _sut;

    public RespondToGameNightCommandHandlerTests()
    {
        // #3636: the handler writes through ExecuteInTransactionAsync; the mock runs the delegate as the real one would.
        _unitOfWork.SetupExecuteInTransaction();
        _sut = new RespondToGameNightCommandHandler(
            _nights.Object, _invitations.Object, _unitOfWork.Object, _events.Object, new FakeTimeProvider(UtcNow));
    }

    private GameNightEvent NightWith(int? maxPlayers, params Guid[] members)
    {
        var night = GameNightEvent.Create(
            Guid.NewGuid(), "Serata", UtcNow.AddDays(1), gameIds: [Guid.NewGuid()], maxPlayers: maxPlayers);
        night.Publish(members.ToList());
        _nights.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);
        return night;
    }

    private GameNightInvitation GuestInvitationOf(GameNightEvent night, Guid guest, GameNightInvitationStatus status)
    {
        var invitation = GameNightInvitation.Reconstitute(
            id: Guid.NewGuid(),
            token: "tokentokentokentokent1",
            gameNightId: night.Id,
            email: "guest@example.com",
            status: status,
            expiresAt: UtcNow.AddDays(7),
            respondedAt: UtcNow.AddDays(-1),
            respondedByUserId: guest,
            createdAt: UtcNow.AddDays(-2),
            createdBy: night.OrganizerId);
        _invitations.Setup(r => r.GetAnsweredByUserAsync(night.Id, guest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        return invitation;
    }

    private void AcceptedGuests(GameNightEvent night, int count) =>
        _invitations.Setup(r => r.CountAcceptedByGameNightIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(count);

    private Task Respond(GameNightEvent night, Guid user, RsvpStatus response) =>
        _sut.Handle(new RespondToGameNightCommand(night.Id, user, response), TestContext.Current.CancellationToken);

    private void VerifyTransactions(Times times) =>
        _unitOfWork.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), times);

    private void VerifySeatLock(Times times) =>
        _nights.Verify(r => r.LockSeatsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), times);

    // ── the email guest ───────────────────────────────────────────────────────

    [Fact]
    public async Task EmailGuest_CanDeclineFromTheApp_AndTheInvitationTakesTheAnswer()
    {
        var guest = Guid.NewGuid();
        var night = NightWith(maxPlayers: null);
        var invitation = GuestInvitationOf(night, guest, GameNightInvitationStatus.Accepted);

        await Respond(night, guest, RsvpStatus.Declined);

        invitation.Status.Should().Be(GameNightInvitationStatus.Declined);
        _invitations.Verify(r => r.UpdateAsync(invitation, It.IsAny<CancellationToken>()), Times.Once);
        VerifyTransactions(Times.Once());
        _nights.Verify(r => r.UpdateAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()), Times.Never,
            "nothing is written twice: the invitation stays the guest's one record");
    }

    [Fact]
    public async Task EmailGuest_RepeatingTheirAnswer_IsANoOp()
    {
        var guest = Guid.NewGuid();
        var night = NightWith(maxPlayers: 4);
        GuestInvitationOf(night, guest, GameNightInvitationStatus.Accepted);

        await Respond(night, guest, RsvpStatus.Accepted);

        _invitations.Verify(r => r.UpdateAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
        _invitations.Verify(r => r.CountAcceptedByGameNightIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "their own seat is already counted: no seat check for an answer that does not change");
        VerifySeatLock(Times.Never());
    }

    [Fact]
    public async Task EmailGuest_CanChangeTheirMind_WhenASeatIsFree()
    {
        // The email link refuses a switch (a public credential replayed must not flip an answer); the
        // account that answered is not a replay, and changes its mind as a member would.
        var guest = Guid.NewGuid();
        var night = NightWith(maxPlayers: 4);
        var invitation = GuestInvitationOf(night, guest, GameNightInvitationStatus.Declined);
        AcceptedGuests(night, 2);

        await Respond(night, guest, RsvpStatus.Accepted);

        invitation.Status.Should().Be(GameNightInvitationStatus.Accepted);
        _invitations.Verify(r => r.UpdateAsync(invitation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmailGuest_ChangingTheirMindBackToAccepted_IsRefusedWhenTheTableIsFull()
    {
        var guest = Guid.NewGuid();
        var night = NightWith(maxPlayers: 2);
        GuestInvitationOf(night, guest, GameNightInvitationStatus.Declined);
        AcceptedGuests(night, 2);

        var act = () => Respond(night, guest, RsvpStatus.Accepted);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be(GameNightSeats.FullCode);
        _invitations.Verify(r => r.UpdateAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmailGuest_TakingASeat_LocksTheSeatsBeforeCounting_AndReadsTheNightAgainUnderTheLock()
    {
        // #4947: the copy read before the lock may be short of a seat by the time the lock is held.
        var guest = Guid.NewGuid();
        var night = NightWith(maxPlayers: 4);
        GuestInvitationOf(night, guest, GameNightInvitationStatus.Declined);
        AcceptedGuests(night, 0);
        var order = new List<string>();
        _nights.Setup(r => r.LockSeatsAsync(night.Id, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("lock")).Returns(Task.CompletedTask);
        _nights.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("read")).ReturnsAsync(night);
        _invitations.Setup(r => r.CountAcceptedByGameNightIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("count")).ReturnsAsync(0);

        await Respond(night, guest, RsvpStatus.Accepted);

        // Two reads before the lock: the one that finds no RSVP row, and the attempt's own (#4744: each attempt
        // reads the night again). Then the lock, the read under it, and the count on that copy.
        order.Should().Equal("read", "read", "lock", "read", "count");
    }

    [Fact]
    public async Task EmailGuest_TheSeatCheckDecidesOnTheNightReadUnderTheLock_NotOnTheFirstCopy()
    {
        // First read: two of four seats, a seat is free. Under the lock: the two seats left went while we waited.
        var guest = Guid.NewGuid();
        var stale = NightWith(maxPlayers: 4);
        var fresh = GameNightEvent.Create(stale.OrganizerId, "Serata", UtcNow.AddDays(1), gameIds: [Guid.NewGuid()], maxPlayers: 4);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        fresh.Publish([a, b]);
        fresh.GetRsvp(a)!.Accept();
        fresh.GetRsvp(b)!.Accept();
        // The two reads before the lock see the stale copy; the one under the lock sees the fresh one.
        _nights.SetupSequence(r => r.GetByIdAsync(stale.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stale)
            .ReturnsAsync(stale)
            .ReturnsAsync(fresh);
        GuestInvitationOf(stale, guest, GameNightInvitationStatus.Declined);
        AcceptedGuests(stale, 2);

        var act = () => Respond(stale, guest, RsvpStatus.Accepted);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be(GameNightSeats.FullCode);
    }

    [Fact]
    public async Task EmailGuest_Declining_NeverTakesTheSeatLock()
    {
        var guest = Guid.NewGuid();
        var night = NightWith(maxPlayers: 2);
        GuestInvitationOf(night, guest, GameNightInvitationStatus.Accepted);

        await Respond(night, guest, RsvpStatus.Declined);

        VerifySeatLock(Times.Never());
        _invitations.Verify(r => r.CountAcceptedByGameNightIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmailGuest_Maybe_IsA409WithItsCode_BecauseTheInvitationDoesNotRecordIt()
    {
        var guest = Guid.NewGuid();
        var night = NightWith(maxPlayers: null);
        GuestInvitationOf(night, guest, GameNightInvitationStatus.Accepted);

        var act = () => Respond(night, guest, RsvpStatus.Maybe);

        (await act.Should().ThrowAsync<ConflictException>())
            .Which.ErrorCode.Should().Be(RespondToGameNightCommandHandler.MaybeNotAvailableCode);
    }

    [Theory]
    [InlineData(GameNightInvitationStatus.Cancelled)]
    [InlineData(GameNightInvitationStatus.Expired)]
    public async Task EmailGuest_WithdrawnOrExpiredInvitation_Is410_AsFromTheEmailLink(GameNightInvitationStatus terminal)
    {
        var guest = Guid.NewGuid();
        var night = NightWith(maxPlayers: null);
        GuestInvitationOf(night, guest, terminal);

        var act = () => Respond(night, guest, RsvpStatus.Declined);

        await act.Should().ThrowAsync<GoneException>();
    }

    [Fact]
    public async Task EmailGuest_OnACancelledNight_Is410_AsFromTheEmailLink()
    {
        var guest = Guid.NewGuid();
        var night = NightWith(maxPlayers: null);
        night.Cancel();
        GuestInvitationOf(night, guest, GameNightInvitationStatus.Accepted);

        var act = () => Respond(night, guest, RsvpStatus.Declined);

        await act.Should().ThrowAsync<GoneException>();
    }

    [Fact]
    public async Task Stranger_StillGetsA404()
    {
        var night = NightWith(maxPlayers: null);
        _invitations.Setup(r => r.GetAnsweredByUserAsync(night.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightInvitation?)null);

        var act = () => Respond(night, Guid.NewGuid(), RsvpStatus.Accepted);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── the seats, counted together ───────────────────────────────────────────

    [Fact]
    public async Task Member_CannotAccept_WhenTheGuestsFillTheLastSeats()
    {
        // Four seats: one accepted member and three accepted guests. The aggregate alone said «one of four».
        var accepted = Guid.NewGuid();
        var late = Guid.NewGuid();
        var night = NightWith(maxPlayers: 4, accepted, late);
        night.GetRsvp(accepted)!.Accept();
        AcceptedGuests(night, 3);

        var act = () => Respond(night, late, RsvpStatus.Accepted);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be(GameNightSeats.FullCode);
        _nights.Verify(r => r.UpdateAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Member_CanAccept_WhenASeatIsLeftAfterTheGuests()
    {
        var late = Guid.NewGuid();
        var night = NightWith(maxPlayers: 4, late);
        AcceptedGuests(night, 3);

        await Respond(night, late, RsvpStatus.Accepted);

        night.GetRsvp(late)!.Status.Should().Be(RsvpStatus.Accepted);
        _nights.Verify(r => r.UpdateAsync(night, It.IsAny<CancellationToken>()), Times.Once);
        VerifyTransactions(Times.Once());
        VerifySeatLock(Times.Once());
    }

    [Fact]
    public async Task Member_TakingASeat_WritesTheNightReadUnderTheLock_WithTheirRowOnIt()
    {
        // The night written is the one read under the lock (its row version is the current one), and the member's
        // answer lands on that copy's row, not on the stale one.
        var late = Guid.NewGuid();
        var stale = NightWith(maxPlayers: 4, late);
        var fresh = GameNightEvent.Create(stale.OrganizerId, "Serata", UtcNow.AddDays(1), gameIds: [Guid.NewGuid()], maxPlayers: 4);
        fresh.Publish([late]);
        _nights.SetupSequence(r => r.GetByIdAsync(stale.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stale)
            .ReturnsAsync(fresh);
        AcceptedGuests(stale, 0);

        await Respond(stale, late, RsvpStatus.Accepted);

        fresh.GetRsvp(late)!.Status.Should().Be(RsvpStatus.Accepted);
        stale.GetRsvp(late)!.Status.Should().Be(RsvpStatus.Pending, "the stale copy is left alone");
        _nights.Verify(r => r.UpdateAsync(fresh, It.IsAny<CancellationToken>()), Times.Once);
        _nights.Verify(r => r.UpdateAsync(stale, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Member_WhoseTwinRequestTookTheLastSeatMeanwhile_IsNotBouncedByTheirOwnSeat()
    {
        // Read before the lock: Pending, one seat left. Under the lock: their own twin request already accepted and
        // the table is full. Their seat is one of the counted ones: a no-op, not a refusal.
        var member = Guid.NewGuid();
        var stale = NightWith(maxPlayers: 2, member);
        var fresh = GameNightEvent.Create(stale.OrganizerId, "Serata", UtcNow.AddDays(1), gameIds: [Guid.NewGuid()], maxPlayers: 2);
        fresh.Publish([member]);
        fresh.GetRsvp(member)!.Accept();
        _nights.SetupSequence(r => r.GetByIdAsync(stale.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stale)
            .ReturnsAsync(fresh);
        AcceptedGuests(stale, 1);

        await Respond(stale, member, RsvpStatus.Accepted);

        fresh.GetRsvp(member)!.Status.Should().Be(RsvpStatus.Accepted);
    }

    [Fact]
    public async Task Member_AlreadyAccepted_IsNotRefusedByAFullTable_AndDoesNotLock()
    {
        // Their seat is one of the counted ones; repeating the answer must not bounce them.
        var accepted = Guid.NewGuid();
        var night = NightWith(maxPlayers: 2, accepted);
        night.GetRsvp(accepted)!.Accept();
        AcceptedGuests(night, 1);

        await Respond(night, accepted, RsvpStatus.Accepted);

        night.GetRsvp(accepted)!.Status.Should().Be(RsvpStatus.Accepted);
        VerifySeatLock(Times.Never());
    }

    [Theory]
    [InlineData(RsvpStatus.Declined)]
    [InlineData(RsvpStatus.Maybe)]
    public async Task Member_NotTakingASeat_NeverLocksNorCounts(RsvpStatus answer)
    {
        var member = Guid.NewGuid();
        var night = NightWith(maxPlayers: 2, member);

        await Respond(night, member, answer);

        night.GetRsvp(member)!.Status.Should().Be(answer);
        VerifySeatLock(Times.Never());
        _invitations.Verify(r => r.CountAcceptedByGameNightIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyTransactions(Times.Once());
    }

    [Fact]
    public async Task Member_WithoutASeatCap_NeverAsksAboutTheGuests()
    {
        var member = Guid.NewGuid();
        var night = NightWith(maxPlayers: null, member);

        await Respond(night, member, RsvpStatus.Accepted);

        _invitations.Verify(r => r.CountAcceptedByGameNightIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifySeatLock(Times.Never());
    }

    [Fact]
    public async Task TheHandler_NeverOpensATransactionByHand()
    {
        // #3636: under the retrying execution strategy a user-initiated transaction throws in every environment but
        // Testing. ExecuteInTransactionAsync is the only legal door.
        var member = Guid.NewGuid();
        var night = NightWith(maxPlayers: 4, member);
        AcceptedGuests(night, 0);

        await Respond(night, member, RsvpStatus.Accepted);

        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never,
            "the transaction saves; a second save outside it would be a write with no lock");
    }
}
