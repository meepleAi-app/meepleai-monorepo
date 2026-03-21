using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class GetSessionEventsQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ISessionEventRepository> _eventRepoMock = new();
    private readonly GetSessionEventsQueryHandler _handler;

    public GetSessionEventsQueryHandlerTests()
    {
        _handler = new GetSessionEventsQueryHandler(
            _sessionRepoMock.Object, _eventRepoMock.Object);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var query = new GetSessionEventsQuery(Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidSession_ReturnsPaginatedEvents()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var events = new List<SessionEvent>
        {
            SessionEvent.Create(session.Id, "dice_roll", "{\"value\":6}", Guid.NewGuid(), "player")
        };

        _eventRepoMock.Setup(r => r.GetBySessionIdAsync(session.Id, null, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);
        _eventRepoMock.Setup(r => r.CountBySessionIdAsync(session.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var query = new GetSessionEventsQuery(session.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Events);
        Assert.Equal(1, result.TotalCount);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectHasMore()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _eventRepoMock.Setup(r => r.GetBySessionIdAsync(session.Id, null, 10, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionEvent>());
        _eventRepoMock.Setup(r => r.CountBySessionIdAsync(session.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);

        var query = new GetSessionEventsQuery(session.Id, Limit: 10, Offset: 0);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.HasMore);
        Assert.Equal(25, result.TotalCount);
    }
}
