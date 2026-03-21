using Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;
using Api.BoundedContexts.Authentication.Application.Queries.AccessRequest;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Integration;

/// <summary>
/// Integration tests for the access request feature — handler + repository lifecycle.
/// Uses InMemory EF Core (same pattern as OAuthCallbackIntegrationTests) for speed
/// and simplicity. The AccessRequestRepository is exercised against real EF Core
/// change-tracking instead of a mock.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public sealed class AccessRequestIntegrationTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IAccessRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AccessRequestIntegrationTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _repository = new AccessRequestRepository(_dbContext, TestDbContextFactory.CreateMockEventCollector().Object);
        _unitOfWork = new EfCoreUnitOfWork(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    // ─────────────────────────────────────────────────────────────────────────
    // RequestAccessCommandHandler
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RequestAccess_WithNewEmail_CreatesAccessRequest()
    {
        // Arrange
        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new RequestAccessCommandHandler(_repository, userRepoMock.Object, _unitOfWork);
        var command = new RequestAccessCommand("newuser@example.com");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        await _dbContext.SaveChangesAsync();
        var stored = await _repository.GetPendingByEmailAsync("newuser@example.com");
        stored.Should().NotBeNull();
        stored!.Email.Should().Be("newuser@example.com");
        stored.Status.Should().Be(AccessRequestStatus.Pending);
    }

    [Fact]
    public async Task RequestAccess_WithUppercaseEmail_NormalizesToLowercase()
    {
        // Arrange
        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new RequestAccessCommandHandler(_repository, userRepoMock.Object, _unitOfWork);
        var command = new RequestAccessCommand("VISITOR@EXAMPLE.COM");

        // Act
        await handler.Handle(command, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert
        var stored = await _repository.GetPendingByEmailAsync("visitor@example.com");
        stored.Should().NotBeNull();
        stored!.Email.Should().Be("visitor@example.com");
    }

    [Fact]
    public async Task RequestAccess_WithExistingPendingRequest_ReturnsSilentlyWithoutDuplicate()
    {
        // Arrange — seed an existing pending request
        var existing = AccessRequest.Create("duplicate@example.com");
        await _repository.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new RequestAccessCommandHandler(_repository, userRepoMock.Object, _unitOfWork);
        var command = new RequestAccessCommand("duplicate@example.com");

        // Act — email enumeration prevention: should not throw even for existing
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert — idempotent success, no duplicate created
        await act.Should().NotThrowAsync();
        await _dbContext.SaveChangesAsync();
        var count = await _repository.CountByStatusAsync(AccessRequestStatus.Pending);
        count.Should().Be(1); // only the original
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ApproveAccessRequestCommandHandler
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveAccessRequest_WithPendingRequest_SetsStatusToApproved()
    {
        // Arrange
        var request = AccessRequest.Create("pending@example.com");
        await _repository.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var handler = new ApproveAccessRequestCommandHandler(_repository, _unitOfWork);
        var adminId = Guid.NewGuid();
        var command = new ApproveAccessRequestCommand(request.Id, adminId);

        // Act
        await handler.Handle(command, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _repository.GetByIdAsync(request.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(AccessRequestStatus.Approved);
        updated.ReviewedBy.Should().Be(adminId);
        updated.ReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ApproveAccessRequest_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var handler = new ApproveAccessRequestCommandHandler(_repository, _unitOfWork);
        var command = new ApproveAccessRequestCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<Api.Middleware.Exceptions.NotFoundException>();
    }

    [Fact]
    public async Task ApproveAccessRequest_AlreadyApproved_IsIdempotent()
    {
        // Arrange — seed already approved entity
        var request = AccessRequest.Create("already@example.com");
        request.Approve(Guid.NewGuid());
        await _repository.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var handler = new ApproveAccessRequestCommandHandler(_repository, _unitOfWork);
        var command = new ApproveAccessRequestCommand(request.Id, Guid.NewGuid());

        // Act — approving again should not throw
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RejectAccessRequestCommandHandler
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RejectAccessRequest_WithPendingRequest_SetsStatusToRejected()
    {
        // Arrange
        var request = AccessRequest.Create("toReject@example.com");
        await _repository.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var handler = new RejectAccessRequestCommandHandler(
            _repository, _unitOfWork, new NoOpEmailService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RejectAccessRequestCommandHandler>.Instance);
        var adminId = Guid.NewGuid();
        var command = new RejectAccessRequestCommand(request.Id, adminId, "Not eligible at this time.");

        // Act
        await handler.Handle(command, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _repository.GetByIdAsync(request.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(AccessRequestStatus.Rejected);
        updated.ReviewedBy.Should().Be(adminId);
        updated.RejectionReason.Should().Be("Not eligible at this time.");
    }

    [Fact]
    public async Task RejectAccessRequest_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var handler = new RejectAccessRequestCommandHandler(
            _repository, _unitOfWork, new NoOpEmailService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RejectAccessRequestCommandHandler>.Instance);
        var command = new RejectAccessRequestCommand(Guid.NewGuid(), Guid.NewGuid(), null);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<Api.Middleware.Exceptions.NotFoundException>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BulkApproveAccessRequestsCommandHandler
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkApprove_WithMultiplePendingRequests_ApprovesAll()
    {
        // Arrange — create 3 pending requests
        var req1 = AccessRequest.Create("bulk1@example.com");
        var req2 = AccessRequest.Create("bulk2@example.com");
        var req3 = AccessRequest.Create("bulk3@example.com");
        await _repository.AddAsync(req1);
        await _repository.AddAsync(req2);
        await _repository.AddAsync(req3);
        await _dbContext.SaveChangesAsync();

        var handler = new BulkApproveAccessRequestsCommandHandler(_repository, _unitOfWork);
        var adminId = Guid.NewGuid();
        var command = new BulkApproveAccessRequestsCommand(
            new List<Guid> { req1.Id, req2.Id, req3.Id },
            adminId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert
        result.Processed.Should().Be(3);
        result.Succeeded.Should().Be(3);
        result.Failed.Should().Be(0);

        var updated1 = await _repository.GetByIdAsync(req1.Id);
        var updated2 = await _repository.GetByIdAsync(req2.Id);
        var updated3 = await _repository.GetByIdAsync(req3.Id);
        updated1!.Status.Should().Be(AccessRequestStatus.Approved);
        updated2!.Status.Should().Be(AccessRequestStatus.Approved);
        updated3!.Status.Should().Be(AccessRequestStatus.Approved);
    }

    [Fact]
    public async Task BulkApprove_WithMixOfValidAndMissingIds_ReportsFailuresForMissing()
    {
        // Arrange — one real, one phantom
        var realRequest = AccessRequest.Create("real@example.com");
        await _repository.AddAsync(realRequest);
        await _dbContext.SaveChangesAsync();

        var phantomId = Guid.NewGuid();
        var handler = new BulkApproveAccessRequestsCommandHandler(_repository, _unitOfWork);
        var command = new BulkApproveAccessRequestsCommand(
            new List<Guid> { realRequest.Id, phantomId },
            Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert
        result.Processed.Should().Be(2);
        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(1);
    }

    [Fact]
    public async Task BulkApprove_WithMoreThan25Ids_ThrowsArgumentException()
    {
        // Arrange
        var ids = Enumerable.Range(0, 26).Select(_ => Guid.NewGuid()).ToList();
        var handler = new BulkApproveAccessRequestsCommandHandler(_repository, _unitOfWork);
        var command = new BulkApproveAccessRequestsCommand(ids, Guid.NewGuid());

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetAccessRequestsQueryHandler
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccessRequests_WithNoFilter_ReturnsAllRequests()
    {
        // Arrange
        var r1 = AccessRequest.Create("list1@example.com");
        var r2 = AccessRequest.Create("list2@example.com");
        await _repository.AddAsync(r1);
        await _repository.AddAsync(r2);
        await _dbContext.SaveChangesAsync();

        var handler = new GetAccessRequestsQueryHandler(_repository);
        var query = new GetAccessRequestsQuery();

        // Act
        var response = await handler.Handle(query, CancellationToken.None);

        // Assert
        response.Items.Count.Should().BeGreaterThanOrEqualTo(2);
        response.TotalCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAccessRequests_FilteredByPendingStatus_ReturnsOnlyPending()
    {
        // Arrange — one pending, one approved
        var pending = AccessRequest.Create("onlypending@example.com");
        var approved = AccessRequest.Create("onlyapproved@example.com");
        approved.Approve(Guid.NewGuid());
        await _repository.AddAsync(pending);
        await _repository.AddAsync(approved);
        await _dbContext.SaveChangesAsync();

        var handler = new GetAccessRequestsQueryHandler(_repository);
        var query = new GetAccessRequestsQuery(Status: "Pending");

        // Act
        var response = await handler.Handle(query, CancellationToken.None);

        // Assert
        response.Items.Should().OnlyContain(r => r.Status == "Pending");
    }
}
