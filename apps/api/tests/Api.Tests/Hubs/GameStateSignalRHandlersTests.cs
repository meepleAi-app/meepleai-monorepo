using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Hubs;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Hubs;

// ── SignalR Event Handler Tests ──

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class DisputeResolvedSignalRHandlerTests
{
    private readonly Mock<IHubContext<GameStateHub>> _mockHubContext;
    private readonly Mock<IHubClients> _mockHubClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly DisputeResolvedSignalRHandler _handler;

    public DisputeResolvedSignalRHandlerTests()
    {
        _mockHubContext = new Mock<IHubContext<GameStateHub>>();
        _mockHubClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();

        _mockHubContext.Setup(h => h.Clients).Returns(_mockHubClients.Object);

        var logger = new Mock<ILogger<DisputeResolvedSignalRHandler>>();
        _handler = new DisputeResolvedSignalRHandler(_mockHubContext.Object, logger.Object);
    }

    [Fact]
    public async Task Handle_BroadcastsToCorrectSessionGroup()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var dispute = new RuleDisputeEntry(
            Guid.NewGuid(),
            "Can I place 2 workers?",
            "No, one per turn.",
            new List<string> { "Rulebook p.12" },
            "Alice",
            DateTime.UtcNow);
        var @event = new DisputeResolvedEvent(sessionId, dispute);
        var expectedGroup = $"session:{sessionId}";

        _mockHubClients
            .Setup(c => c.Group(expectedGroup))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync("DisputeResolved", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mockHubClients.Verify(c => c.Group(expectedGroup), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync("DisputeResolved", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SendsDisputePayloadWithCorrectFields()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
        var dispute = new RuleDisputeEntry(
            disputeId,
            "Move question",
            "Allowed.",
            new List<string> { "p.5", "p.8" },
            "Bob",
            new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc));
        var @event = new DisputeResolvedEvent(sessionId, dispute);

        _mockHubClients
            .Setup(c => c.Group(It.IsAny<string>()))
            .Returns(_mockClientProxy.Object);

        object?[]? capturedArgs = null;
        _mockClientProxy
            .Setup(p => p.SendCoreAsync("DisputeResolved", It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((_, args, _) => capturedArgs = args)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert — payload is an anonymous object; verify via dynamic reflection
        capturedArgs.Should().NotBeNull();
        capturedArgs!.Should().HaveCount(1);
        var payload = capturedArgs[0]!;
        var payloadType = payload.GetType();
        payloadType.GetProperty("Id")!.GetValue(payload).Should().Be(disputeId);
        payloadType.GetProperty("Description")!.GetValue(payload).Should().Be("Move question");
        payloadType.GetProperty("Verdict")!.GetValue(payload).Should().Be("Allowed.");
        payloadType.GetProperty("RaisedByPlayerName")!.GetValue(payload).Should().Be("Bob");
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class SessionPausedSignalRHandlerTests
{
    private readonly Mock<IHubContext<GameStateHub>> _mockHubContext;
    private readonly Mock<IHubClients> _mockHubClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly SessionPausedSignalRHandler _handler;

    public SessionPausedSignalRHandlerTests()
    {
        _mockHubContext = new Mock<IHubContext<GameStateHub>>();
        _mockHubClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();

        _mockHubContext.Setup(h => h.Clients).Returns(_mockHubClients.Object);

        var logger = new Mock<ILogger<SessionPausedSignalRHandler>>();
        _handler = new SessionPausedSignalRHandler(_mockHubContext.Object, logger.Object);
    }

    [Fact]
    public async Task Handle_BroadcastsToCorrectSessionGroup()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var @event = new SessionPausedEvent(sessionId);
        var expectedGroup = $"session:{sessionId}";

        _mockHubClients
            .Setup(c => c.Group(expectedGroup))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync("SessionPaused", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mockHubClients.Verify(c => c.Group(expectedGroup), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync("SessionPaused", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNotSendToOtherGroups()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var @event = new SessionPausedEvent(sessionId);
        var expectedGroup = $"session:{sessionId}";

        _mockHubClients
            .Setup(c => c.Group(expectedGroup))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert — only the specific group is targeted
        _mockHubClients.Verify(
            c => c.Group(It.Is<string>(g => g != expectedGroup)),
            Times.Never);
    }
}
