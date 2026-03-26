using Api.BoundedContexts.GameManagement.Application.Commands.Whiteboard;
using Api.BoundedContexts.GameManagement.Application.Queries.Whiteboard;
using Api.BoundedContexts.GameManagement.Domain.Entities.WhiteboardState;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.Whiteboard;

/// <summary>
/// Unit tests for WhiteboardState query handler.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class WhiteboardQueryHandlerTests
{
    private readonly Mock<IWhiteboardStateRepository> _whiteboardRepoMock;
    private static readonly Guid UserId = Guid.NewGuid();

    public WhiteboardQueryHandlerTests()
    {
        _whiteboardRepoMock = new Mock<IWhiteboardStateRepository>();
    }

    [Fact]
    public async Task GetWhiteboardState_WhenNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _whiteboardRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhiteboardState?)null);

        var handler = new GetWhiteboardStateQueryHandler(_whiteboardRepoMock.Object);

        var act = () =>
            handler.Handle(new GetWhiteboardStateQuery(sessionId), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetWhiteboardState_WhenExists_ReturnsDto()
    {
        var sessionId = Guid.NewGuid();
        var whiteboard = new WhiteboardState(Guid.NewGuid(), sessionId, UserId);
        whiteboard.AddStroke("s1", "{\"path\":[]}", UserId);
        whiteboard.UpdateStructured("{\"tokens\":[]}", UserId);

        _whiteboardRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(whiteboard);

        var handler = new GetWhiteboardStateQueryHandler(_whiteboardRepoMock.Object);

        var result = await handler.Handle(
            new GetWhiteboardStateQuery(sessionId), TestContext.Current.CancellationToken);

        result.SessionId.Should().Be(sessionId);
        result.Strokes.Should().ContainSingle();
        result.Strokes[0].Id.Should().Be("s1");
        result.StructuredJson.Should().Be("{\"tokens\":[]}");
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        var act = () =>
            new GetWhiteboardStateQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        var handler = new GetWhiteboardStateQueryHandler(_whiteboardRepoMock.Object);

        var act = () =>
            handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
