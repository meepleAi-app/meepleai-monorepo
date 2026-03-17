using Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;
using AuthAccessRequest = Api.BoundedContexts.Authentication.Domain.Entities.AccessRequest;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class ApproveAccessRequestCommandHandlerTests
{
    private readonly Mock<IAccessRequestRepository> _repoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ApproveAccessRequestCommandHandler _handler;

    public ApproveAccessRequestCommandHandlerTests()
    {
        _repoMock = new Mock<IAccessRequestRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new ApproveAccessRequestCommandHandler(_repoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_PendingRequest_ApprovesSuccessfully()
    {
        var request = AuthAccessRequest.Create("test@example.com");
        _repoMock.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var command = new ApproveAccessRequestCommand(request.Id, Guid.NewGuid());
        await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AccessRequestStatus.Approved, request.Status);
        _repoMock.Verify(x => x.UpdateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AlreadyApproved_ReturnsSuccessWithoutModification()
    {
        var request = AuthAccessRequest.Create("test@example.com");
        request.Approve(Guid.NewGuid());
        _repoMock.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var command = new ApproveAccessRequestCommand(request.Id, Guid.NewGuid());
        await _handler.Handle(command, CancellationToken.None);

        _repoMock.Verify(x => x.UpdateAsync(It.IsAny<AuthAccessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectedRequest_ThrowsConflictException()
    {
        var request = AuthAccessRequest.Create("test@example.com");
        request.Reject(Guid.NewGuid());
        _repoMock.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var command = new ApproveAccessRequestCommand(request.Id, Guid.NewGuid());
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NonExistentId_ThrowsNotFoundException()
    {
        _repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthAccessRequest?)null);

        var command = new ApproveAccessRequestCommand(Guid.NewGuid(), Guid.NewGuid());
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
