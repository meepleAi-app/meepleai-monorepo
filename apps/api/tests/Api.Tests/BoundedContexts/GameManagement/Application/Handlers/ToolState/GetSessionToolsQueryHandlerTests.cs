using Api.BoundedContexts.GameManagement.Application.DTOs.ToolState;
using Api.BoundedContexts.GameManagement.Application.Handlers.ToolState;
using Api.BoundedContexts.GameManagement.Application.Queries.ToolState;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.ToolState;

/// <summary>
/// Unit tests for GetSessionToolsQueryHandler.
/// Issue #4969: Base Toolkit Layer — GET /live-sessions/{sessionId}/tools.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetSessionToolsQueryHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly Mock<IToolStateRepository> _toolStateRepoMock;
    private readonly GetSessionToolsQueryHandler _sut;

    public GetSessionToolsQueryHandlerTests()
    {
        _sessionRepoMock = new Mock<ILiveSessionRepository>();
        _toolStateRepoMock = new Mock<IToolStateRepository>();
        _sut = new GetSessionToolsQueryHandler(_sessionRepoMock.Object, _toolStateRepoMock.Object);
    }

    // ========================================================================
    // Session not found
    // ========================================================================

    [Fact]
    public async Task Handle_WhenSessionNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var query = new GetSessionToolsQuery(sessionId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.Handle(query, TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // Base toolkit — always four tools
    // ========================================================================

    [Fact]
    public async Task Handle_WithValidSession_ReturnsAllFourBaseTools()
    {
        var (sessionId, session) = CreateSession(toolkitId: null);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _toolStateRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>());

        var result = await _sut.Handle(new GetSessionToolsQuery(sessionId), TestContext.Current.CancellationToken);

        Assert.Equal(BaseToolType.TurnOrder, result.BaseTools.TurnOrder.ToolType);
        Assert.Equal(BaseToolType.DiceSet, result.BaseTools.DiceSet.ToolType);
        Assert.Equal(BaseToolType.Whiteboard, result.BaseTools.Whiteboard.ToolType);
        Assert.Equal(BaseToolType.Scoreboard, result.BaseTools.Scoreboard.ToolType);
    }

    [Fact]
    public async Task Handle_WithValidSession_AllBaseToolsAreAvailable()
    {
        var (sessionId, session) = CreateSession(toolkitId: null);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _toolStateRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>());

        var result = await _sut.Handle(new GetSessionToolsQuery(sessionId), TestContext.Current.CancellationToken);

        Assert.True(result.BaseTools.TurnOrder.IsAvailable);
        Assert.True(result.BaseTools.DiceSet.IsAvailable);
        Assert.True(result.BaseTools.Whiteboard.IsAvailable);
        Assert.True(result.BaseTools.Scoreboard.IsAvailable);
    }

    [Fact]
    public async Task Handle_WithValidSession_BaseToolIdsAreStable()
    {
        var (sessionId, session) = CreateSession(toolkitId: null);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _toolStateRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>());

        var result = await _sut.Handle(new GetSessionToolsQuery(sessionId), TestContext.Current.CancellationToken);

        Assert.Equal("turn-order", result.BaseTools.TurnOrder.ToolId);
        Assert.Equal("dice-set", result.BaseTools.DiceSet.ToolId);
        Assert.Equal("whiteboard", result.BaseTools.Whiteboard.ToolId);
        Assert.Equal("scoreboard", result.BaseTools.Scoreboard.ToolId);
    }

    // ========================================================================
    // Custom tools
    // ========================================================================

    [Fact]
    public async Task Handle_WithNoCustomTools_ReturnsEmptyCustomToolsList()
    {
        var (sessionId, session) = CreateSession(toolkitId: null);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _toolStateRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>());

        var result = await _sut.Handle(new GetSessionToolsQuery(sessionId), TestContext.Current.CancellationToken);

        Assert.Empty(result.CustomTools);
    }

    [Fact]
    public async Task Handle_WithCustomTools_ReturnsThemInCustomToolsList()
    {
        var toolkitId = Guid.NewGuid();
        var (sessionId, session) = CreateSession(toolkitId);

        var toolStates = new List<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>
        {
            new(Guid.NewGuid(), sessionId, toolkitId, "Battle Dice", ToolType.Dice, "{\"diceType\":\"D6\"}"),
            new(Guid.NewGuid(), sessionId, toolkitId, "HP Counter", ToolType.Counter, "{\"currentValue\":20}"),
        };

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _toolStateRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolStates);

        var result = await _sut.Handle(new GetSessionToolsQuery(sessionId), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.CustomTools.Count);
        Assert.Contains(result.CustomTools, t => t.ToolName == "Battle Dice");
        Assert.Contains(result.CustomTools, t => t.ToolName == "HP Counter");
    }

    // ========================================================================
    // SessionId and ToolkitId pass-through
    // ========================================================================

    [Fact]
    public async Task Handle_WithNoLinkedToolkit_ReturnsNullToolkitId()
    {
        var (sessionId, session) = CreateSession(toolkitId: null);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _toolStateRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>());

        var result = await _sut.Handle(new GetSessionToolsQuery(sessionId), TestContext.Current.CancellationToken);

        Assert.Null(result.ToolkitId);
        Assert.Equal(sessionId, result.SessionId);
    }

    [Fact]
    public async Task Handle_WithLinkedToolkit_ReturnsToolkitId()
    {
        var toolkitId = Guid.NewGuid();
        var (sessionId, session) = CreateSession(toolkitId);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _toolStateRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>());

        var result = await _sut.Handle(new GetSessionToolsQuery(sessionId), TestContext.Current.CancellationToken);

        Assert.Equal(toolkitId, result.ToolkitId);
    }

    // ========================================================================
    // Guard clauses
    // ========================================================================

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_WithNullSessionRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetSessionToolsQueryHandler(null!, _toolStateRepoMock.Object));
    }

    [Fact]
    public void Constructor_WithNullToolStateRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetSessionToolsQueryHandler(_sessionRepoMock.Object, null!));
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static (Guid sessionId, LiveGameSession session) CreateSession(Guid? toolkitId)
    {
        var sessionId = Guid.NewGuid();
        var session = LiveGameSession.Create(
            id: sessionId,
            createdByUserId: Guid.NewGuid(),
            gameName: "Test Game");

        if (toolkitId.HasValue)
            session.LinkToolkit(toolkitId.Value);

        return (sessionId, session);
    }
}
