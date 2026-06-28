using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class LiveGameSessionCreateTests
{
    [Fact]
    public void Create_WithTrackingSessionId_SetsProperty()
    {
        var trackingId = Guid.NewGuid();
        var session = LiveGameSession.Create(
            id: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            gameName: "Mage Knight",
            timeProvider: TimeProvider.System,
            gameId: Guid.NewGuid(),
            trackingSessionId: trackingId);

        Assert.Equal(trackingId, session.TrackingSessionId);
    }

    [Fact]
    public void Create_WithoutTrackingSessionId_LeavesNull()
    {
        var session = LiveGameSession.Create(
            id: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            gameName: "Free session",
            timeProvider: TimeProvider.System);

        Assert.Null(session.TrackingSessionId);
    }
}
