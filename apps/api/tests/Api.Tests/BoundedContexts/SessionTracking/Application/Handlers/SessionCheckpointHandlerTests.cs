using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

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

        var act = () =>
            handler.Handle(new ListSessionCheckpointsQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void ListHandler_NullSessionRepo_Throws() =>
        ((Action)(() => new ListSessionCheckpointsQueryHandler(null!, new Mock<ISessionCheckpointRepository>().Object))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void ListHandler_NullCheckpointRepo_Throws() =>
        ((Action)(() => new ListSessionCheckpointsQueryHandler(new Mock<ISessionRepository>().Object, null!))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void CreateHandler_NullSessionRepo_Throws() =>
        ((Action)(() => new CreateSessionCheckpointCommandHandler(null!, null!, null!, null!, null!))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void RestoreHandler_NullSessionRepo_Throws() =>
        ((Action)(() => new RestoreSessionCheckpointCommandHandler(null!, null!, null!, null!))).Should().Throw<ArgumentNullException>();
}
