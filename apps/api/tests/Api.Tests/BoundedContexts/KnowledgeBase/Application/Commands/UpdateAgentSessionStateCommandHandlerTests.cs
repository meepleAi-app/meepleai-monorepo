using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
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
/// Tests for UpdateAgentSessionStateCommandHandler.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class UpdateAgentSessionStateCommandHandlerTests
{
    private readonly Mock<IAgentSessionRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<UpdateAgentSessionStateCommandHandler>> _mockLogger;
    private readonly UpdateAgentSessionStateCommandHandler _handler;

    public UpdateAgentSessionStateCommandHandlerTests()
    {
        _mockRepository = new Mock<IAgentSessionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<UpdateAgentSessionStateCommandHandler>>();
        _handler = new UpdateAgentSessionStateCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesGameState()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var initialState = GameState.Initial(player1Id);
        var session = new AgentSession(
            sessionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            initialState);

        var newGameStateJson = $"{{\"currentTurn\":2,\"activePlayer\":\"{player2Id}\",\"playerScores\":{{\"{player1Id}\":10}},\"gamePhase\":\"MainPhase\",\"lastAction\":\"moved\"}}";

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new UpdateAgentSessionStateCommand(sessionId, newGameStateJson);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.UpdateAsync(It.Is<AgentSession>(s => s.Id == sessionId), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var command = new UpdateAgentSessionStateCommand(sessionId, "{}");

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentSession?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));

        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionInactive_ThrowsInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var session = new AgentSession(
            sessionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            GameState.Initial(playerId));

        session.End(); // Make session inactive

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var newGameStateJson = $"{{\"currentTurn\":2,\"activePlayer\":\"{playerId}\",\"playerScores\":{{}},\"gamePhase\":\"Phase\",\"lastAction\":\"action\"}}";
        var command = new UpdateAgentSessionStateCommand(sessionId, newGameStateJson);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));
    }
}
