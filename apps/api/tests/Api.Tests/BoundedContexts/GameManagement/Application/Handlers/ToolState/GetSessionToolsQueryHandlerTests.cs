using Api.BoundedContexts.GameManagement.Application.DTOs.ToolState;
using Api.BoundedContexts.GameManagement.Application.Commands.ToolState;
using Api.BoundedContexts.GameManagement.Application.Queries.ToolState;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

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

        var act = () =>
            _sut.Handle(query, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
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

        result.BaseTools.TurnOrder.ToolType.Should().Be(BaseToolType.TurnOrder);
        result.BaseTools.DiceSet.ToolType.Should().Be(BaseToolType.DiceSet);
        result.BaseTools.Whiteboard.ToolType.Should().Be(BaseToolType.Whiteboard);
        result.BaseTools.Scoreboard.ToolType.Should().Be(BaseToolType.Scoreboard);
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

        (result.BaseTools.TurnOrder.IsAvailable).Should().BeTrue();
        (result.BaseTools.DiceSet.IsAvailable).Should().BeTrue();
        (result.BaseTools.Whiteboard.IsAvailable).Should().BeTrue();
        (result.BaseTools.Scoreboard.IsAvailable).Should().BeTrue();
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

        result.BaseTools.TurnOrder.ToolId.Should().Be("turn-order");
        result.BaseTools.DiceSet.ToolId.Should().Be("dice-set");
        result.BaseTools.Whiteboard.ToolId.Should().Be("whiteboard");
        result.BaseTools.Scoreboard.ToolId.Should().Be("scoreboard");
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

        result.CustomTools.Should().BeEmpty();
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

        result.CustomTools.Count.Should().Be(2);
        result.CustomTools.Should().Contain(t => t.ToolName == "Battle Dice");
        result.CustomTools.Should().Contain(t => t.ToolName == "HP Counter");
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

        result.ToolkitId.Should().BeNull();
        result.SessionId.Should().Be(sessionId);
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

        result.ToolkitId.Should().Be(toolkitId);
    }

    // ========================================================================
    // Guard clauses
    // ========================================================================

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        var act = () =>
            _sut.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullSessionRepository_ThrowsArgumentNullException()
    {
        var act = () =>
            new GetSessionToolsQueryHandler(null!, _toolStateRepoMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullToolStateRepository_ThrowsArgumentNullException()
    {
        var act = () =>
            new GetSessionToolsQueryHandler(_sessionRepoMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
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
