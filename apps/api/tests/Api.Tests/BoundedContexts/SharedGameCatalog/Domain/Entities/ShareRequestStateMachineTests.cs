using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Unit tests for ShareRequest state machine enhancements.
/// Tests Issue #2721: ShareRequest State Machine Enhancement
/// </summary>
public class ShareRequestStateMachineTests
{
    #region StartReview Tests

    [Fact]
    public void StartReview_FromPending_TransitionsToInReview()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var adminId = Guid.NewGuid();

        // Act
        request.StartReview(adminId);

        // Assert
        request.Status.Should().Be(ShareRequestStatus.InReview);
        request.ReviewingAdminId.Should().Be(adminId);
        request.ReviewStartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        request.StatusBeforeReview.Should().Be(ShareRequestStatus.Pending);
    }

    [Fact]
    public void StartReview_FromChangesRequested_TransitionsToInReview()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var adminId = Guid.NewGuid();
        request.StartReview(adminId);
        request.RequestChanges(adminId, "Please update the description");
        request.Resubmit();

        // Move back to ChangesRequested
        request.StartReview(adminId);
        request.RequestChanges(adminId, "Need more details");

        var newAdminId = Guid.NewGuid();

        // Act
        request.StartReview(newAdminId);

        // Assert
        request.Status.Should().Be(ShareRequestStatus.InReview);
        request.StatusBeforeReview.Should().Be(ShareRequestStatus.ChangesRequested);
    }

    [Fact]
    public void StartReview_SetsLockExpirationTime()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var adminId = Guid.NewGuid();

        // Act
        request.StartReview(adminId, lockDurationMinutes: 45);

        // Assert
        request.ReviewLockExpiresAt.Should().NotBeNull();
        request.ReviewLockExpiresAt.Should().BeCloseTo(
            DateTime.UtcNow.AddMinutes(45),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StartReview_UsesDefaultLockDuration()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var adminId = Guid.NewGuid();

        // Act
        request.StartReview(adminId);

        // Assert
        request.ReviewLockExpiresAt.Should().BeCloseTo(
            DateTime.UtcNow.AddMinutes(ShareRequest.DefaultLockDurationMinutes),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StartReview_WithEmptyAdminId_ThrowsArgumentException()
    {
        // Arrange
        var request = CreateTestShareRequest();

        // Act
        var act = () => request.StartReview(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("adminId")
            .WithMessage("*AdminId cannot be empty*");
    }

    [Fact]
    public void StartReview_WhenNotPendingOrChangesRequested_ThrowsInvalidShareRequestStateException()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var adminId = Guid.NewGuid();
        request.StartReview(adminId);

        // Act
        var act = () => request.StartReview(Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidShareRequestStateException>()
            .Where(e => e.CurrentStatus == ShareRequestStatus.InReview);
    }

    [Fact]
    public void StartReview_WhenReviewerAlreadySet_ThrowsShareRequestAlreadyInReviewException()
    {
        // Arrange - Create a request with reviewer set but status is still Pending
        // This simulates a race condition scenario
        var userId = Guid.NewGuid();
        var sourceGameId = Guid.NewGuid();
        var existingReviewerId = Guid.NewGuid();
        var reviewStartedAt = DateTime.UtcNow.AddMinutes(-5);

        // Use internal constructor to create inconsistent state (for race condition testing)
        var request = new ShareRequest(
            Guid.NewGuid(),
            userId,
            sourceGameId,
            null,
            ShareRequestStatus.Pending, // Status is reviewable
            null,
            ContributionType.NewGame,
            null,
            null,
            existingReviewerId, // But reviewer is already set (race condition)
            reviewStartedAt,
            DateTime.UtcNow.AddMinutes(25),
            null,
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(-5),
            userId,
            existingReviewerId);

        // Act
        var act = () => request.StartReview(Guid.NewGuid());

        // Assert
        act.Should().Throw<ShareRequestAlreadyInReviewException>()
            .Where(e => e.CurrentReviewerAdminId == existingReviewerId);
    }

    [Fact]
    public void StartReview_RaisesShareRequestReviewStartedEvent()
    {
        // Arrange
        var request = CreateTestShareRequest();
        request.ClearDomainEvents();
        var adminId = Guid.NewGuid();

        // Act
        request.StartReview(adminId);

        // Assert
        var evt = request.DomainEvents.OfType<ShareRequestReviewStartedEvent>().Single();
        evt.ShareRequestId.Should().Be(request.Id);
        evt.AdminId.Should().Be(adminId);
    }

    #endregion

    #region ReleaseReview Tests

    [Fact]
    public void ReleaseReview_FromPendingOrigin_ReturnsToPending()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var adminId = Guid.NewGuid();
        request.StartReview(adminId);

        // Act
        request.ReleaseReview();

        // Assert
        request.Status.Should().Be(ShareRequestStatus.Pending);
        request.StatusBeforeReview.Should().BeNull();
        request.ReviewingAdminId.Should().BeNull();
        request.ReviewStartedAt.Should().BeNull();
        request.ReviewLockExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ReleaseReview_FromChangesRequestedOrigin_ReturnsToChangesRequested()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var adminId = Guid.NewGuid();

        // First review cycle - request changes
        request.StartReview(adminId);
        request.RequestChanges(adminId, "Need updates");

        // Start new review from ChangesRequested state
        request.StartReview(adminId);

        // Act
        request.ReleaseReview();

        // Assert
        request.Status.Should().Be(ShareRequestStatus.ChangesRequested);
        request.StatusBeforeReview.Should().BeNull();
    }

    [Fact]
    public void ReleaseReview_WhenNotInReview_ThrowsInvalidShareRequestStateException()
    {
        // Arrange
        var request = CreateTestShareRequest();

        // Act
        var act = () => request.ReleaseReview();

        // Assert
        act.Should().Throw<InvalidShareRequestStateException>()
            .Where(e => e.CurrentStatus == ShareRequestStatus.Pending
                && e.AttemptedOperation == "release review");
    }

    [Fact]
    public void ReleaseReview_RaisesShareRequestReviewReleasedEvent()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var adminId = Guid.NewGuid();
        request.StartReview(adminId);
        request.ClearDomainEvents();

        // Act
        request.ReleaseReview();

        // Assert
        var evt = request.DomainEvents.OfType<ShareRequestReviewReleasedEvent>().Single();
        evt.ShareRequestId.Should().Be(request.Id);
        evt.AdminId.Should().Be(adminId);
    }

    #endregion

    #region ExtendReviewLock Tests

    [Fact]
    public void ExtendReviewLock_ExtendsLockExpiration()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var adminId = Guid.NewGuid();
        request.StartReview(adminId, lockDurationMinutes: 10);
        var originalExpiration = request.ReviewLockExpiresAt;

        // Act
        request.ExtendReviewLock(adminId, additionalMinutes: 30);

        // Assert
        request.ReviewLockExpiresAt.Should().BeCloseTo(
            DateTime.UtcNow.AddMinutes(30),
            TimeSpan.FromSeconds(5));
        request.ReviewLockExpiresAt.Should().BeAfter(originalExpiration!.Value);
    }

    [Fact]
    public void ExtendReviewLock_WithEmptyAdminId_ThrowsArgumentException()
    {
        // Arrange
        var request = CreateTestShareRequest();
        request.StartReview(Guid.NewGuid());

        // Act
        var act = () => request.ExtendReviewLock(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("adminId");
    }

    [Fact]
    public void ExtendReviewLock_WhenNotInReview_ThrowsInvalidShareRequestStateException()
    {
        // Arrange
        var request = CreateTestShareRequest();

        // Act
        var act = () => request.ExtendReviewLock(Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidShareRequestStateException>()
            .Where(e => e.AttemptedOperation == "extend review lock");
    }

    [Fact]
    public void ExtendReviewLock_ByDifferentAdmin_ThrowsShareRequestReviewerMismatchException()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var originalAdmin = Guid.NewGuid();
        var differentAdmin = Guid.NewGuid();
        request.StartReview(originalAdmin);

        // Act
        var act = () => request.ExtendReviewLock(differentAdmin);

        // Assert
        act.Should().Throw<ShareRequestReviewerMismatchException>()
            .Where(e => e.ExpectedAdminId == originalAdmin
                && e.ActualAdminId == differentAdmin);
    }

    [Fact]
    public void ExtendReviewLock_RaisesShareRequestLockExtendedEvent()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var adminId = Guid.NewGuid();
        request.StartReview(adminId);
        request.ClearDomainEvents();

        // Act
        request.ExtendReviewLock(adminId, 45);

        // Assert
        var evt = request.DomainEvents.OfType<ShareRequestLockExtendedEvent>().Single();
        evt.ShareRequestId.Should().Be(request.Id);
        evt.AdminId.Should().Be(adminId);
        evt.NewExpirationTime.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(45), TimeSpan.FromSeconds(5));
    }

    #endregion

    #region ExpireLock Tests

    [Fact]
    public void ExpireLock_FromPendingOrigin_ReturnsToPending()
    {
        // Arrange
        var request = CreateExpiredReviewRequest(ShareRequestStatus.Pending);

        // Act
        request.ExpireLock();

        // Assert
        request.Status.Should().Be(ShareRequestStatus.Pending);
        request.ReviewingAdminId.Should().BeNull();
        request.ReviewStartedAt.Should().BeNull();
        request.ReviewLockExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ExpireLock_FromChangesRequestedOrigin_ReturnsToChangesRequested()
    {
        // Arrange
        var request = CreateExpiredReviewRequest(ShareRequestStatus.ChangesRequested);

        // Act
        request.ExpireLock();

        // Assert
        request.Status.Should().Be(ShareRequestStatus.ChangesRequested);
    }

    [Fact]
    public void ExpireLock_WhenNotInReview_ThrowsInvalidShareRequestStateException()
    {
        // Arrange
        var request = CreateTestShareRequest();

        // Act
        var act = () => request.ExpireLock();

        // Assert
        act.Should().Throw<InvalidShareRequestStateException>()
            .Where(e => e.AttemptedOperation == "expire lock");
    }

    [Fact]
    public void ExpireLock_WhenLockNotExpired_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = CreateTestShareRequest();
        request.StartReview(Guid.NewGuid(), lockDurationMinutes: 60);

        // Act
        var act = () => request.ExpireLock();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not yet reached its expiration*");
    }

    [Fact]
    public void ExpireLock_RaisesShareRequestLockExpiredEvent()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var request = CreateExpiredReviewRequest(ShareRequestStatus.Pending, adminId);
        request.ClearDomainEvents();

        // Act
        request.ExpireLock();

        // Assert
        var evt = request.DomainEvents.OfType<ShareRequestLockExpiredEvent>().Single();
        evt.ShareRequestId.Should().Be(request.Id);
        evt.AdminId.Should().Be(adminId);
        evt.ReturnedToStatus.Should().Be(ShareRequestStatus.Pending);
    }

    #endregion

    #region IsLockExpired Tests

    [Fact]
    public void IsLockExpired_WhenNotInReview_ReturnsFalse()
    {
        // Arrange
        var request = CreateTestShareRequest();

        // Act & Assert
        request.IsLockExpired().Should().BeFalse();
    }

    [Fact]
    public void IsLockExpired_WhenLockNotExpired_ReturnsFalse()
    {
        // Arrange
        var request = CreateTestShareRequest();
        request.StartReview(Guid.NewGuid(), lockDurationMinutes: 60);

        // Act & Assert
        request.IsLockExpired().Should().BeFalse();
    }

    [Fact]
    public void IsLockExpired_WhenLockExpired_ReturnsTrue()
    {
        // Arrange
        var request = CreateExpiredReviewRequest(ShareRequestStatus.Pending);

        // Act & Assert
        request.IsLockExpired().Should().BeTrue();
    }

    #endregion

    #region Approve with Lock Expiration Tests

    [Fact]
    public void Approve_WhenLockExpired_ThrowsShareRequestLockExpiredException()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var request = CreateExpiredReviewRequest(ShareRequestStatus.Pending, adminId);

        // Act
        var act = () => request.Approve(adminId);

        // Assert
        act.Should().Throw<ShareRequestLockExpiredException>()
            .Where(e => e.AdminId == adminId);
    }

    [Fact]
    public void Approve_ByDifferentAdmin_ThrowsShareRequestReviewerMismatchException()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var originalAdmin = Guid.NewGuid();
        var differentAdmin = Guid.NewGuid();
        request.StartReview(originalAdmin);

        // Act
        var act = () => request.Approve(differentAdmin);

        // Assert
        act.Should().Throw<ShareRequestReviewerMismatchException>();
    }

    [Fact]
    public void Approve_ClearsLockExpiration()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var adminId = Guid.NewGuid();
        request.StartReview(adminId);

        // Act
        request.Approve(adminId, Guid.NewGuid());

        // Assert
        request.ReviewLockExpiresAt.Should().BeNull();
        request.StatusBeforeReview.Should().BeNull();
    }

    #endregion

    #region Reject with Lock Expiration Tests

    [Fact]
    public void Reject_WhenLockExpired_ThrowsShareRequestLockExpiredException()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var request = CreateExpiredReviewRequest(ShareRequestStatus.Pending, adminId);

        // Act
        var act = () => request.Reject(adminId, "Reason");

        // Assert
        act.Should().Throw<ShareRequestLockExpiredException>();
    }

    [Fact]
    public void Reject_ByDifferentAdmin_ThrowsShareRequestReviewerMismatchException()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var originalAdmin = Guid.NewGuid();
        var differentAdmin = Guid.NewGuid();
        request.StartReview(originalAdmin);

        // Act
        var act = () => request.Reject(differentAdmin, "Reason");

        // Assert
        act.Should().Throw<ShareRequestReviewerMismatchException>();
    }

    #endregion

    #region RequestChanges with Lock Expiration Tests

    [Fact]
    public void RequestChanges_WhenLockExpired_ThrowsShareRequestLockExpiredException()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var request = CreateExpiredReviewRequest(ShareRequestStatus.Pending, adminId);

        // Act
        var act = () => request.RequestChanges(adminId, "Feedback");

        // Assert
        act.Should().Throw<ShareRequestLockExpiredException>();
    }

    [Fact]
    public void RequestChanges_ByDifferentAdmin_ThrowsShareRequestReviewerMismatchException()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var originalAdmin = Guid.NewGuid();
        var differentAdmin = Guid.NewGuid();
        request.StartReview(originalAdmin);

        // Act
        var act = () => request.RequestChanges(differentAdmin, "Feedback");

        // Assert
        act.Should().Throw<ShareRequestReviewerMismatchException>();
    }

    #endregion

    #region GetRemainingLockTime Tests

    [Fact]
    public void GetRemainingLockTime_WhenNotInReview_ReturnsNull()
    {
        // Arrange
        var request = CreateTestShareRequest();

        // Act & Assert
        request.GetRemainingLockTime().Should().BeNull();
    }

    [Fact]
    public void GetRemainingLockTime_WhenLockExpired_ReturnsNull()
    {
        // Arrange
        var request = CreateExpiredReviewRequest(ShareRequestStatus.Pending);

        // Act & Assert
        request.GetRemainingLockTime().Should().BeNull();
    }

    [Fact]
    public void GetRemainingLockTime_WhenLockActive_ReturnsRemainingTime()
    {
        // Arrange
        var request = CreateTestShareRequest();
        request.StartReview(Guid.NewGuid(), lockDurationMinutes: 30);

        // Act
        var remaining = request.GetRemainingLockTime();

        // Assert
        remaining.Should().NotBeNull();
        remaining!.Value.Should().BeCloseTo(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Document Limit Tests

    [Fact]
    public void AttachDocument_WhenAtLimit_ThrowsShareRequestDocumentLimitExceededException()
    {
        // Arrange
        var request = CreateTestShareRequest();
        for (int i = 0; i < ShareRequest.MaxDocumentCount; i++)
        {
            request.AttachDocument(Guid.NewGuid(), $"doc{i}.pdf", "application/pdf", 1000);
        }

        // Act
        var act = () => request.AttachDocument(Guid.NewGuid(), "extra.pdf", "application/pdf", 1000);

        // Assert
        act.Should().Throw<ShareRequestDocumentLimitExceededException>()
            .Where(e => e.CurrentCount == ShareRequest.MaxDocumentCount
                && e.MaxAllowed == ShareRequest.MaxDocumentCount);
    }

    [Fact]
    public void AttachDocument_WhenBelowLimit_Succeeds()
    {
        // Arrange
        var request = CreateTestShareRequest();
        for (int i = 0; i < ShareRequest.MaxDocumentCount - 1; i++)
        {
            request.AttachDocument(Guid.NewGuid(), $"doc{i}.pdf", "application/pdf", 1000);
        }

        // Act
        request.AttachDocument(Guid.NewGuid(), "last.pdf", "application/pdf", 1000);

        // Assert
        request.AttachedDocuments.Should().HaveCount(ShareRequest.MaxDocumentCount);
    }

    #endregion

    #region Withdraw with Domain Exceptions Tests

    [Fact]
    public void Withdraw_WhenInReview_ThrowsInvalidShareRequestStateException()
    {
        // Arrange
        var request = CreateTestShareRequest();
        request.StartReview(Guid.NewGuid());

        // Act
        var act = () => request.Withdraw();

        // Assert
        act.Should().Throw<InvalidShareRequestStateException>()
            .Where(e => e.CurrentStatus == ShareRequestStatus.InReview
                && e.AttemptedOperation == "withdraw");
    }

    [Fact]
    public void Withdraw_WhenApproved_ThrowsInvalidShareRequestStateException()
    {
        // Arrange
        var request = CreateTestShareRequest();
        var adminId = Guid.NewGuid();
        request.StartReview(adminId);
        request.Approve(adminId, Guid.NewGuid());

        // Act
        var act = () => request.Withdraw();

        // Assert
        act.Should().Throw<InvalidShareRequestStateException>()
            .Where(e => e.CurrentStatus == ShareRequestStatus.Approved);
    }

    #endregion

    #region Resubmit with Domain Exceptions Tests

    [Fact]
    public void Resubmit_WhenPending_ThrowsInvalidShareRequestStateException()
    {
        // Arrange
        var request = CreateTestShareRequest();

        // Act
        var act = () => request.Resubmit();

        // Assert
        act.Should().Throw<InvalidShareRequestStateException>()
            .Where(e => e.CurrentStatus == ShareRequestStatus.Pending
                && e.AttemptedOperation == "resubmit");
    }

    #endregion

    #region RowVersion Tests

    [Fact]
    public void Create_InitializesWithNullRowVersion()
    {
        // Arrange & Act
        var request = CreateTestShareRequest();

        // Assert
        request.RowVersion.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private static ShareRequest CreateTestShareRequest()
    {
        return ShareRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ContributionType.NewGame,
            "Test notes");
    }

    private static ShareRequest CreateExpiredReviewRequest(
        ShareRequestStatus originStatus,
        Guid? adminId = null)
    {
        var userId = Guid.NewGuid();
        var sourceGameId = Guid.NewGuid();
        var reviewerId = adminId ?? Guid.NewGuid();

        // Create a request with an already-expired lock using internal constructor
        return new ShareRequest(
            Guid.NewGuid(),
            userId,
            sourceGameId,
            null,
            ShareRequestStatus.InReview,
            originStatus,
            ContributionType.NewGame,
            null,
            null,
            reviewerId,
            DateTime.UtcNow.AddMinutes(-35),
            DateTime.UtcNow.AddMinutes(-5), // Lock expired 5 minutes ago
            null,
            DateTime.UtcNow.AddMinutes(-60),
            DateTime.UtcNow.AddMinutes(-35),
            userId,
            reviewerId);
    }

    #endregion
}
