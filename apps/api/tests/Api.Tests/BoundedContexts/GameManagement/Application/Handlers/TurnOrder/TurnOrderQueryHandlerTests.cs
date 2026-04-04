using Api.BoundedContexts.GameManagement.Application.Commands.TurnOrder;
using Api.BoundedContexts.GameManagement.Application.Queries.TurnOrder;
using Api.BoundedContexts.GameManagement.Domain.Entities.TurnOrder;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

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

        var act = () =>
            _sut.Handle(new GetTurnOrderQuery(sessionId), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
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

        result.SessionId.Should().Be(sessionId);
        result.CurrentIndex.Should().Be(1);
        result.CurrentPlayer.Should().Be("Bob");
        result.NextPlayer.Should().Be("Charlie");
        result.RoundNumber.Should().Be(1);
        result.PlayerOrder.Should().BeEquivalentTo(new[] { "Alice", "Bob", "Charlie" });
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        var act = () =>
            _sut.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        var act = () =>
            new GetTurnOrderQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
