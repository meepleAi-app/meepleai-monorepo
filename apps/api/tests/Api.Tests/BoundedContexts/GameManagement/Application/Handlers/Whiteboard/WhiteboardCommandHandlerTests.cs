using Api.BoundedContexts.GameManagement.Application.Commands.Whiteboard;
using Api.BoundedContexts.GameManagement.Application.Events;
using Api.BoundedContexts.GameManagement.Application.Commands.Whiteboard;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.WhiteboardState;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.Whiteboard;

/// <summary>
/// Unit tests for WhiteboardState command handlers.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class WhiteboardCommandHandlerTests
{
    private readonly Mock<IWhiteboardStateRepository> _whiteboardRepoMock;
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly Mock<ISessionBroadcastService> _broadcastMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private static readonly Guid UserId = Guid.NewGuid();

    public WhiteboardCommandHandlerTests()
    {
        _whiteboardRepoMock = new Mock<IWhiteboardStateRepository>();
        _sessionRepoMock = new Mock<ILiveSessionRepository>();
        _broadcastMock = new Mock<ISessionBroadcastService>();
        _uowMock = new Mock<IUnitOfWork>();
    }

    // ========================================================================
    // InitializeWhiteboardCommandHandler
    // ========================================================================

    [Fact]
    public async Task Initialize_WhenSessionNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var handler = new InitializeWhiteboardCommandHandler(
            _whiteboardRepoMock.Object, _sessionRepoMock.Object, _uowMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new InitializeWhiteboardCommand(sessionId, UserId),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Initialize_WhenAlreadyInitialized_ThrowsConflictException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSession(sessionId);
        var existing = new WhiteboardState(Guid.NewGuid(), sessionId, UserId);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _whiteboardRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = new InitializeWhiteboardCommandHandler(
            _whiteboardRepoMock.Object, _sessionRepoMock.Object, _uowMock.Object);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new InitializeWhiteboardCommand(sessionId, UserId),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Initialize_WithValidArgs_CreatesWhiteboardAndSaves()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSession(sessionId);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _whiteboardRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhiteboardState?)null);

        var handler = new InitializeWhiteboardCommandHandler(
            _whiteboardRepoMock.Object, _sessionRepoMock.Object, _uowMock.Object);

        var result = await handler.Handle(
            new InitializeWhiteboardCommand(sessionId, UserId),
            TestContext.Current.CancellationToken);

        result.SessionId.Should().Be(sessionId);
        Assert.Empty(result.Strokes);
        result.StructuredJson.Should().Be("{}");
        _whiteboardRepoMock.Verify(r => r.AddAsync(It.IsAny<WhiteboardState>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Initialize_Constructor_WithNullRepo_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InitializeWhiteboardCommandHandler(null!, _sessionRepoMock.Object, _uowMock.Object));
    }

    // ========================================================================
    // AddStrokeCommandHandler
    // ========================================================================

    [Fact]
    public async Task AddStroke_WhenWhiteboardNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _whiteboardRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhiteboardState?)null);

        var handler = new AddStrokeCommandHandler(
            _whiteboardRepoMock.Object, _broadcastMock.Object, _uowMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new AddStrokeCommand(sessionId, "s1", "{}", UserId),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddStroke_WithValidArgs_AddsStrokeAndSaves()
    {
        var sessionId = Guid.NewGuid();
        var whiteboard = new WhiteboardState(Guid.NewGuid(), sessionId, UserId);

        _whiteboardRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(whiteboard);

        var handler = new AddStrokeCommandHandler(
            _whiteboardRepoMock.Object, _broadcastMock.Object, _uowMock.Object);

        var result = await handler.Handle(
            new AddStrokeCommand(sessionId, "stroke-1", "{\"path\":[]}", UserId),
            TestContext.Current.CancellationToken);

        result.Strokes.Should().ContainSingle();
        result.Strokes[0].Id.Should().Be("stroke-1");
        _whiteboardRepoMock.Verify(r => r.UpdateAsync(It.IsAny<WhiteboardState>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddStroke_WithValidArgs_PublishesStrokeAddedEvent()
    {
        var sessionId = Guid.NewGuid();
        var whiteboard = new WhiteboardState(Guid.NewGuid(), sessionId, UserId);

        _whiteboardRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(whiteboard);

        var handler = new AddStrokeCommandHandler(
            _whiteboardRepoMock.Object, _broadcastMock.Object, _uowMock.Object);

        await handler.Handle(
            new AddStrokeCommand(sessionId, "stroke-1", "{}", UserId),
            TestContext.Current.CancellationToken);

        _broadcastMock.Verify(b => b.PublishAsync(
            sessionId,
            It.Is<StrokeAddedEvent>(e => e.StrokeId == "stroke-1" && e.ModifiedBy == UserId),
            It.IsAny<EventVisibility>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================================================================
    // RemoveStrokeCommandHandler
    // ========================================================================

    [Fact]
    public async Task RemoveStroke_WhenWhiteboardNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _whiteboardRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhiteboardState?)null);

        var handler = new RemoveStrokeCommandHandler(
            _whiteboardRepoMock.Object, _broadcastMock.Object, _uowMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new RemoveStrokeCommand(sessionId, "stroke-1", UserId),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveStroke_WithExistingStroke_RemovesAndSaves()
    {
        var sessionId = Guid.NewGuid();
        var whiteboard = new WhiteboardState(Guid.NewGuid(), sessionId, UserId);
        whiteboard.AddStroke("stroke-1", "{}", UserId);
        whiteboard.AddStroke("stroke-2", "{}", UserId);

        _whiteboardRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(whiteboard);

        var handler = new RemoveStrokeCommandHandler(
            _whiteboardRepoMock.Object, _broadcastMock.Object, _uowMock.Object);

        var result = await handler.Handle(
            new RemoveStrokeCommand(sessionId, "stroke-1", UserId),
            TestContext.Current.CancellationToken);

        result.Strokes.Should().ContainSingle();
        _broadcastMock.Verify(b => b.PublishAsync(
            sessionId,
            It.Is<StrokeRemovedEvent>(e => e.StrokeId == "stroke-1"),
            It.IsAny<EventVisibility>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================================================================
    // UpdateStructuredCommandHandler
    // ========================================================================

    [Fact]
    public async Task UpdateStructured_WhenWhiteboardNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _whiteboardRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhiteboardState?)null);

        var handler = new UpdateStructuredCommandHandler(
            _whiteboardRepoMock.Object, _broadcastMock.Object, _uowMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new UpdateStructuredCommand(sessionId, "{}", UserId),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateStructured_WithValidJson_UpdatesAndBroadcasts()
    {
        var sessionId = Guid.NewGuid();
        var whiteboard = new WhiteboardState(Guid.NewGuid(), sessionId, UserId);
        var newJson = "{\"tokens\":[]}";

        _whiteboardRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(whiteboard);

        var handler = new UpdateStructuredCommandHandler(
            _whiteboardRepoMock.Object, _broadcastMock.Object, _uowMock.Object);

        var result = await handler.Handle(
            new UpdateStructuredCommand(sessionId, newJson, UserId),
            TestContext.Current.CancellationToken);

        result.StructuredJson.Should().Be(newJson);
        _broadcastMock.Verify(b => b.PublishAsync(
            sessionId,
            It.Is<StructuredUpdatedEvent>(e => e.StructuredJson == newJson),
            It.IsAny<EventVisibility>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================================================================
    // ClearWhiteboardCommandHandler
    // ========================================================================

    [Fact]
    public async Task Clear_WhenWhiteboardNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _whiteboardRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhiteboardState?)null);

        var handler = new ClearWhiteboardCommandHandler(
            _whiteboardRepoMock.Object, _broadcastMock.Object, _uowMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ClearWhiteboardCommand(sessionId, UserId),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Clear_WithStrokes_ClearsAndBroadcasts()
    {
        var sessionId = Guid.NewGuid();
        var whiteboard = new WhiteboardState(Guid.NewGuid(), sessionId, UserId);
        whiteboard.AddStroke("s1", "{}", UserId);
        whiteboard.AddStroke("s2", "{}", UserId);
        whiteboard.UpdateStructured("{\"tokens\":[]}", UserId);

        _whiteboardRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(whiteboard);

        var handler = new ClearWhiteboardCommandHandler(
            _whiteboardRepoMock.Object, _broadcastMock.Object, _uowMock.Object);

        var result = await handler.Handle(
            new ClearWhiteboardCommand(sessionId, UserId),
            TestContext.Current.CancellationToken);

        Assert.Empty(result.Strokes);
        result.StructuredJson.Should().Be("{}");
        _broadcastMock.Verify(b => b.PublishAsync(
            sessionId,
            It.Is<WhiteboardClearedEvent>(e => e.ClearedBy == UserId),
            It.IsAny<EventVisibility>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static LiveGameSession CreateSession(Guid sessionId)
        => LiveGameSession.Create(id: sessionId, createdByUserId: Guid.NewGuid(), gameName: "Test Game");
}
