using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// Unit tests for Session.AssignParticipantRole domain method.
/// Issue #4766 - Session Join via Code + Active Player Roles
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class AssignParticipantRoleDomainTests
{
    private static Session CreateSessionWithParticipants(
        out Guid hostUserId, out Guid hostParticipantId,
        out Guid playerUserId, out Guid playerParticipantId)
    {
        hostUserId = Guid.NewGuid();
        var session = Session.Create(hostUserId, Guid.NewGuid(), SessionType.GameSpecific);

        // The Create method adds owner as first participant
        var owner = session.Participants.First();
        hostParticipantId = owner.Id;
        owner.Role = ParticipantRole.Host;

        // Add a player via reflection
        playerUserId = Guid.NewGuid();
        playerParticipantId = Guid.NewGuid();
        var participantsField = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)participantsField!.GetValue(session)!;
        list.Add(new Participant
        {
            Id = playerParticipantId,
            SessionId = session.Id,
            UserId = playerUserId,
            DisplayName = "Player",
            IsOwner = false,
            Role = ParticipantRole.Player,
            JoinOrder = 2
        });

        return session;
    }

    [Fact]
    public void AssignParticipantRole_ValidPromotion_ChangesRole()
    {
        // Arrange
        var session = CreateSessionWithParticipants(
            out var hostUserId, out _, out _, out var playerParticipantId);

        // Act
        session.AssignParticipantRole(playerParticipantId, ParticipantRole.Host, hostUserId);

        // Assert
        var player = session.Participants.First(p => p.Id == playerParticipantId);
        player.Role.Should().Be(ParticipantRole.Host);
        player.IsOwner.Should().BeTrue();
    }

    [Fact]
    public void AssignParticipantRole_DemoteToSpectator_ChangesRole()
    {
        // Arrange
        var session = CreateSessionWithParticipants(
            out var hostUserId, out _, out _, out var playerParticipantId);

        // Act
        session.AssignParticipantRole(playerParticipantId, ParticipantRole.Spectator, hostUserId);

        // Assert
        var player = session.Participants.First(p => p.Id == playerParticipantId);
        player.Role.Should().Be(ParticipantRole.Spectator);
        player.IsOwner.Should().BeFalse();
    }

    [Fact]
    public void AssignParticipantRole_FinalizedSession_ThrowsConflict()
    {
        // Arrange
        var session = CreateSessionWithParticipants(
            out var hostUserId, out _, out _, out var playerParticipantId);
        session.Finalize();

        // Act & Assert
        var act = () => session.AssignParticipantRole(playerParticipantId, ParticipantRole.Host, hostUserId);
        act.Should().Throw<ConflictException>()
            .WithMessage("*finalized*");
    }

    [Fact]
    public void AssignParticipantRole_NonHostRequester_ThrowsForbidden()
    {
        // Arrange
        var session = CreateSessionWithParticipants(
            out _, out _, out var playerUserId, out var playerParticipantId);

        // Act & Assert - player tries to assign roles
        var act = () => session.AssignParticipantRole(playerParticipantId, ParticipantRole.Host, playerUserId);
        act.Should().Throw<ForbiddenException>()
            .WithMessage("*hosts can assign*");
    }

    [Fact]
    public void AssignParticipantRole_LastHostDemotion_ThrowsConflict()
    {
        // Arrange
        var session = CreateSessionWithParticipants(
            out var hostUserId, out var hostParticipantId, out _, out _);

        // Act & Assert - try to demote the only host
        var act = () => session.AssignParticipantRole(hostParticipantId, ParticipantRole.Player, hostUserId);
        act.Should().Throw<ConflictException>()
            .WithMessage("*last host*");
    }

    [Fact]
    public void AssignParticipantRole_DemoteHostWithMultipleHosts_Succeeds()
    {
        // Arrange
        var session = CreateSessionWithParticipants(
            out var hostUserId, out var hostParticipantId, out _, out var playerParticipantId);

        // Promote player to host first (so there are 2 hosts)
        session.AssignParticipantRole(playerParticipantId, ParticipantRole.Host, hostUserId);

        // Act - now demoting original host should work
        session.AssignParticipantRole(hostParticipantId, ParticipantRole.Player, hostUserId);

        // Assert
        var originalHost = session.Participants.First(p => p.Id == hostParticipantId);
        originalHost.Role.Should().Be(ParticipantRole.Player);
        originalHost.IsOwner.Should().BeFalse();
    }

    [Fact]
    public void AssignParticipantRole_UnknownRequester_ThrowsNotFound()
    {
        // Arrange
        var session = CreateSessionWithParticipants(
            out _, out _, out _, out var playerParticipantId);
        var unknownUserId = Guid.NewGuid();

        // Act & Assert
        var act = () => session.AssignParticipantRole(playerParticipantId, ParticipantRole.Host, unknownUserId);
        act.Should().Throw<NotFoundException>()
            .WithMessage("*Requester*not a participant*");
    }

    [Fact]
    public void AssignParticipantRole_UnknownParticipant_ThrowsNotFound()
    {
        // Arrange
        var session = CreateSessionWithParticipants(
            out var hostUserId, out _, out _, out _);
        var unknownParticipantId = Guid.NewGuid();

        // Act & Assert
        var act = () => session.AssignParticipantRole(unknownParticipantId, ParticipantRole.Host, hostUserId);
        act.Should().Throw<NotFoundException>()
            .WithMessage("*Participant*not found*");
    }
}
