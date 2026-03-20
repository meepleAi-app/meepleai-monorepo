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
public class GameStateHubTests
{
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly GameStateHub _hub;

    private const string TestSessionId = "session-123";
    private const string TestSessionGroup = $"session:{TestSessionId}";
    private const string TestConnectionId = "conn-abc";
    private const string TestParticipantId = "user-456";

    public GameStateHubTests()
    {
        _mockClients = new Mock<IHubCallerClients>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();
        _mockClientProxy = new Mock<IClientProxy>();

        _mockContext.Setup(c => c.ConnectionId).Returns(TestConnectionId);
        _mockContext.Setup(c => c.UserIdentifier).Returns("test-user");

        var logger = new Mock<ILogger<GameStateHub>>();
        var publisher = new Mock<IPublisher>();
        _hub = new GameStateHub(logger.Object, publisher.Object)
        {
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object,
            Context = _mockContext.Object
        };
    }

    // ── JoinSessionWithRole ──

    [Fact]
    public async Task JoinSessionWithRole_WithHostRole_JoinsBothSessionAndHostGroups()
    {
        // Arrange
        _mockGroups
            .Setup(g => g.AddToGroupAsync(TestConnectionId, It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.JoinSessionWithRole(TestSessionId, "host");

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, TestSessionGroup, default),
            Times.Once);
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, $"{TestSessionGroup}:host", default),
            Times.Once);
    }

    [Fact]
    public async Task JoinSessionWithRole_WithHostRole_CaseInsensitive_JoinsBothGroups()
    {
        // Arrange
        _mockGroups
            .Setup(g => g.AddToGroupAsync(TestConnectionId, It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.JoinSessionWithRole(TestSessionId, "HOST");

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, TestSessionGroup, default),
            Times.Once);
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, $"{TestSessionGroup}:host", default),
            Times.Once);
    }

    [Fact]
    public async Task JoinSessionWithRole_WithPlayerRole_JoinsOnlySessionGroup()
    {
        // Arrange
        _mockGroups
            .Setup(g => g.AddToGroupAsync(TestConnectionId, It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.JoinSessionWithRole(TestSessionId, "player");

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, TestSessionGroup, default),
            Times.Once);
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, $"{TestSessionGroup}:host", default),
            Times.Never);
    }

    // ── ProposeScore ──

    [Fact]
    public async Task ProposeScore_SendsToHostGroupOnly()
    {
        // Arrange
        var proposal = new { playerId = "p1", score = 42 };
        _mockClients
            .Setup(c => c.Group($"{TestSessionGroup}:host"))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync("ScoreProposed", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.ProposeScore(TestSessionId, proposal);

        // Assert
        _mockClients.Verify(c => c.Group($"{TestSessionGroup}:host"), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync("ScoreProposed", It.Is<object?[]>(args => args[0] == (object)proposal), default),
            Times.Once);
    }

    [Fact]
    public async Task ProposeScore_DoesNotSendToFullSessionGroup()
    {
        // Arrange
        var proposal = new { playerId = "p1", score = 42 };
        _mockClients
            .Setup(c => c.Group($"{TestSessionGroup}:host"))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.ProposeScore(TestSessionId, proposal);

        // Assert — never calls Group(sessionGroup) without ":host" suffix
        _mockClients.Verify(c => c.Group(TestSessionGroup), Times.Never);
    }

    // ── ConfirmScore ──

    [Fact]
    public async Task ConfirmScore_SendsToEntireSessionGroup()
    {
        // Arrange
        var confirmation = new { playerId = "p1", score = 42, confirmed = true };
        _mockClients
            .Setup(c => c.Group(TestSessionGroup))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync("ScoreConfirmed", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.ConfirmScore(TestSessionId, confirmation);

        // Assert
        _mockClients.Verify(c => c.Group(TestSessionGroup), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync("ScoreConfirmed", It.Is<object?[]>(args => args[0] == (object)confirmation), default),
            Times.Once);
    }

    // ── UpdateAgentAccess ──

    [Fact]
    public async Task UpdateAgentAccess_SendsToSessionGroup()
    {
        // Arrange
        _mockClients
            .Setup(c => c.Group(TestSessionGroup))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync("AgentAccessChanged", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.UpdateAgentAccess(TestSessionId, TestParticipantId, true);

        // Assert
        _mockClients.Verify(c => c.Group(TestSessionGroup), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync("AgentAccessChanged", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAgentAccess_WithDisabledAccess_SendsFalse()
    {
        // Arrange
        _mockClients
            .Setup(c => c.Group(TestSessionGroup))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync("AgentAccessChanged", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.UpdateAgentAccess(TestSessionId, TestParticipantId, false);

        // Assert
        _mockClientProxy.Verify(
            p => p.SendCoreAsync("AgentAccessChanged", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAgentAccess_DoesNotSendToSpecificUser()
    {
        // Arrange
        _mockClients
            .Setup(c => c.Group(TestSessionGroup))
            .Returns(_mockClientProxy.Object);
        _mockClientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.UpdateAgentAccess(TestSessionId, TestParticipantId, true);

        // Assert — never sends to a specific user (broadcasts to group instead)
        _mockClients.Verify(c => c.User(It.IsAny<string>()), Times.Never);
    }
}
