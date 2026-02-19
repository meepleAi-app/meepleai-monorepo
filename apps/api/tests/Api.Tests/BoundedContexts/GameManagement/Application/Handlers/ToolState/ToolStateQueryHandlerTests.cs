using Api.BoundedContexts.GameManagement.Application.Handlers.ToolState;
using Api.BoundedContexts.GameManagement.Application.Queries.ToolState;
using Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.ToolState;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class ToolStateQueryHandlerTests
{
    private readonly Mock<IToolStateRepository> _repoMock;

    public ToolStateQueryHandlerTests()
    {
        _repoMock = new Mock<IToolStateRepository>();
    }

    // ========================================================================
    // GetToolStatesQueryHandler
    // ========================================================================

    [Fact]
    public async Task GetToolStates_WithExistingSession_ReturnsAllStates()
    {
        var sessionId = Guid.NewGuid();
        var toolkitId = Guid.NewGuid();
        var toolStates = new List<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>
        {
            new(Guid.NewGuid(), sessionId, toolkitId, "Dice", ToolType.Dice, "{\"diceType\":\"D6\"}"),
            new(Guid.NewGuid(), sessionId, toolkitId, "HP", ToolType.Counter, "{\"currentValue\":20}")
        };

        _repoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolStates);

        var handler = new GetToolStatesQueryHandler(_repoMock.Object);
        var query = new GetToolStatesQuery(sessionId);

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.ToolName == "Dice");
        Assert.Contains(result, r => r.ToolName == "HP");
    }

    [Fact]
    public async Task GetToolStates_WithNoStates_ReturnsEmptyList()
    {
        var sessionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState>());

        var handler = new GetToolStatesQueryHandler(_repoMock.Object);
        var query = new GetToolStatesQuery(sessionId);

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetToolStates_WithNullQuery_ThrowsArgumentNullException()
    {
        var handler = new GetToolStatesQueryHandler(_repoMock.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // GetToolStateQueryHandler
    // ========================================================================

    [Fact]
    public async Task GetToolState_WhenExists_ReturnsDto()
    {
        var sessionId = Guid.NewGuid();
        var toolState = new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), sessionId, Guid.NewGuid(), "Battle Dice", ToolType.Dice, "{\"diceType\":\"D6\"}");

        _repoMock.Setup(r => r.GetBySessionAndToolNameAsync(sessionId, "Battle Dice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolState);

        var handler = new GetToolStateQueryHandler(_repoMock.Object);
        var query = new GetToolStateQuery(sessionId, "Battle Dice");

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Battle Dice", result.ToolName);
        Assert.Equal(ToolType.Dice, result.ToolType);
    }

    [Fact]
    public async Task GetToolState_WhenNotFound_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetBySessionAndToolNameAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState?)null);

        var handler = new GetToolStateQueryHandler(_repoMock.Object);
        var query = new GetToolStateQuery(Guid.NewGuid(), "Missing");

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetToolState_WithNullQuery_ThrowsArgumentNullException()
    {
        var handler = new GetToolStateQueryHandler(_repoMock.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
