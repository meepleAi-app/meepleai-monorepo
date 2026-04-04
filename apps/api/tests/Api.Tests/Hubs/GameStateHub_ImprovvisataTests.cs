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

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GameStateHub_ImprovvisataTests
{
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly GameStateHub _hub;

    private const string TestSessionId = "session-abc";
    private const string TestSessionGroup = $"session:{TestSessionId}";
    private const string TestConnectionId = "conn-xyz";

    public GameStateHub_ImprovvisataTests()
    {
        _mockClients = new Mock<IHubCallerClients>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockPublisher = new Mock<IPublisher>();

        _mockContext.Setup(c => c.ConnectionId).Returns(TestConnectionId);
        _mockContext.Setup(c => c.UserIdentifier).Returns("test-user");

        var logger = new Mock<ILogger<GameStateHub>>();
        _hub = new GameStateHub(logger.Object, _mockPublisher.Object)
        {
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object,
            Context = _mockContext.Object
        };
    }

    // ── NotifyDisputeResolved ──

    [Fact]
    public async Task NotifyDisputeResolved_SendsToCorrectSessionGroup()
    {
        // Arrange
        var verdict = new { Id = Guid.NewGuid(), Description = "Can I play 2 cards?", Verdict = "No." };
        _mockClients
            .Setup(c => c.Group(TestSessionGroup))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync("DisputeResolved", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.NotifyDisputeResolved(TestSessionId, verdict);

        // Assert
        _mockClients.Verify(c => c.Group(TestSessionGroup), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync("DisputeResolved", It.Is<object?[]>(args => args[0] == (object)verdict), default),
            Times.Once);
    }

    [Fact]
    public async Task NotifyDisputeResolved_DoesNotSendToOtherGroups()
    {
        // Arrange
        var verdict = new { Id = Guid.NewGuid(), Verdict = "Yes." };
        _mockClients
            .Setup(c => c.Group(TestSessionGroup))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.NotifyDisputeResolved(TestSessionId, verdict);

        // Assert — should only send to the specific session group
        _mockClients.Verify(c => c.Group(It.Is<string>(g => g != TestSessionGroup)), Times.Never);
    }

    // ── NotifySessionPaused ──

    [Fact]
    public async Task NotifySessionPaused_SendsToCorrectSessionGroup()
    {
        // Arrange
        _mockClients
            .Setup(c => c.Group(TestSessionGroup))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync("SessionPaused", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.NotifySessionPaused(TestSessionId);

        // Assert
        _mockClients.Verify(c => c.Group(TestSessionGroup), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync("SessionPaused", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task NotifySessionPaused_DoesNotSendToHostSubgroup()
    {
        // Arrange
        _mockClients
            .Setup(c => c.Group(TestSessionGroup))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.NotifySessionPaused(TestSessionId);

        // Assert — not restricted to host sub-group
        _mockClients.Verify(c => c.Group($"{TestSessionGroup}:host"), Times.Never);
    }

    // ── NotifyScoreUpdated ──

    [Fact]
    public async Task NotifyScoreUpdated_SendsToCorrectSessionGroup()
    {
        // Arrange
        var scoreUpdate = new { PlayerId = "player-1", Score = 42 };
        _mockClients
            .Setup(c => c.Group(TestSessionGroup))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync("ScoreUpdated", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.NotifyScoreUpdated(TestSessionId, scoreUpdate);

        // Assert
        _mockClients.Verify(c => c.Group(TestSessionGroup), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync("ScoreUpdated", It.Is<object?[]>(args => args[0] == (object)scoreUpdate), default),
            Times.Once);
    }

    // ── AppBackgrounded ──

    [Fact]
    public async Task AppBackgrounded_PublishesAppBackgroundedEvent()
    {
        // Arrange
        var sessionGuid = Guid.NewGuid();
        _mockPublisher
            .Setup(p => p.Publish(It.IsAny<AppBackgroundedEvent>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.AppBackgrounded(sessionGuid.ToString());

        // Assert
        _mockPublisher.Verify(
            p => p.Publish(
                It.Is<AppBackgroundedEvent>(e => e.SessionId == sessionGuid),
                default),
            Times.Once);
    }

    [Fact]
    public async Task AppBackgrounded_WithInvalidSessionId_DoesNotPublishEvent()
    {
        // Arrange — invalid GUID string
        // Act
        await _hub.AppBackgrounded("not-a-guid");

        // Assert
        _mockPublisher.Verify(
            p => p.Publish(It.IsAny<AppBackgroundedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AppBackgrounded_WhenPublisherThrows_DoesNotPropagateException()
    {
        // Arrange
        var sessionGuid = Guid.NewGuid();
        _mockPublisher
            .Setup(p => p.Publish(It.IsAny<AppBackgroundedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Save failed"));

        // Act — must not throw
        var act = async () => await _hub.AppBackgrounded(sessionGuid.ToString());

        // Assert
        await act.Should().NotThrowAsync();
    }
}

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
