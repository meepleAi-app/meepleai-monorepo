using Api.BoundedContexts.SessionTracking.Application.EventHandlers;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Hubs;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.EventHandlers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "BlockA-StoreSignalR")]
public class SessionScoresUpdatedSignalRHandlerTests
{
    [Fact]
    public async Task Handle_SendsScoringConfiguredToSessionGroup_WithScoringTypeAndScoreData()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var hubContextMock = new Mock<IHubContext<GameStateHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();

        hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        clientsMock.Setup(c => c.Group($"session:{sessionId}")).Returns(clientProxyMock.Object);

        var handler = new SessionScoresUpdatedSignalRHandler(
            hubContextMock.Object,
            NullLogger<SessionScoresUpdatedSignalRHandler>.Instance);

        var @event = new SessionScoresUpdatedEvent(sessionId, ScoreType.Points, "{\"scores\":[]}");

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert: hub.Clients.Group("session:<id>").SendAsync("ScoringConfigured", payload)
        clientProxyMock.Verify(c => c.SendCoreAsync(
            "ScoringConfigured",
            It.Is<object?[]>(args =>
                args.Length == 1
                && args[0] != null
                && args[0]!.GetType().GetProperty("scoringType")!.GetValue(args[0])!.Equals("Points")
                && args[0]!.GetType().GetProperty("scoreData")!.GetValue(args[0])!.Equals("{\"scores\":[]}")
            ),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
}
