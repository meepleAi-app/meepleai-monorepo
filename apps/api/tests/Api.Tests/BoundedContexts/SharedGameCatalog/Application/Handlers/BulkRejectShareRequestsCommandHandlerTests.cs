using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Tests for BulkRejectShareRequestsCommandHandler.
/// Issue #2893: Verify all-or-nothing transaction and audit logging.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class BulkRejectShareRequestsCommandHandlerTests
{
    private readonly Mock<IShareRequestRepository> _mockShareRequestRepository;
    private readonly Mock<IAuditLogRepository> _mockAuditLogRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly BulkRejectShareRequestsCommandHandler _handler;

    public BulkRejectShareRequestsCommandHandlerTests()
    {
        _mockShareRequestRepository = new Mock<IShareRequestRepository>();
        _mockAuditLogRepository = new Mock<IAuditLogRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new BulkRejectShareRequestsCommandHandler(
            _mockShareRequestRepository.Object,
            _mockAuditLogRepository.Object,
            _mockUnitOfWork.Object,
            Mock.Of<ILogger<BulkRejectShareRequestsCommandHandler>>(),
            TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithAllValidRequests_RejectsAllSuccessfully()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var reason = "Does not meet quality standards";
        var shareRequestIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var command = new BulkRejectShareRequestsCommand(shareRequestIds, editorId, reason);

        var shareRequestsDict = shareRequestIds.ToDictionary(
            id => id,
            id => CreateInReviewShareRequest(id, editorId));

        _mockShareRequestRepository
            .Setup(r => r.GetByIdsForUpdateAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken ct) => shareRequestsDict);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.SuccessCount.Should().Be(shareRequestIds.Count);
        result.FailedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
        _mockShareRequestRepository.Verify(
            r => r.Update(It.IsAny<ShareRequest>()),
            Times.Exactly(shareRequestIds.Count));
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAllValidRequests_CreatesAuditLogForEach()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var reason = "Test rejection";
        var shareRequestIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var command = new BulkRejectShareRequestsCommand(shareRequestIds, editorId, reason);

        var shareRequestsDict = shareRequestIds.ToDictionary(
            id => id,
            id => CreateInReviewShareRequest(id, editorId));

        _mockShareRequestRepository
            .Setup(r => r.GetByIdsForUpdateAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken ct) => shareRequestsDict);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockAuditLogRepository.Verify(
            r => r.AddAsync(
                It.Is<Api.BoundedContexts.Administration.Domain.Entities.AuditLog>(a =>
                    a.UserId == editorId &&
                    a.Action == "share_request_rejected" &&
                    a.Resource == "ShareRequest" &&
                    a.Result == "success"),
                It.IsAny<CancellationToken>()),
            Times.Exactly(shareRequestIds.Count));
    }

    [Fact]
    public async Task Handle_WhenAnyRequestNotFound_RollsBackAllChanges()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var reason = "Rejection reason";
        var shareRequestIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var command = new BulkRejectShareRequestsCommand(shareRequestIds, editorId, reason);

        // Only first two exist in dictionary, third missing
        var shareRequestsDict = new Dictionary<Guid, ShareRequest>
        {
            { shareRequestIds[0], CreateInReviewShareRequest(shareRequestIds[0], editorId) },
            { shareRequestIds[1], CreateInReviewShareRequest(shareRequestIds[1], editorId) }
            // shareRequestIds[2] is missing - triggers rollback
        };

        _mockShareRequestRepository
            .Setup(r => r.GetByIdsForUpdateAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken ct) => shareRequestsDict);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - All-or-nothing failure
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(shareRequestIds.Count);
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("Bulk operation failed");
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never); // No commit on failure
    }

    [Fact]
    public async Task Handle_WhenDuplicateIds_RemovesDuplicatesAndProcessesDistinct()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var reason = "Duplicate test";
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var shareRequestIds = new List<Guid> { id1, id2, id1 }; // id1 duplicated
        var command = new BulkRejectShareRequestsCommand(shareRequestIds, editorId, reason);

        var shareRequestsDict = new Dictionary<Guid, ShareRequest>
        {
            { id1, CreateInReviewShareRequest(id1, editorId) },
            { id2, CreateInReviewShareRequest(id2, editorId) }
        };

        _mockShareRequestRepository
            .Setup(r => r.GetByIdsForUpdateAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken ct) => shareRequestsDict);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Only 2 distinct IDs processed
        result.SuccessCount.Should().Be(2);
        _mockShareRequestRepository.Verify(
            r => r.GetByIdsForUpdateAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()),
            Times.Once); // Batch method called once for all IDs
    }

    [Fact]
    public async Task Handle_WhenExceedsMaxLimit_ReturnsFailure()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var reason = "Too many";
        var shareRequestIds = Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToList();
        var command = new BulkRejectShareRequestsCommand(shareRequestIds, editorId, reason);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Handler catches DomainException and returns failure
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(shareRequestIds.Count);
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("maximum limit of 20");
    }

    [Fact]
    public async Task Handle_WhenInvalidState_RollsBackAllChanges()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var reason = "State test";
        var shareRequestIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var command = new BulkRejectShareRequestsCommand(shareRequestIds, editorId, reason);

        // First is InReview (correct), second is Pending (wrong state)
        var validRequest = CreateInReviewShareRequest(shareRequestIds[0], editorId);
        var invalidRequest = ShareRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ContributionType.NewGame,
            "Test"); // Status is Pending, not InReview

        _mockShareRequestRepository
            .Setup(r => r.GetByIdForUpdateAsync(shareRequestIds[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(validRequest);
        _mockShareRequestRepository
            .Setup(r => r.GetByIdForUpdateAsync(shareRequestIds[1], It.IsAny<CancellationToken>()))
            .ReturnsAsync(invalidRequest);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - All-or-nothing failure due to invalid state
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(shareRequestIds.Count);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ShareRequest CreateInReviewShareRequest(Guid id, Guid editorId)
    {
        var shareRequest = ShareRequest.Create(
            userId: Guid.NewGuid(),
            sourceGameId: Guid.NewGuid(),
            contributionType: ContributionType.NewGame,
            userNotes: "Test share request");

        // Transition to InReview status
        shareRequest.StartReview(editorId, lockDurationMinutes: 30);

        return shareRequest;
    }
}