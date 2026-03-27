using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;
using Api.BoundedContexts.GameManagement.Application.Queries.GameSessionContext;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.GameSessionContext;

/// <summary>
/// Unit tests for GetGameSessionContextQueryHandler and RefreshGameSessionContextQueryHandler.
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GameSessionContextQueryHandlerTests
{
    private readonly Mock<IGameSessionOrchestratorService> _orchestratorMock;

    public GameSessionContextQueryHandlerTests()
    {
        _orchestratorMock = new Mock<IGameSessionOrchestratorService>();
    }

    private static GameSessionContextDto CreateTestContext(Guid sessionId)
    {
        return new GameSessionContextDto(
            SessionId: sessionId,
            PrimaryGameId: Guid.NewGuid(),
            ExpansionGameIds: new List<Guid>(),
            AllGameIds: new List<Guid> { Guid.NewGuid() },
            KbCardIds: new List<Guid>(),
            CurrentPhase: null,
            PrimaryRules: null,
            ExpansionRules: new List<RulebookAnalysisSummaryDto>(),
            MissingAnalysisGameNames: new List<string>(),
            GamesWithoutPdf: new List<Guid>(),
            DegradationLevel: SessionDegradationLevel.NoAI);
    }

    // ========================================================================
    // GetGameSessionContextQueryHandler Tests
    // ========================================================================

    [Fact]
    public async Task GetHandler_DelegatesToOrchestratorBuildContext()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var expectedContext = CreateTestContext(sessionId);

        _orchestratorMock
            .Setup(x => x.BuildContextAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedContext);

        var handler = new GetGameSessionContextQueryHandler(_orchestratorMock.Object);
        var query = new GetGameSessionContextQuery(sessionId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().Be(sessionId);
        _orchestratorMock.Verify(x => x.BuildContextAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetHandler_PropagatesNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _orchestratorMock
            .Setup(x => x.BuildContextAsync(sessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("LiveGameSession", sessionId.ToString()));

        var handler = new GetGameSessionContextQueryHandler(_orchestratorMock.Object);
        var query = new GetGameSessionContextQuery(sessionId);

        // Act & Assert
        await ((Func<Task>)(() => handler.Handle(query, CancellationToken.None))).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetHandler_ThrowsOnNullQuery()
    {
        var handler = new GetGameSessionContextQueryHandler(_orchestratorMock.Object);
        await ((Func<Task>)(() => handler.Handle(null!, CancellationToken.None))).Should().ThrowAsync<ArgumentNullException>();
    }

    // ========================================================================
    // RefreshGameSessionContextQueryHandler Tests
    // ========================================================================

    [Fact]
    public async Task RefreshHandler_DelegatesToOrchestratorRefreshContext()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var expectedContext = CreateTestContext(sessionId);

        _orchestratorMock
            .Setup(x => x.RefreshContextAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedContext);

        var handler = new RefreshGameSessionContextQueryHandler(_orchestratorMock.Object, Mock.Of<Api.Services.IHybridCacheService>());
        var query = new RefreshGameSessionContextQuery(sessionId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().Be(sessionId);
        _orchestratorMock.Verify(x => x.RefreshContextAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshHandler_PropagatesNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _orchestratorMock
            .Setup(x => x.RefreshContextAsync(sessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("LiveGameSession", sessionId.ToString()));

        var handler = new RefreshGameSessionContextQueryHandler(_orchestratorMock.Object, Mock.Of<Api.Services.IHybridCacheService>());
        var query = new RefreshGameSessionContextQuery(sessionId);

        // Act & Assert
        await ((Func<Task>)(() => handler.Handle(query, CancellationToken.None))).Should().ThrowAsync<NotFoundException>();
    }
}
