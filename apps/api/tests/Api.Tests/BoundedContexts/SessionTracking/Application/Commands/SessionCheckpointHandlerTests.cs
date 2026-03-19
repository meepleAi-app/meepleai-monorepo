using Api.BoundedContexts.SessionTracking.Application.Handlers;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class SessionCheckpointHandlerTests
{
    [Fact]
    public async Task ListHandler_SessionNotFound_ThrowsNotFoundException()
    {
        var mockSessionRepo = new Mock<ISessionRepository>();
        mockSessionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);
        var mockCheckpointRepo = new Mock<ISessionCheckpointRepository>();

        var handler = new ListSessionCheckpointsQueryHandler(mockSessionRepo.Object, mockCheckpointRepo.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ListSessionCheckpointsQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public void ListHandler_NullSessionRepo_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new ListSessionCheckpointsQueryHandler(null!, new Mock<ISessionCheckpointRepository>().Object));

    [Fact]
    public void ListHandler_NullCheckpointRepo_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new ListSessionCheckpointsQueryHandler(new Mock<ISessionRepository>().Object, null!));

    [Fact]
    public void CreateHandler_NullSessionRepo_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new CreateSessionCheckpointCommandHandler(null!, null!, null!, null!, null!));

    [Fact]
    public void RestoreHandler_NullSessionRepo_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new RestoreSessionCheckpointCommandHandler(null!, null!, null!, null!));
}
