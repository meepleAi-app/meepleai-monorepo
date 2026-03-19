using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Tests for LaunchSessionAgentCommandHandler.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class LaunchSessionAgentCommandHandlerTests
{
    private readonly Mock<IAgentSessionRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<LaunchSessionAgentCommandHandler>> _mockLogger;
    private readonly LaunchSessionAgentCommandHandler _handler;

    public LaunchSessionAgentCommandHandlerTests()
    {
        _mockRepository = new Mock<IAgentSessionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<LaunchSessionAgentCommandHandler>>();
        _handler = new LaunchSessionAgentCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesAgentSession()
    {
        // Arrange
        var gameSessionId = Guid.NewGuid();
        var typologyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var player1Id = Guid.NewGuid();
        var gameStateJson = $"{{\"currentTurn\":1,\"activePlayer\":\"{player1Id}\",\"playerScores\":{{}},\"gamePhase\":\"Setup\",\"lastAction\":\"started\"}}";

        _mockRepository
            .Setup(r => r.HasActiveSessionAsync(gameSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new LaunchSessionAgentCommand(
            gameSessionId,
            typologyId,
            userId,
            agentId,
            gameId,
            gameStateJson);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenActiveSessionExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var gameSessionId = Guid.NewGuid();
        var command = new LaunchSessionAgentCommand(
            gameSessionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "{}");

        _mockRepository
            .Setup(r => r.HasActiveSessionAsync(gameSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidGameStateJson_ThrowsArgumentException()
    {
        // Arrange
        var command = new LaunchSessionAgentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "invalid json");

        _mockRepository
            .Setup(r => r.HasActiveSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));
    }
}
