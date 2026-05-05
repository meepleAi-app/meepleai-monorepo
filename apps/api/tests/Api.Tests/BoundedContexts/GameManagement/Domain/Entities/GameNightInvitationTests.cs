using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Tests for the GameNightInvitation aggregate root.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightInvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OneWeekFromNow = Now.AddDays(7);

    private static GameNightInvitation NewPending(
        DateTimeOffset? utcNow = null,
        DateTimeOffset? expiresAt = null)
    {
        return GameNightInvitation.Create(
            gameNightId: Guid.NewGuid(),
            email: "guest@example.com",
            expiresAt: expiresAt ?? OneWeekFromNow,
            createdBy: Guid.NewGuid(),
            utcNow: utcNow ?? Now);
    }

    #region Factory — Create

    [Fact]
    public void Create_WithValidArguments_ReturnsPendingInvitation()
    {
        var gameNightId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();

        var invitation = GameNightInvitation.Create(
            gameNightId: gameNightId,
            email: "Guest@Example.com",
            expiresAt: OneWeekFromNow,
            createdBy: organizerId,
            utcNow: Now);

        invitation.Id.Should().NotBe(Guid.Empty);
        invitation.GameNightId.Should().Be(gameNightId);
        invitation.Email.Should().Be("guest@example.com", "email is normalized to lowercase + trimmed");
        invitation.Status.Should().Be(GameNightInvitationStatus.Pending);
        invitation.ExpiresAt.Should().Be(OneWeekFromNow);
        invitation.RespondedAt.Should().BeNull();
        invitation.RespondedByUserId.Should().BeNull();
        invitation.CreatedAt.Should().Be(Now);
        invitation.CreatedBy.Should().Be(organizerId);
        invitation.Token.Should().HaveLength(22);
        invitation.Token.Should().MatchRegex("^[0-9A-Za-z]{22}$");
    }

    [Fact]
    public void Create_RaisesGameNightInvitationCreatedEvent()
    {
        var invitation = NewPending();

        invitation.DomainEvents.Should().ContainSingle(e => e is GameNightInvitationCreatedEvent);
        var evt = invitation.DomainEvents.OfType<GameNightInvitationCreatedEvent>().Single();
        evt.GameNightInvitationId.Should().Be(invitation.Id);
        evt.GameNightId.Should().Be(invitation.GameNightId);
        evt.Email.Should().Be(invitation.Email);
        evt.Token.Should().Be(invitation.Token);
        evt.ExpiresAt.Should().Be(invitation.ExpiresAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithNullOrWhitespaceEmail_Throws(string? email)
    {
        var action = () => GameNightInvitation.Create(
            gameNightId: Guid.NewGuid(),
            email: email!,
            expiresAt: OneWeekFromNow,
            createdBy: Guid.NewGuid(),
            utcNow: Now);

        action.Should().Throw<ArgumentException>().WithMessage("*Email*");
    }

    [Fact]
    public void Create_WithEmptyGameNightId_Throws()
    {
        var action = () => GameNightInvitation.Create(
            gameNightId: Guid.Empty,
            email: "guest@example.com",
            expiresAt: OneWeekFromNow,
            createdBy: Guid.NewGuid(),
            utcNow: Now);

        action.Should().Throw<ArgumentException>().WithMessage("*GameNightId*");
    }

    [Fact]
    public void Create_WithEmptyCreatedBy_Throws()
    {
        var action = () => GameNightInvitation.Create(
            gameNightId: Guid.NewGuid(),
            email: "guest@example.com",
            expiresAt: OneWeekFromNow,
            createdBy: Guid.Empty,
            utcNow: Now);

        action.Should().Throw<ArgumentException>().WithMessage("*CreatedBy*");
    }

    [Fact]
    public void Create_WithExpiresAtInThePast_Throws()
    {
        var action = () => GameNightInvitation.Create(
            gameNightId: Guid.NewGuid(),
            email: "guest@example.com",
            expiresAt: Now.AddMinutes(-1),
            createdBy: Guid.NewGuid(),
            utcNow: Now);

        action.Should().Throw<ArgumentException>().WithMessage("*future*");
    }

    [Fact]
    public void Create_WithExpiresAtEqualToNow_Throws()
    {
        var action = () => GameNightInvitation.Create(
            gameNightId: Guid.NewGuid(),
            email: "guest@example.com",
            expiresAt: Now,
            createdBy: Guid.NewGuid(),
            utcNow: Now);

        action.Should().Throw<ArgumentException>().WithMessage("*future*");
    }

    #endregion

    #region Accept

    [Fact]
    public void Accept_OnPending_TransitionsToAccepted_ReturnsTrue()
    {
        var invitation = NewPending();
        var userId = Guid.NewGuid();
        var respondedAt = Now.AddHours(1);

        var changed = invitation.Accept(userId, respondedAt);

        changed.Should().BeTrue();
        invitation.Status.Should().Be(GameNightInvitationStatus.Accepted);
        invitation.RespondedAt.Should().Be(respondedAt);
        invitation.RespondedByUserId.Should().Be(userId);
    }

    [Fact]
    public void Accept_OnPending_RaisesGameNightInvitationRespondedEvent()
    {
        var invitation = NewPending();
        var userId = Guid.NewGuid();

        invitation.Accept(userId, Now.AddHours(1));

        var evt = invitation.DomainEvents.OfType<GameNightInvitationRespondedEvent>().SingleOrDefault();
        evt.Should().NotBeNull();
        evt!.GameNightInvitationId.Should().Be(invitation.Id);
        evt.GameNightId.Should().Be(invitation.GameNightId);
        evt.Token.Should().Be(invitation.Token);
        evt.Status.Should().Be(GameNightInvitationStatus.Accepted);
        evt.RespondedByUserId.Should().Be(userId);
    }

    [Fact]
    public void Accept_OnPending_WithoutUserId_AllowsAnonymousResponse()
    {
        var invitation = NewPending();

        var changed = invitation.Accept(userId: null, utcNow: Now.AddHours(1));

        changed.Should().BeTrue();
        invitation.Status.Should().Be(GameNightInvitationStatus.Accepted);
        invitation.RespondedByUserId.Should().BeNull();
    }

    [Fact]
    public void Accept_OnAccepted_NoOp_ReturnsFalse()
    {
        var invitation = NewPending();
        invitation.Accept(Guid.NewGuid(), Now.AddHours(1));
        invitation.ClearDomainEvents();

        var changed = invitation.Accept(Guid.NewGuid(), Now.AddHours(2));

        changed.Should().BeFalse("D2 b idempotency: same response is a no-op");
        invitation.Status.Should().Be(GameNightInvitationStatus.Accepted);
        invitation.DomainEvents.Should().BeEmpty("no event raised on idempotent no-op");
    }

    [Fact]
    public void Accept_OnDeclined_Throws()
    {
        var invitation = NewPending();
        invitation.Decline(Guid.NewGuid(), Now.AddHours(1));

        var action = () => invitation.Accept(Guid.NewGuid(), Now.AddHours(2));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*already*Declined*");
    }

    [Fact]
    public void Accept_OnExpired_Throws()
    {
        var invitation = NewPending(expiresAt: Now.AddMinutes(5));

        var action = () => invitation.Accept(Guid.NewGuid(), Now.AddHours(1));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public void Accept_OnCancelled_Throws()
    {
        var invitation = NewPending();
        invitation.Cancel(Now.AddHours(1));

        var action = () => invitation.Accept(Guid.NewGuid(), Now.AddHours(2));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cancelled*");
    }

    #endregion

    #region Decline

    [Fact]
    public void Decline_OnPending_TransitionsToDeclined_ReturnsTrue()
    {
        var invitation = NewPending();
        var respondedAt = Now.AddHours(1);

        var changed = invitation.Decline(Guid.NewGuid(), respondedAt);

        changed.Should().BeTrue();
        invitation.Status.Should().Be(GameNightInvitationStatus.Declined);
        invitation.RespondedAt.Should().Be(respondedAt);
    }

    [Fact]
    public void Decline_OnDeclined_NoOp_ReturnsFalse()
    {
        var invitation = NewPending();
        invitation.Decline(Guid.NewGuid(), Now.AddHours(1));
        invitation.ClearDomainEvents();

        var changed = invitation.Decline(Guid.NewGuid(), Now.AddHours(2));

        changed.Should().BeFalse();
        invitation.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Decline_OnAccepted_Throws()
    {
        var invitation = NewPending();
        invitation.Accept(Guid.NewGuid(), Now.AddHours(1));

        var action = () => invitation.Decline(Guid.NewGuid(), Now.AddHours(2));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*already*Accepted*");
    }

    #endregion

    #region Cancel

    [Fact]
    public void Cancel_FromPending_TransitionsToCancelled()
    {
        var invitation = NewPending();
        var cancelledAt = Now.AddHours(1);

        invitation.Cancel(cancelledAt);

        invitation.Status.Should().Be(GameNightInvitationStatus.Cancelled);
        invitation.RespondedAt.Should().Be(cancelledAt);
    }

    [Fact]
    public void Cancel_FromAccepted_TransitionsToCancelled()
    {
        // Organizer revokes an accepted invite (or game night is cancelled).
        var invitation = NewPending();
        invitation.Accept(Guid.NewGuid(), Now.AddHours(1));

        invitation.Cancel(Now.AddHours(2));

        invitation.Status.Should().Be(GameNightInvitationStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromCancelled_IsIdempotent()
    {
        var invitation = NewPending();
        invitation.Cancel(Now.AddHours(1));

        var action = () => invitation.Cancel(Now.AddHours(2));

        action.Should().NotThrow();
        invitation.Status.Should().Be(GameNightInvitationStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromDeclined_Throws()
    {
        var invitation = NewPending();
        invitation.Decline(Guid.NewGuid(), Now.AddHours(1));

        var action = () => invitation.Cancel(Now.AddHours(2));

        action.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region IsExpired

    [Fact]
    public void IsExpired_WhenPendingAndPastExpiresAt_ReturnsTrue()
    {
        var invitation = NewPending(expiresAt: Now.AddMinutes(5));

        invitation.IsExpired(Now.AddMinutes(10)).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenPendingAndAtExpiresAt_ReturnsTrue()
    {
        // Boundary: utcNow == ExpiresAt is treated as expired (>=).
        var expiresAt = Now.AddMinutes(5);
        var invitation = NewPending(expiresAt: expiresAt);

        invitation.IsExpired(expiresAt).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenPendingAndBeforeExpiresAt_ReturnsFalse()
    {
        var invitation = NewPending(expiresAt: Now.AddDays(7));

        invitation.IsExpired(Now.AddHours(1)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenAccepted_ReturnsFalse_RegardlessOfTime()
    {
        // Terminal state: expiry is irrelevant.
        var invitation = NewPending(expiresAt: Now.AddMinutes(5));
        invitation.Accept(Guid.NewGuid(), Now.AddMinutes(1));

        invitation.IsExpired(Now.AddDays(30)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenDeclined_ReturnsFalse()
    {
        var invitation = NewPending(expiresAt: Now.AddMinutes(5));
        invitation.Decline(Guid.NewGuid(), Now.AddMinutes(1));

        invitation.IsExpired(Now.AddDays(30)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenCancelled_ReturnsFalse()
    {
        var invitation = NewPending(expiresAt: Now.AddMinutes(5));
        invitation.Cancel(Now.AddMinutes(1));

        invitation.IsExpired(Now.AddDays(30)).Should().BeFalse();
    }

    #endregion

    #region Reconstitute

    [Fact]
    public void Reconstitute_RestoresState_WithoutRaisingEvents()
    {
        var id = Guid.NewGuid();
        var token = "ABCDEFGHIJKLMNOPQRSTUV";
        var gameNightId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var respondedBy = Guid.NewGuid();

        var invitation = GameNightInvitation.Reconstitute(
            id: id,
            token: token,
            gameNightId: gameNightId,
            email: "guest@example.com",
            status: GameNightInvitationStatus.Accepted,
            expiresAt: OneWeekFromNow,
            respondedAt: Now.AddHours(1),
            respondedByUserId: respondedBy,
            createdAt: Now,
            createdBy: createdBy);

        invitation.Id.Should().Be(id);
        invitation.Token.Should().Be(token);
        invitation.Status.Should().Be(GameNightInvitationStatus.Accepted);
        invitation.RespondedByUserId.Should().Be(respondedBy);
        invitation.DomainEvents.Should().BeEmpty("reconstitution must not raise events");
    }

    #endregion
}
