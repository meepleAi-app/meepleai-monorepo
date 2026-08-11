using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Tests.Constants;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>#3025 L1: LiveSessionGameStateEvent is forwarded to SSE "session:game-state".</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class LiveSessionStreamForwarderGameStateTests
{
    [Fact]
    public async Task Handle_GameStateEvent_BroadcastsSessionGameState()
    {
        var gateway = new Mock<ILiveSessionStreamGateway>();
        var sut = new LiveSessionStreamForwarder(gateway.Object, NullLogger<LiveSessionStreamForwarder>.Instance);
        var sessionId = Guid.NewGuid();

        await sut.Handle(new LiveSessionGameStateEvent(sessionId, """{"k":"v"}"""), CancellationToken.None);

        gateway.Verify(g => g.BroadcastAsync(
            sessionId,
            It.Is<LiveSessionStreamEvent>(e => e.Type == "session:game-state"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
