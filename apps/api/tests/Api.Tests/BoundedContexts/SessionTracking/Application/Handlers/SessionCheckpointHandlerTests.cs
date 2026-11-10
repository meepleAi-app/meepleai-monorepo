using Api.BoundedContexts.SessionTracking.Application.Commands;
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
    public async Task ListHandler_StrangerWhoIsNeitherOwnerNorParticipant_ThrowsForbiddenAndReadsNothing()
    {
        // #4883: the query carried RequesterId and the handler ignored it.
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        var mockSessionRepo = new Mock<ISessionRepository>();
        mockSessionRepo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var mockCheckpointRepo = new Mock<ISessionCheckpointRepository>();
        var handler = new ListSessionCheckpointsQueryHandler(mockSessionRepo.Object, mockCheckpointRepo.Object);

        var act = () =>
            handler.Handle(new ListSessionCheckpointsQuery(session.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        mockCheckpointRepo.Verify(
            r => r.GetBySessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListHandler_Owner_ReturnsCheckpoints()
    {
        var ownerId = Guid.NewGuid();
        var session = Session.Create(ownerId, Guid.NewGuid(), SessionType.Generic);
        var mockSessionRepo = new Mock<ISessionRepository>();
        mockSessionRepo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var mockCheckpointRepo = new Mock<ISessionCheckpointRepository>();
        mockCheckpointRepo.Setup(r => r.GetBySessionIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionCheckpoint>());
        var handler = new ListSessionCheckpointsQueryHandler(mockSessionRepo.Object, mockCheckpointRepo.Object);

        var result = await handler.Handle(new ListSessionCheckpointsQuery(session.Id, ownerId), CancellationToken.None);

        result.Checkpoints.Should().BeEmpty();
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
