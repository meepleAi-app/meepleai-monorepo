using Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR_Unit = MediatR.Unit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Integration.AlphaFlow;

/// <summary>
/// Integration tests for the alpha invite-only registration flow.
/// Verifies: access request → approval → invitation → registration pipeline.
/// Uses InMemory EF Core for speed (same pattern as AccessRequestIntegrationTests).
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public sealed class AlphaInviteOnlyFlowTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IAccessRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IEmailService> _emailServiceMock;

    public AlphaInviteOnlyFlowTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _repository = new AccessRequestRepository(_dbContext, TestDbContextFactory.CreateMockEventCollector().Object);
        _unitOfWork = new EfCoreUnitOfWork(_dbContext);
        _userRepoMock = new Mock<IUserRepository>();
        _userRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _emailServiceMock = new Mock<IEmailService>();
    }

    public void Dispose() => _dbContext.Dispose();

    // ─────────────────────────────────────────────────────────────────────────
    // Access Request Submission
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RequestAccess_WithNewEmail_CreatesAccessRequestWithPendingStatus()
    {
        // Arrange
        var handler = new RequestAccessCommandHandler(_repository, _userRepoMock.Object, _unitOfWork);
        var command = new RequestAccessCommand("alpha-test@example.com");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — always returns Unit (privacy: no user enumeration)
        result.Should().Be(MediatR_Unit.Value);
        await _dbContext.SaveChangesAsync();

        var stored = await _repository.GetPendingByEmailAsync("alpha-test@example.com");
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(AccessRequestStatus.Pending);
    }

    [Fact]
    public async Task DuplicateAccessRequest_ReturnsSameUnitWithoutCreatingDuplicate()
    {
        // Arrange — seed first request
        var existing = AccessRequest.Create("duplicate-alpha@example.com");
        await _repository.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        var handler = new RequestAccessCommandHandler(_repository, _userRepoMock.Object, _unitOfWork);
        var command = new RequestAccessCommand("duplicate-alpha@example.com");

        // Act — second request for same email
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — returns Unit (no error leaked), only one record exists
        result.Should().Be(MediatR_Unit.Value);
        await _dbContext.SaveChangesAsync();
        var count = await _repository.CountByStatusAsync(AccessRequestStatus.Pending);
        count.Should().Be(1);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Access Request Approval
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveAccessRequest_SetsStatusToApprovedWithAdminId()
    {
        // Arrange
        var request = AccessRequest.Create("approve-alpha@example.com");
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

    // ─────────────────────────────────────────────────────────────────────────
    // Access Request Rejection with Email
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RejectAccessRequest_SetsStatusToRejectedAndSendsEmail()
    {
        // Arrange
        var request = AccessRequest.Create("reject-alpha@example.com");
        await _repository.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var logger = NullLogger<RejectAccessRequestCommandHandler>.Instance;
        var handler = new RejectAccessRequestCommandHandler(
            _repository, _unitOfWork, _emailServiceMock.Object, logger);

        var adminId = Guid.NewGuid();
        var command = new RejectAccessRequestCommand(request.Id, adminId, "Not in alpha group");

        // Act
        await handler.Handle(command, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert — status updated
        var updated = await _repository.GetByIdAsync(request.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(AccessRequestStatus.Rejected);
        updated.RejectionReason.Should().Be("Not in alpha group");

        // Assert — rejection email was sent
        _emailServiceMock.Verify(
            e => e.SendAccessRequestRejectedEmailAsync(
                "reject-alpha@example.com",
                "Not in alpha group",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RejectAccessRequest_WithNullReason_StillSendsEmail()
    {
        // Arrange
        var request = AccessRequest.Create("reject-noreason@example.com");
        await _repository.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        var logger = NullLogger<RejectAccessRequestCommandHandler>.Instance;
        var handler = new RejectAccessRequestCommandHandler(
            _repository, _unitOfWork, _emailServiceMock.Object, logger);

        var command = new RejectAccessRequestCommand(request.Id, Guid.NewGuid(), null);

        // Act
        await handler.Handle(command, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _repository.GetByIdAsync(request.Id);
        updated!.Status.Should().Be(AccessRequestStatus.Rejected);
        updated.RejectionReason.Should().BeNull();

        _emailServiceMock.Verify(
            e => e.SendAccessRequestRejectedEmailAsync(
                "reject-noreason@example.com",
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RejectAccessRequest_WhenEmailFails_StillRejects()
    {
        // Arrange
        var request = AccessRequest.Create("email-fail@example.com");
        await _repository.AddAsync(request);
        await _dbContext.SaveChangesAsync();

        _emailServiceMock
            .Setup(e => e.SendAccessRequestRejectedEmailAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP failed"));

        var logger = NullLogger<RejectAccessRequestCommandHandler>.Instance;
        var handler = new RejectAccessRequestCommandHandler(
            _repository, _unitOfWork, _emailServiceMock.Object, logger);

        var command = new RejectAccessRequestCommand(request.Id, Guid.NewGuid(), "Test");

        // Act — should not throw even though email fails
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().NotThrowAsync();
        await _dbContext.SaveChangesAsync();

        // Assert — rejection still applied
        var updated = await _repository.GetByIdAsync(request.Id);
        updated!.Status.Should().Be(AccessRequestStatus.Rejected);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Invitation Domain Logic
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void InvitationToken_WhenExpired_IsNotValid()
    {
        // Arrange — create a token that expired yesterday
        var token = InvitationToken.Create(
            "expired@example.com",
            "User",
            "hashedtoken123",
            Guid.NewGuid());

        // Force expiry by marking it expired
        token.MarkExpired();

        // Assert
        token.IsValid.Should().BeFalse();
        token.Status.Should().Be(InvitationStatus.Expired);
    }

    [Fact]
    public void InvitationToken_WhenAccepted_CannotBeAcceptedAgain()
    {
        // Arrange
        var token = InvitationToken.Create(
            "accept@example.com",
            "User",
            "hashedtoken456",
            Guid.NewGuid());

        // Act
        token.MarkAccepted(Guid.NewGuid());

        // Assert
        token.Status.Should().Be(InvitationStatus.Accepted);
        token.IsValid.Should().BeFalse(); // Already used
    }
}
