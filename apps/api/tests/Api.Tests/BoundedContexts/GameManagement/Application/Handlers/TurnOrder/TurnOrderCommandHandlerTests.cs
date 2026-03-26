using Api.BoundedContexts.GameManagement.Application.Commands.TurnOrder;
using Api.BoundedContexts.GameManagement.Application.Events;
using Api.BoundedContexts.GameManagement.Application.Commands.TurnOrder;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.TurnOrder;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.TurnOrderHandlers;

/// <summary>
/// Unit tests for TurnOrder command handlers.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class TurnOrderCommandHandlerTests
{
    private readonly Mock<ITurnOrderRepository> _turnOrderRepoMock;
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly Mock<ISessionBroadcastService> _broadcastMock;
    private readonly Mock<IUnitOfWork> _uowMock;

    public TurnOrderCommandHandlerTests()
    {
        _turnOrderRepoMock = new Mock<ITurnOrderRepository>();
        _sessionRepoMock = new Mock<ILiveSessionRepository>();
        _broadcastMock = new Mock<ISessionBroadcastService>();
        _uowMock = new Mock<IUnitOfWork>();
    }

    // ========================================================================
    // InitializeTurnOrderCommandHandler
    // ========================================================================

    [Fact]
    public async Task Initialize_WhenSessionNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var handler = new InitializeTurnOrderCommandHandler(
            _turnOrderRepoMock.Object, _sessionRepoMock.Object, _uowMock.Object);

        var act = () =>
            handler.Handle(new InitializeTurnOrderCommand(sessionId, new[] { "Alice" }),
                TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Initialize_WhenAlreadyInitialized_ThrowsConflictException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSession(sessionId);
        var existing = new TurnOrder(Guid.NewGuid(), sessionId, new[] { "Alice" });

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _turnOrderRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = new InitializeTurnOrderCommandHandler(
            _turnOrderRepoMock.Object, _sessionRepoMock.Object, _uowMock.Object);

        var act = () =>
            handler.Handle(new InitializeTurnOrderCommand(sessionId, new[] { "Alice" }),
                TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Initialize_WithValidArgs_CreatesTurnOrderAndSaves()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSession(sessionId);
        var players = new[] { "Alice", "Bob", "Charlie" };

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _turnOrderRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TurnOrder?)null);

        var handler = new InitializeTurnOrderCommandHandler(
            _turnOrderRepoMock.Object, _sessionRepoMock.Object, _uowMock.Object);

        var result = await handler.Handle(
            new InitializeTurnOrderCommand(sessionId, players),
            TestContext.Current.CancellationToken);

        result.SessionId.Should().Be(sessionId);
        result.PlayerOrder.Should().Equal(players);
        result.CurrentIndex.Should().Be(0);
        result.RoundNumber.Should().Be(1);
        result.CurrentPlayer.Should().Be("Alice");

        _turnOrderRepoMock.Verify(r => r.AddAsync(It.IsAny<TurnOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Initialize_Constructor_WithNullTurnOrderRepo_ThrowsArgumentNullException()
    {
        var act = () =>
            new InitializeTurnOrderCommandHandler(null!, _sessionRepoMock.Object, _uowMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Initialize_Constructor_WithNullSessionRepo_ThrowsArgumentNullException()
    {
        var act = () =>
            new InitializeTurnOrderCommandHandler(_turnOrderRepoMock.Object, null!, _uowMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Initialize_Constructor_WithNullUoW_ThrowsArgumentNullException()
    {
        var act = () =>
            new InitializeTurnOrderCommandHandler(_turnOrderRepoMock.Object, _sessionRepoMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ========================================================================
    // AdvanceTurnCommandHandler
    // ========================================================================

    [Fact]
    public async Task Advance_WhenTurnOrderNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _turnOrderRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TurnOrder?)null);

        var handler = new AdvanceTurnCommandHandler(
            _turnOrderRepoMock.Object, _broadcastMock.Object, _uowMock.Object);

        var act = () =>
            handler.Handle(new AdvanceTurnCommand(sessionId), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Advance_WithValidTurnOrder_AdvancesPlayerAndSaves()
    {
        var sessionId = Guid.NewGuid();
        var turnOrder = new TurnOrder(Guid.NewGuid(), sessionId, new[] { "Alice", "Bob", "Charlie" });

        _turnOrderRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(turnOrder);

        var handler = new AdvanceTurnCommandHandler(
            _turnOrderRepoMock.Object, _broadcastMock.Object, _uowMock.Object);

        var result = await handler.Handle(
            new AdvanceTurnCommand(sessionId), TestContext.Current.CancellationToken);

        result.CurrentPlayer.Should().Be("Bob");
        result.RoundNumber.Should().Be(1);
        _turnOrderRepoMock.Verify(r => r.UpdateAsync(It.IsAny<TurnOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Advance_WithValidTurnOrder_PublishesTurnAdvancedEvent()
    {
        var sessionId = Guid.NewGuid();
        var turnOrder = new TurnOrder(Guid.NewGuid(), sessionId, new[] { "Alice", "Bob" });

        _turnOrderRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(turnOrder);

        var handler = new AdvanceTurnCommandHandler(
            _turnOrderRepoMock.Object, _broadcastMock.Object, _uowMock.Object);

        await handler.Handle(new AdvanceTurnCommand(sessionId), TestContext.Current.CancellationToken);

        _broadcastMock.Verify(b => b.PublishAsync(
            sessionId,
            It.Is<TurnAdvancedEvent>(e =>
                e.PreviousPlayerName == "Alice" &&
                e.CurrentPlayerName == "Bob"),
            It.IsAny<EventVisibility>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Advance_WhenLastPlayerPassed_IncrementsRoundAndBroadcasts()
    {
        var sessionId = Guid.NewGuid();
        var turnOrder = new TurnOrder(Guid.NewGuid(), sessionId, new[] { "Alice", "Bob" });
        turnOrder.Advance(); // Bob is now current

        _turnOrderRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(turnOrder);

        var handler = new AdvanceTurnCommandHandler(
            _turnOrderRepoMock.Object, _broadcastMock.Object, _uowMock.Object);

        var result = await handler.Handle(
            new AdvanceTurnCommand(sessionId), TestContext.Current.CancellationToken);

        result.CurrentPlayer.Should().Be("Alice");
        result.RoundNumber.Should().Be(2);
        _broadcastMock.Verify(b => b.PublishAsync(
            sessionId,
            It.Is<TurnAdvancedEvent>(e => e.RoundNumber == 2),
            It.IsAny<EventVisibility>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Advance_Constructor_WithNullBroadcastService_ThrowsArgumentNullException()
    {
        var act = () =>
            new AdvanceTurnCommandHandler(_turnOrderRepoMock.Object, null!, _uowMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    // ========================================================================
    // ReorderPlayersCommandHandler
    // ========================================================================

    [Fact]
    public async Task Reorder_WhenTurnOrderNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _turnOrderRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TurnOrder?)null);

        var handler = new ReorderPlayersCommandHandler(_turnOrderRepoMock.Object, _uowMock.Object);

        var act = () =>
            handler.Handle(
                new ReorderPlayersCommand(sessionId, new[] { "Bob", "Alice" }),
                TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Reorder_WithValidArgs_ReordersPlayersAndSaves()
    {
        var sessionId = Guid.NewGuid();
        var turnOrder = new TurnOrder(Guid.NewGuid(), sessionId, new[] { "Alice", "Bob" });

        _turnOrderRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(turnOrder);

        var handler = new ReorderPlayersCommandHandler(_turnOrderRepoMock.Object, _uowMock.Object);
        var newOrder = new[] { "Bob", "Charlie", "Alice" };

        var result = await handler.Handle(
            new ReorderPlayersCommand(sessionId, newOrder),
            TestContext.Current.CancellationToken);

        result.PlayerOrder.Should().Equal(newOrder);
        _turnOrderRepoMock.Verify(r => r.UpdateAsync(It.IsAny<TurnOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================================================================
    // ResetTurnOrderCommandHandler
    // ========================================================================

    [Fact]
    public async Task Reset_WhenTurnOrderNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _turnOrderRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TurnOrder?)null);

        var handler = new ResetTurnOrderCommandHandler(_turnOrderRepoMock.Object, _uowMock.Object);

        var act = () =>
            handler.Handle(new ResetTurnOrderCommand(sessionId), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Reset_AfterAdvancing_ResetsToFirstPlayerRoundOne()
    {
        var sessionId = Guid.NewGuid();
        var turnOrder = new TurnOrder(Guid.NewGuid(), sessionId, new[] { "Alice", "Bob" });
        turnOrder.Advance(); // Bob
        turnOrder.Advance(); // wrap → round 2

        _turnOrderRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(turnOrder);

        var handler = new ResetTurnOrderCommandHandler(_turnOrderRepoMock.Object, _uowMock.Object);

        var result = await handler.Handle(
            new ResetTurnOrderCommand(sessionId), TestContext.Current.CancellationToken);

        result.CurrentPlayer.Should().Be("Alice");
        result.CurrentIndex.Should().Be(0);
        result.RoundNumber.Should().Be(1);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static LiveGameSession CreateSession(Guid sessionId)
        => LiveGameSession.Create(id: sessionId, createdByUserId: Guid.NewGuid(), gameName: "Test Game");
}
