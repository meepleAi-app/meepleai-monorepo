using Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;
using AuthAccessRequest = Api.BoundedContexts.Authentication.Domain.Entities.AccessRequest;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class BulkApproveAccessRequestsCommandHandlerTests
{
    private readonly Mock<IAccessRequestRepository> _repoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly BulkApproveAccessRequestsCommandHandler _handler;

    public BulkApproveAccessRequestsCommandHandlerTests()
    {
        _repoMock = new Mock<IAccessRequestRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new BulkApproveAccessRequestsCommandHandler(_repoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ExceedsMaxBatchSize_ThrowsArgumentException()
    {
        var ids = Enumerable.Range(0, 26).Select(_ => Guid.NewGuid()).ToList();
        var command = new BulkApproveAccessRequestsCommand(ids, Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MixedStates_ReturnsPerItemResults()
    {
        var pending = AuthAccessRequest.Create("a@test.com");
        var approved = AuthAccessRequest.Create("b@test.com");
        approved.Approve(Guid.NewGuid());

        _repoMock.Setup(x => x.GetByIdAsync(pending.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        _repoMock.Setup(x => x.GetByIdAsync(approved.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approved);

        var command = new BulkApproveAccessRequestsCommand(
            new[] { pending.Id, approved.Id }, Guid.NewGuid());
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(2, result.Processed);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task Handle_NonExistentId_ReportsFailureForThatItem()
    {
        var nonExistentId = Guid.NewGuid();
        _repoMock.Setup(x => x.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthAccessRequest?)null);

        var command = new BulkApproveAccessRequestsCommand(
            new[] { nonExistentId }, Guid.NewGuid());
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal("Not found", result.Results[0].Error);
    }
}
