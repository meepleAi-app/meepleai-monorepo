using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for ApplySuggestionCommandHandler.
/// Issue #2404 - Player Mode apply suggestion
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ApplySuggestionCommandHandlerTests
{
    private readonly Mock<IGameSessionStateRepository> _sessionStateRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<ApplySuggestionCommandHandler>> _loggerMock;
    private readonly ApplySuggestionCommandHandler _handler;

    public ApplySuggestionCommandHandlerTests()
    {
        _sessionStateRepositoryMock = new Mock<IGameSessionStateRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<ApplySuggestionCommandHandler>>();
        _handler = new ApplySuggestionCommandHandler(
            _sessionStateRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidSuggestion_AppliesStateChangesAndReturnsUpdatedState()
    {
        // Arrange
        var sessionState = CreateGameSessionState();
        var suggestionId = Guid.NewGuid();
        var stateChanges = new Dictionary<string, object>
        {
            { "turn", 2 },
            { "currentPlayer", "Bob" }
        };
        var command = new ApplySuggestionCommand(
            SessionId: sessionState.GameSessionId,
            SuggestionId: suggestionId,
            StateChanges: stateChanges,
            UserId: Guid.NewGuid());

        _sessionStateRepositoryMock
            .Setup(r => r.GetBySessionIdAsync(sessionState.GameSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sessionState.Id, result.Id);

        _sessionStateRepositoryMock.Verify(
            r => r.UpdateAsync(sessionState, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentSession_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var command = new ApplySuggestionCommand(
            SessionId: sessionId,
            SuggestionId: Guid.NewGuid(),
            StateChanges: new Dictionary<string, object> { { "test", "value" } },
            UserId: Guid.NewGuid());

        _sessionStateRepositoryMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSessionState?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains(sessionId.ToString(), exception.Message);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var sessionState = CreateGameSessionState();
        var command = new ApplySuggestionCommand(
            SessionId: sessionState.GameSessionId,
            SuggestionId: Guid.NewGuid(),
            StateChanges: new Dictionary<string, object> { { "test", "value" } },
            UserId: Guid.NewGuid());

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _sessionStateRepositoryMock
            .Setup(r => r.GetBySessionIdAsync(sessionState.GameSessionId, token))
            .ReturnsAsync(sessionState);

        // Act
        await _handler.Handle(command, token);

        // Assert
        _sessionStateRepositoryMock.Verify(
            r => r.GetBySessionIdAsync(sessionState.GameSessionId, token),
            Times.Once);
        _sessionStateRepositoryMock.Verify(
            r => r.UpdateAsync(sessionState, token),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CreatesSnapshotAfterApplyingChanges()
    {
        // Arrange
        var sessionState = CreateGameSessionState();
        var initialSnapshotCount = sessionState.Snapshots.Count;
        var command = new ApplySuggestionCommand(
            SessionId: sessionState.GameSessionId,
            SuggestionId: Guid.NewGuid(),
            StateChanges: new Dictionary<string, object> { { "action", "move" } },
            UserId: Guid.NewGuid());

        _sessionStateRepositoryMock
            .Setup(r => r.GetBySessionIdAsync(sessionState.GameSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - snapshot should be created
        Assert.Equal(initialSnapshotCount + 1, sessionState.Snapshots.Count);
    }

    [Fact]
    public async Task Handle_IncrementsVersionAfterUpdate()
    {
        // Arrange
        var sessionState = CreateGameSessionState();
        var initialVersion = sessionState.Version;
        var command = new ApplySuggestionCommand(
            SessionId: sessionState.GameSessionId,
            SuggestionId: Guid.NewGuid(),
            StateChanges: new Dictionary<string, object> { { "update", true } },
            UserId: Guid.NewGuid());

        _sessionStateRepositoryMock
            .Setup(r => r.GetBySessionIdAsync(sessionState.GameSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - version should be incremented (state update + snapshot both increment)
        Assert.True(sessionState.Version > initialVersion);
    }

    private static GameSessionState CreateGameSessionState()
    {
        return GameSessionState.Create(
            id: Guid.NewGuid(),
            gameSessionId: Guid.NewGuid(),
            templateId: Guid.NewGuid(),
            initialState: JsonDocument.Parse("{\"turn\": 1, \"currentPlayer\": \"Alice\"}"),
            createdBy: "system"
        );
    }
}
