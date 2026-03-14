using Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;
using AuthAccessRequest = Api.BoundedContexts.Authentication.Domain.Entities.AccessRequest;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class RejectAccessRequestCommandHandlerTests
{
    private readonly Mock<IAccessRequestRepository> _repoMock;
    private readonly RejectAccessRequestCommandHandler _handler;

    public RejectAccessRequestCommandHandlerTests()
    {
        _repoMock = new Mock<IAccessRequestRepository>();
        _handler = new RejectAccessRequestCommandHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_PendingRequest_RejectsWithReason()
    {
        var request = AuthAccessRequest.Create("test@example.com");
        _repoMock.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await _handler.Handle(
            new RejectAccessRequestCommand(request.Id, Guid.NewGuid(), "Not in beta"),
            CancellationToken.None);

        Assert.Equal(AccessRequestStatus.Rejected, request.Status);
        Assert.Equal("Not in beta", request.RejectionReason);
    }

    [Fact]
    public async Task Handle_AlreadyRejected_ReturnsSuccessWithoutModification()
    {
        var request = AuthAccessRequest.Create("test@example.com");
        request.Reject(Guid.NewGuid());
        _repoMock.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await _handler.Handle(
            new RejectAccessRequestCommand(request.Id, Guid.NewGuid()),
            CancellationToken.None);

        _repoMock.Verify(x => x.UpdateAsync(It.IsAny<AuthAccessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ApprovedRequest_ThrowsConflictException()
    {
        var request = AuthAccessRequest.Create("test@example.com");
        request.Approve(Guid.NewGuid());
        _repoMock.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(
                new RejectAccessRequestCommand(request.Id, Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NonExistentId_ThrowsNotFoundException()
    {
        _repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthAccessRequest?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(
                new RejectAccessRequestCommand(Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None));
    }
}
