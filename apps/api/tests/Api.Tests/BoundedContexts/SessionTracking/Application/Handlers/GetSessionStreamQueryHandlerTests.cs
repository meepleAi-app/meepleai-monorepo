using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class GetSessionStreamQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ISessionSyncService> _syncServiceMock = new();
    private readonly GetSessionStreamQueryHandler _handler;

    public GetSessionStreamQueryHandlerTests()
    {
        _handler = new GetSessionStreamQueryHandler(
            _sessionRepoMock.Object, _syncServiceMock.Object);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var query = new GetSessionStreamQuery(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var query = new GetSessionStreamQuery(session.Id, Guid.NewGuid()); // random user

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SessionOwner_ReturnsStream()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = Session.Create(userId, Guid.NewGuid(), SessionType.Generic);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _syncServiceMock.Setup(s => s.SubscribeToSessionEvents(session.Id, It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable());

        var query = new GetSessionStreamQuery(session.Id, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        _syncServiceMock.Verify(s => s.SubscribeToSessionEvents(session.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async IAsyncEnumerable<INotification> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }
}
