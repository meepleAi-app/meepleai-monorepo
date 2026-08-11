using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.Routing;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Routing.SessionTracking;

/// <summary>
/// #2920: the POST /game-sessions endpoint binds a request DTO that excludes the internal-only
/// orchestration flags, so a client cannot forge them. These tests pin that the DTO→command
/// mapping leaves the flags at their safe defaults while forwarding the legitimate fields.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class CreateSessionRequestTests
{
    [Fact]
    public void ToCommand_LeavesInternalFlagsAtSafeDefaults()
    {
        var request = new SessionCommandEndpoints.CreateSessionRequest(
            GameId: Guid.NewGuid(),
            SessionType: "Standard",
            SessionDate: null,
            Location: null,
            Participants: new List<ParticipantDto>());

        var command = request.ToCommand(Guid.NewGuid());

        // The internal-only flags are NOT part of the request DTO, so they are structurally
        // unforgeable and must always land on their safe defaults.
        command.SkipKbReadinessGate.Should().BeFalse();
        command.SkipGameNightEnvelope.Should().BeFalse();
        command.GamebookCampaignId.Should().BeNull();
    }

    [Fact]
    public void ToCommand_PropagatesLegitimateFields()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var nightId = Guid.NewGuid();
        var participants = new List<ParticipantDto> { new() { DisplayName = "Alice", IsOwner = true } };

        var request = new SessionCommandEndpoints.CreateSessionRequest(
            GameId: gameId,
            SessionType: "GameSpecific",
            SessionDate: null,
            Location: "Home",
            Participants: participants,
            GameNightEventId: nightId,
            GuestNames: new[] { "Bob" });

        var command = request.ToCommand(userId);

        command.UserId.Should().Be(userId);
        command.GameId.Should().Be(gameId);
        command.SessionType.Should().Be("GameSpecific");
        command.Location.Should().Be("Home");
        command.Participants.Should().BeEquivalentTo(participants);
        command.GameNightEventId.Should().Be(nightId);
        command.GuestNames.Should().NotBeNull().And.Contain("Bob");
    }
}
