using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Unit tests for <see cref="LiveGameSession.IsAuthorizedParticipant"/> — the single source of
/// truth for live-session participant authorization (#2573). Mirrors the canonical predicate
/// (creator OR active linked player) and the IsActive / guest semantics from #2561.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class LiveGameSessionAuthorizationTests
{
    private static LiveGameSession NewSession(Guid creatorId) =>
        LiveGameSession.Create(Guid.NewGuid(), creatorId, "Test Game");

    [Fact]
    public void IsAuthorizedParticipant_Creator_ReturnsTrue()
    {
        var creator = Guid.NewGuid();
        var session = NewSession(creator);

        session.IsAuthorizedParticipant(creator).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorizedParticipant_ActiveLinkedPlayer_ReturnsTrue()
    {
        var creator = Guid.NewGuid();
        var playerUserId = Guid.NewGuid();
        var session = NewSession(creator);
        session.AddPlayer(playerUserId, "Alice", PlayerColor.Red);

        session.IsAuthorizedParticipant(playerUserId).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorizedParticipant_RemovedPlayer_ReturnsFalse()
    {
        var creator = Guid.NewGuid();
        var playerUserId = Guid.NewGuid();
        var session = NewSession(creator);
        var player = session.AddPlayer(playerUserId, "Alice", PlayerColor.Red);

        session.RemovePlayer(player.Id);

        session.IsAuthorizedParticipant(playerUserId).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorizedParticipant_GuestPlayerPresent_DoesNotAuthorizeArbitraryUser()
    {
        var creator = Guid.NewGuid();
        var session = NewSession(creator);
        // Guest player has a null UserId; its presence must never authorize an arbitrary caller.
        session.AddPlayer(null, "Guest", PlayerColor.Blue);

        session.IsAuthorizedParticipant(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorizedParticipant_Stranger_ReturnsFalse()
    {
        var session = NewSession(Guid.NewGuid());

        session.IsAuthorizedParticipant(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorizedParticipant_EmptyUserId_ReturnsFalse()
    {
        var session = NewSession(Guid.NewGuid());

        session.IsAuthorizedParticipant(Guid.Empty).Should().BeFalse();
    }
}
