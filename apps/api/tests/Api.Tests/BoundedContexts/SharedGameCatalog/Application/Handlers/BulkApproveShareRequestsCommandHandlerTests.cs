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

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Tests for BulkApproveShareRequestsCommandHandler.
/// Issue #2893: Verify all-or-nothing transaction and audit logging.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class BulkApproveShareRequestsCommandHandlerTests
{
    private readonly Mock<IShareRequestRepository> _mockShareRequestRepository;
    private readonly Mock<IAuditLogRepository> _mockAuditLogRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly BulkApproveShareRequestsCommandHandler _handler;

    public BulkApproveShareRequestsCommandHandlerTests()
    {
        _mockShareRequestRepository = new Mock<IShareRequestRepository>();
        _mockAuditLogRepository = new Mock<IAuditLogRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new BulkApproveShareRequestsCommandHandler(
            _mockShareRequestRepository.Object,
            _mockAuditLogRepository.Object,
            _mockUnitOfWork.Object,
            Mock.Of<ILogger<BulkApproveShareRequestsCommandHandler>>());
    }

    [Fact]
    public async Task Handle_WithAllValidRequests_ApprovesAllSuccessfully()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var shareRequestIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var command = new BulkApproveShareRequestsCommand(shareRequestIds, editorId);

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
        Assert.Equal(shareRequestIds.Count, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(result.Errors);
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
        var shareRequestIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var command = new BulkApproveShareRequestsCommand(shareRequestIds, editorId);

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
                    a.Action == "share_request_approved" &&
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
        var shareRequestIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var command = new BulkApproveShareRequestsCommand(shareRequestIds, editorId);

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
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(shareRequestIds.Count, result.FailedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Bulk operation failed", result.Errors[0]);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never); // No commit on failure
    }

    [Fact]
    public async Task Handle_WhenDuplicateIds_RemovesDuplicatesWithWarning()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var shareRequestIds = new List<Guid> { id1, id2, id1 }; // id1 duplicated
        var command = new BulkApproveShareRequestsCommand(shareRequestIds, editorId);

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
        Assert.Equal(2, result.SuccessCount);
        _mockShareRequestRepository.Verify(
            r => r.GetByIdsForUpdateAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()),
            Times.Once); // Batch method called once for all IDs
    }

    [Fact]
    public async Task Handle_WhenExceedsMaxLimit_ThrowsDomainException()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var shareRequestIds = Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToList();
        var command = new BulkApproveShareRequestsCommand(shareRequestIds, editorId);

        // Act & Assert
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Handler catches DomainException and returns failure result
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(shareRequestIds.Count, result.FailedCount);
        Assert.Single(result.Errors);
        Assert.Contains("maximum limit of 20", result.Errors[0]);
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
