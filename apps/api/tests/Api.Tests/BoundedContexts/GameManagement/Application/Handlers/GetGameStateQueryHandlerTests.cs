using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for GetGameStateQueryHandler.
/// Issue #2403: GameSessionState Entity
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetGameStateQueryHandlerTests
{
    private readonly Mock<IGameSessionStateRepository> _stateRepositoryMock;
    private readonly GetGameStateQueryHandler _handler;

    public GetGameStateQueryHandlerTests()
    {
        _stateRepositoryMock = new Mock<IGameSessionStateRepository>();
        _handler = new GetGameStateQueryHandler(_stateRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingState_ReturnsStateDto()
    {
        // Arrange
        var state = CreateGameSessionState();
        var query = new GetGameStateQuery(state.GameSessionId);

        _stateRepositoryMock
            .Setup(r => r.GetBySessionIdAsync(state.GameSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(state.Id, result.Id);
        Assert.Equal(state.GameSessionId, result.GameSessionId);
        Assert.Equal(state.TemplateId, result.TemplateId);
        Assert.Equal(state.Version, result.Version);
    }

    [Fact]
    public async Task Handle_WithNonExistentState_ReturnsNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var query = new GetGameStateQuery(sessionId);

        _stateRepositoryMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSessionState?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var query = new GetGameStateQuery(sessionId);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _stateRepositoryMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, token))
            .ReturnsAsync((GameSessionState?)null);

        // Act
        await _handler.Handle(query, token);

        // Assert
        _stateRepositoryMock.Verify(r => r.GetBySessionIdAsync(sessionId, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    private static GameSessionState CreateGameSessionState()
    {
        return GameSessionState.Create(
            id: Guid.NewGuid(),
            gameSessionId: Guid.NewGuid(),
            templateId: Guid.NewGuid(),
            initialState: JsonDocument.Parse("{\"turn\": 1, \"players\": []}"),
            createdBy: "system"
        );
    }
}
