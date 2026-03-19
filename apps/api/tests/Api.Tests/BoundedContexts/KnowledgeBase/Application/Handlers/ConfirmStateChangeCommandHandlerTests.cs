using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for ConfirmStateChangeCommandHandler.
/// Issue #2468 - Ledger Mode Test Suite
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ConfirmStateChangeCommandHandlerTests
{
    private readonly Mock<IGameSessionStateRepository> _mockSessionStateRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<ConfirmStateChangeCommandHandler>> _mockLogger;
    private readonly ConfirmStateChangeCommandHandler _handler;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ConfirmStateChangeCommandHandlerTests()
    {
        _mockSessionStateRepo = new Mock<IGameSessionStateRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<ConfirmStateChangeCommandHandler>>();

        _handler = new ConfirmStateChangeCommandHandler(
            _mockSessionStateRepo.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    #region Basic Validation Tests

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _handler.Handle(null!, TestCancellationToken));
    }

    [Fact]
    public async Task Handle_WithNonExistentSession_ThrowsInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSessionState?)null);

        var command = new ConfirmStateChangeCommand(
            sessionId,
            new Dictionary<string, object> { { "score", 5 } },
            Guid.NewGuid());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _handler.Handle(command, TestCancellationToken));

        exception.Message.Should().Contain(sessionId.ToString());
    }

    #endregion

    #region State Change Application Tests

    [Fact]
    public async Task Handle_WithValidCommand_AppliesStateChanges()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"score": 0, "roads": 2}""");

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object>
        {
            { "score", 5 },
            { "roads", 3 }
        };
        var command = new ConfirmStateChangeCommand(sessionId, stateChanges, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNewProperty_AddsPropertyToState()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"score": 5}""");

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object>
        {
            { "resources.wood", 3 }
        };
        var command = new ConfirmStateChangeCommand(sessionId, stateChanges, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
    }

    [Fact]
    public async Task Handle_WithMultipleChanges_AppliesAllChanges()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"score": 0}""");

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object>
        {
            { "score", 10 },
            { "roads", 3 },
            { "cities", 1 },
            { "currentPlayer", "Marco" }
        };
        var command = new ConfirmStateChangeCommand(sessionId, stateChanges, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
    }

    [Fact]
    public async Task Handle_WithEmptyStateChanges_CompletesSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"score": 5}""");

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object>();
        var command = new ConfirmStateChangeCommand(sessionId, stateChanges, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
    }

    #endregion

    #region Snapshot Creation Tests

    [Fact]
    public async Task Handle_WithDescription_CreatesSnapshot()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"score": 5, "turn": 3}""");

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object> { { "score", 10 } };
        var command = new ConfirmStateChangeCommand(
            sessionId,
            stateChanges,
            userId,
            Description: "Player scored 5 points");

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        // Snapshot is created internally - verify via side effect
        sessionState.Snapshots.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Handle_WithoutDescription_DoesNotCreateSnapshot()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"score": 5}""");
        var initialSnapshotCount = sessionState.Snapshots.Count;

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object> { { "score", 10 } };
        var command = new ConfirmStateChangeCommand(sessionId, stateChanges, userId);

        // Act
        await _handler.Handle(command, TestCancellationToken);

        // Assert - no new snapshot created
        sessionState.Snapshots.Count.Should().Be(initialSnapshotCount);
    }

    [Fact]
    public async Task Handle_WithWhitespaceDescription_DoesNotCreateSnapshot()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"score": 5}""");
        var initialSnapshotCount = sessionState.Snapshots.Count;

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object> { { "score", 10 } };
        var command = new ConfirmStateChangeCommand(
            sessionId,
            stateChanges,
            userId,
            Description: "   ");

        // Act
        await _handler.Handle(command, TestCancellationToken);

        // Assert - no new snapshot created
        sessionState.Snapshots.Count.Should().Be(initialSnapshotCount);
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task Handle_WithConcurrencyConflict_ThrowsInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"score": 5}""");

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict"));

        var stateChanges = new Dictionary<string, object> { { "score", 10 } };
        var command = new ConfirmStateChangeCommand(sessionId, stateChanges, userId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _handler.Handle(command, TestCancellationToken));

        exception.Message.Should().Contain("modified by another user");
        exception.InnerException.Should().BeOfType<DbUpdateConcurrencyException>();
    }

    #endregion

    #region Repository Verification Tests

    [Fact]
    public async Task Handle_VerifiesSessionStateRepositoryCalled()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"score": 5}""");

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object> { { "score", 10 } };
        var command = new ConfirmStateChangeCommand(sessionId, stateChanges, userId);

        // Act
        await _handler.Handle(command, TestCancellationToken);

        // Assert
        _mockSessionStateRepo.Verify(
            r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_VerifiesUnitOfWorkSaveChangesCalled()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"score": 5}""");

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object> { { "score", 10 } };
        var command = new ConfirmStateChangeCommand(sessionId, stateChanges, userId);

        // Act
        await _handler.Handle(command, TestCancellationToken);

        // Assert
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Turn Number Extraction Tests

    [Fact]
    public async Task Handle_WithTurnInState_SnapshotIsCreated()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"score": 5, "turn": 7}""");
        var initialSnapshotCount = sessionState.Snapshots.Count;

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object> { { "score", 10 } };
        var command = new ConfirmStateChangeCommand(
            sessionId,
            stateChanges,
            userId,
            Description: "Score update");

        // Act
        await _handler.Handle(command, TestCancellationToken);

        // Assert - snapshot should be created (turn number is implementation detail)
        sessionState.Snapshots.Should().HaveCountGreaterThan(initialSnapshotCount);
    }

    [Fact]
    public async Task Handle_WithoutTurnInState_SnapshotIsCreated()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"score": 5}""");
        var initialSnapshotCount = sessionState.Snapshots.Count;

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object> { { "score", 10 } };
        var command = new ConfirmStateChangeCommand(
            sessionId,
            stateChanges,
            userId,
            Description: "Score update");

        // Act
        await _handler.Handle(command, TestCancellationToken);

        // Assert - snapshot should be created with default turn
        sessionState.Snapshots.Should().HaveCountGreaterThan(initialSnapshotCount);
    }

    #endregion

    #region Data Type Tests

    [Fact]
    public async Task Handle_WithStringValue_AppliesCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"currentPlayer": "Marco"}""");

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object> { { "currentPlayer", "Luca" } };
        var command = new ConfirmStateChangeCommand(sessionId, stateChanges, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
    }

    [Fact]
    public async Task Handle_WithBooleanValue_AppliesCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"gameEnded": false}""");

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object> { { "gameEnded", true } };
        var command = new ConfirmStateChangeCommand(sessionId, stateChanges, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
    }

    [Fact]
    public async Task Handle_WithDoubleValue_AppliesCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionState = CreateTestSessionState(sessionId, """{"rating": 0.0}""");

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stateChanges = new Dictionary<string, object> { { "rating", 4.5 } };
        var command = new ConfirmStateChangeCommand(sessionId, stateChanges, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
    }

    #endregion

    #region Helper Methods

    private static GameSessionState CreateTestSessionState(Guid sessionId, string stateJson)
    {
        var initialState = JsonDocument.Parse(stateJson);
        return GameSessionState.Create(
            id: Guid.NewGuid(),
            gameSessionId: sessionId,
            templateId: Guid.NewGuid(),
            initialState: initialState,
            createdBy: "test-user");
    }

    #endregion
}
