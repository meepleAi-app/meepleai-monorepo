using Api.BoundedContexts.GameManagement.Application.Handlers.TurnOrder;
using Api.BoundedContexts.GameManagement.Application.Queries.TurnOrder;
using Api.BoundedContexts.GameManagement.Domain.Entities.TurnOrder;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.TurnOrderHandlers;

/// <summary>
/// Unit tests for GetTurnOrderQueryHandler.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class TurnOrderQueryHandlerTests
{
    private readonly Mock<ITurnOrderRepository> _repoMock;
    private readonly GetTurnOrderQueryHandler _sut;

    public TurnOrderQueryHandlerTests()
    {
        _repoMock = new Mock<ITurnOrderRepository>();
        _sut = new GetTurnOrderQueryHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_WhenTurnOrderNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TurnOrder?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.Handle(new GetTurnOrderQuery(sessionId), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithExistingTurnOrder_ReturnsMappedDto()
    {
        var sessionId = Guid.NewGuid();
        var turnOrder = new TurnOrder(Guid.NewGuid(), sessionId, new[] { "Alice", "Bob", "Charlie" });
        turnOrder.Advance(); // Bob

        _repoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(turnOrder);

        var result = await _sut.Handle(
            new GetTurnOrderQuery(sessionId), TestContext.Current.CancellationToken);

        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(1, result.CurrentIndex);
        Assert.Equal("Bob", result.CurrentPlayer);
        Assert.Equal("Charlie", result.NextPlayer);
        Assert.Equal(1, result.RoundNumber);
        Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, result.PlayerOrder);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetTurnOrderQueryHandler(null!));
    }
}
