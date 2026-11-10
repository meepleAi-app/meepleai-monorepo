using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// Unit tests for Session domain methods related to player actions.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class PlayerActionDomainTests
{
    private static Session CreateSessionWithParticipants(
        out Guid ownerId, out Guid playerId, out Guid spectatorId)
    {
        var userId = Guid.NewGuid();
        var session = Session.Create(userId, Guid.NewGuid(), SessionType.GameSpecific);

        // The Create method already adds owner as first participant
        var owner = session.Participants.First();
        ownerId = owner.Id;
        owner.Role = ParticipantRole.Host;

        // Add a player
        playerId = Guid.NewGuid();
        var participantsField = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)participantsField!.GetValue(session)!;
        list.Add(new Participant
        {
            Id = playerId,
            SessionId = session.Id,
            DisplayName = "Player",
            IsOwner = false,
            Role = ParticipantRole.Player,
            JoinOrder = 2
        });

        // Add a spectator
        spectatorId = Guid.NewGuid();
        list.Add(new Participant
        {
            Id = spectatorId,
            SessionId = session.Id,
            DisplayName = "Spectator",
            IsOwner = false,
            Role = ParticipantRole.Spectator,
            JoinOrder = 3
        });

        return session;
    }

    // === RemoveParticipant Tests ===

    [Fact]
    public void RemoveParticipant_ValidPlayer_ShouldRemove()
    {
        // Arrange
        var session = CreateSessionWithParticipants(out _, out var playerId, out _);
        var initialCount = session.Participants.Count;

        // Act
        session.RemoveParticipant(playerId);

        // Assert
        session.Participants.Should().HaveCount(initialCount - 1);
        session.Participants.Should().NotContain(p => p.Id == playerId);
    }

    [Fact]
    public void RemoveParticipant_Host_ShouldThrowConflictException()
    {
        // Arrange
        var session = CreateSessionWithParticipants(out var ownerId, out _, out _);

        // Act
        var act = () => session.RemoveParticipant(ownerId);

        // Assert
        act.Should().Throw<ConflictException>()
            .WithMessage("*Cannot remove the session host*");
    }

    [Fact]
    public void RemoveParticipant_NonExistent_ShouldThrowNotFoundException()
    {
        // Arrange
        var session = CreateSessionWithParticipants(out _, out _, out _);

        // Act
        var act = () => session.RemoveParticipant(Guid.NewGuid());

        // Assert
        act.Should().Throw<NotFoundException>()
            .WithMessage("*not found in session*");
    }

    [Fact]
    public void RemoveParticipant_FinalizedSession_ShouldThrowConflictException()
    {
        // Arrange
        var session = CreateSessionWithParticipants(out var ownerId, out var playerId, out var spectatorId);
        session.Finalize();

        // Act
        var act = () => session.RemoveParticipant(playerId);

        // Assert
        act.Should().Throw<ConflictException>()
            .WithMessage("*finalized*");
    }

    // === Entry credentials after a removal (#4883) ===
    // Joining is open to whoever holds the session code or the invite token. A kick that left both
    // untouched let the removed participant walk back in at once.

    [Fact]
    public void RemoveParticipant_RotatesTheSessionCode()
    {
        var session = CreateSessionWithParticipants(out _, out var playerId, out _);
        var oldCode = session.SessionCode;

        session.RemoveParticipant(playerId);

        session.SessionCode.Should().NotBe(oldCode);
        session.SessionCode.Should().MatchRegex("^[A-HJ-NP-Z2-9]{6}$");
    }

    [Fact]
    public void RemoveParticipant_RevokesTheInviteToken()
    {
        var session = CreateSessionWithParticipants(out _, out var playerId, out _);
        var token = session.GenerateInviteToken(24);
        session.IsInviteTokenValid(token).Should().BeTrue("the precondition: the link works before the kick");

        session.RemoveParticipant(playerId);

        session.InviteToken.Should().BeNull();
        session.IsInviteTokenValid(token).Should().BeFalse();
    }

    [Fact]
    public void RemoveParticipant_Refused_LeavesCodeAndInviteUntouched()
    {
        // A refused removal (the host cannot be kicked) must not burn the code for everybody else.
        var session = CreateSessionWithParticipants(out var ownerId, out _, out _);
        var oldCode = session.SessionCode;
        var token = session.GenerateInviteToken(24);

        var act = () => session.RemoveParticipant(ownerId);

        act.Should().Throw<ConflictException>();
        session.SessionCode.Should().Be(oldCode);
        session.IsInviteTokenValid(token).Should().BeTrue();
    }

    [Fact]
    public void RotateSessionCode_NeverReturnsTheCodeItReplaces()
    {
        var session = CreateSessionWithParticipants(out _, out _, out _);

        for (var i = 0; i < 200; i++)
        {
            var before = session.SessionCode;
            session.RotateSessionCode();
            session.SessionCode.Should().NotBe(before);
        }
    }

    [Fact]
    public void IsAccessibleBy_OwnerAndRegisteredParticipant_True_StrangerAndEmptyId_False()
    {
        var ownerUserId = Guid.NewGuid();
        var playerUserId = Guid.NewGuid();
        var session = Session.Create(ownerUserId, Guid.NewGuid(), SessionType.GameSpecific);
        session.AddParticipant(ParticipantInfo.Create("Player", isOwner: false, joinOrder: 2), playerUserId);

        session.IsAccessibleBy(ownerUserId).Should().BeTrue();
        session.IsAccessibleBy(playerUserId).Should().BeTrue();
        session.IsAccessibleBy(Guid.NewGuid()).Should().BeFalse();
        session.IsAccessibleBy(Guid.Empty).Should().BeFalse("an empty id is not an identity");
    }

    // === MarkParticipantReady Tests ===

    [Fact]
    public void MarkParticipantReady_ActiveSession_ShouldSetReady()
    {
        // Arrange
        var session = CreateSessionWithParticipants(out _, out var playerId, out _);

        // Act
        session.MarkParticipantReady(playerId);

        // Assert
        var player = session.Participants.First(p => p.Id == playerId);
        player.IsReady.Should().BeTrue();
    }

    [Fact]
    public void MarkParticipantReady_NonExistentParticipant_ShouldThrowNotFoundException()
    {
        // Arrange
        var session = CreateSessionWithParticipants(out _, out _, out _);

        // Act
        var act = () => session.MarkParticipantReady(Guid.NewGuid());

        // Assert
        act.Should().Throw<NotFoundException>()
            .WithMessage("*not found in session*");
    }

    [Fact]
    public void MarkParticipantReady_FinalizedSession_ShouldThrowConflictException()
    {
        // Arrange
        var session = CreateSessionWithParticipants(out var ownerId, out var playerId, out var spectatorId);
        session.Finalize();

        // Act
        var act = () => session.MarkParticipantReady(playerId);

        // Assert
        act.Should().Throw<ConflictException>();
    }

    // === ResetAllReady Tests ===

    [Fact]
    public void ResetAllReady_ShouldClearAllReadyFlags()
    {
        // Arrange
        var session = CreateSessionWithParticipants(out var ownerId, out var playerId, out _);
        session.MarkParticipantReady(ownerId);
        session.MarkParticipantReady(playerId);

        // Verify precondition
        session.Participants.Where(p => p.Id == ownerId || p.Id == playerId)
            .Should().AllSatisfy(p => p.IsReady.Should().BeTrue());

        // Act
        session.ResetAllReady();

        // Assert
        session.Participants.Should().AllSatisfy(p => p.IsReady.Should().BeFalse());
    }

    // === ParticipantRole Tests ===

    [Fact]
    public void ParticipantRole_Hierarchy_ShouldBeCorrect()
    {
        // Assert hierarchy: Host > Player > Spectator
        ((int)ParticipantRole.Host).Should().BeGreaterThan((int)ParticipantRole.Player);
        ((int)ParticipantRole.Player).Should().BeGreaterThan((int)ParticipantRole.Spectator);
    }

    [Theory]
    [InlineData(ParticipantRole.Host, ParticipantRole.Host, true)]
    [InlineData(ParticipantRole.Host, ParticipantRole.Player, true)]
    [InlineData(ParticipantRole.Host, ParticipantRole.Spectator, true)]
    [InlineData(ParticipantRole.Player, ParticipantRole.Player, true)]
    [InlineData(ParticipantRole.Player, ParticipantRole.Host, false)]
    [InlineData(ParticipantRole.Spectator, ParticipantRole.Player, false)]
    [InlineData(ParticipantRole.Spectator, ParticipantRole.Host, false)]
    public void ParticipantRole_ComparisonForAuthorization_ShouldWork(
        ParticipantRole actualRole, ParticipantRole requiredRole, bool shouldPass)
    {
        // Act
        var hasPermission = actualRole >= requiredRole;

        // Assert
        hasPermission.Should().Be(shouldPass);
    }
}
