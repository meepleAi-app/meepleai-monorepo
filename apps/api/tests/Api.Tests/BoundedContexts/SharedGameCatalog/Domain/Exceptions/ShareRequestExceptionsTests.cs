using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Exceptions;

/// <summary>
/// Unit tests for ShareRequest domain exceptions.
/// </summary>
public class ShareRequestExceptionsTests
{
    #region InvalidShareRequestStateException Tests

    [Fact]
    public void InvalidShareRequestStateException_ContainsCorrectProperties()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var currentStatus = ShareRequestStatus.InReview;
        var attemptedOperation = "withdraw";
        var allowedStatuses = new[] { ShareRequestStatus.Pending, ShareRequestStatus.ChangesRequested };

        // Act
        var exception = new InvalidShareRequestStateException(
            shareRequestId,
            currentStatus,
            attemptedOperation,
            allowedStatuses);

        // Assert
        exception.ShareRequestId.Should().Be(shareRequestId);
        exception.CurrentStatus.Should().Be(currentStatus);
        exception.AttemptedOperation.Should().Be(attemptedOperation);
        exception.AllowedStatuses.Should().BeEquivalentTo(allowedStatuses);
    }

    [Fact]
    public void InvalidShareRequestStateException_MessageContainsAllowedStatuses()
    {
        // Arrange
        var exception = new InvalidShareRequestStateException(
            Guid.NewGuid(),
            ShareRequestStatus.Approved,
            "start review",
            ShareRequestStatus.Pending,
            ShareRequestStatus.ChangesRequested);

        // Act & Assert
        exception.Message.Should().Contain("start review");
        exception.Message.Should().Contain("Approved");
        exception.Message.Should().Contain("Pending");
        exception.Message.Should().Contain("ChangesRequested");
    }

    [Fact]
    public void InvalidShareRequestStateException_WithSingleAllowedStatus_FormatsCorrectly()
    {
        // Arrange
        var exception = new InvalidShareRequestStateException(
            Guid.NewGuid(),
            ShareRequestStatus.Pending,
            "expire lock",
            ShareRequestStatus.InReview);

        // Act & Assert
        exception.Message.Should().Contain("expire lock");
        exception.Message.Should().Contain("Pending");
        exception.Message.Should().Contain("InReview");
    }

    #endregion

    #region ShareRequestAlreadyInReviewException Tests

    [Fact]
    public void ShareRequestAlreadyInReviewException_ContainsCorrectProperties()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var currentReviewerAdminId = Guid.NewGuid();
        var reviewStartedAt = DateTime.UtcNow.AddMinutes(-15);

        // Act
        var exception = new ShareRequestAlreadyInReviewException(
            shareRequestId,
            currentReviewerAdminId,
            reviewStartedAt);

        // Assert
        exception.ShareRequestId.Should().Be(shareRequestId);
        exception.CurrentReviewerAdminId.Should().Be(currentReviewerAdminId);
        exception.ReviewStartedAt.Should().Be(reviewStartedAt);
    }

    [Fact]
    public void ShareRequestAlreadyInReviewException_MessageContainsAdminInfo()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var currentReviewerAdminId = Guid.NewGuid();

        // Act
        var exception = new ShareRequestAlreadyInReviewException(
            shareRequestId,
            currentReviewerAdminId,
            DateTime.UtcNow);

        // Assert
        exception.Message.Should().Contain(shareRequestId.ToString());
        exception.Message.Should().Contain(currentReviewerAdminId.ToString());
    }

    #endregion

    #region ShareRequestReviewerMismatchException Tests

    [Fact]
    public void ShareRequestReviewerMismatchException_ContainsCorrectProperties()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var expectedAdminId = Guid.NewGuid();
        var actualAdminId = Guid.NewGuid();

        // Act
        var exception = new ShareRequestReviewerMismatchException(
            shareRequestId,
            expectedAdminId,
            actualAdminId);

        // Assert
        exception.ShareRequestId.Should().Be(shareRequestId);
        exception.ExpectedAdminId.Should().Be(expectedAdminId);
        exception.ActualAdminId.Should().Be(actualAdminId);
    }

    [Fact]
    public void ShareRequestReviewerMismatchException_MessageContainsBothAdminIds()
    {
        // Arrange
        var expectedAdminId = Guid.NewGuid();
        var actualAdminId = Guid.NewGuid();

        // Act
        var exception = new ShareRequestReviewerMismatchException(
            Guid.NewGuid(),
            expectedAdminId,
            actualAdminId);

        // Assert
        exception.Message.Should().Contain(expectedAdminId.ToString());
        exception.Message.Should().Contain(actualAdminId.ToString());
    }

    #endregion

    #region ShareRequestLockExpiredException Tests

    [Fact]
    public void ShareRequestLockExpiredException_ContainsCorrectProperties()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var lockExpiredAt = DateTime.UtcNow.AddMinutes(-5);

        // Act
        var exception = new ShareRequestLockExpiredException(
            shareRequestId,
            adminId,
            lockExpiredAt);

        // Assert
        exception.ShareRequestId.Should().Be(shareRequestId);
        exception.AdminId.Should().Be(adminId);
        exception.LockExpiredAt.Should().Be(lockExpiredAt);
    }

    [Fact]
    public void ShareRequestLockExpiredException_MessageContainsExpirationInfo()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        // Act
        var exception = new ShareRequestLockExpiredException(
            shareRequestId,
            adminId,
            DateTime.UtcNow);

        // Assert
        exception.Message.Should().Contain(shareRequestId.ToString());
        exception.Message.Should().Contain(adminId.ToString());
        exception.Message.Should().Contain("expired");
    }

    #endregion

    #region ShareRequestDocumentLimitExceededException Tests

    [Fact]
    public void ShareRequestDocumentLimitExceededException_ContainsCorrectProperties()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var currentCount = 10;
        var maxAllowed = 10;

        // Act
        var exception = new ShareRequestDocumentLimitExceededException(
            shareRequestId,
            currentCount,
            maxAllowed);

        // Assert
        exception.ShareRequestId.Should().Be(shareRequestId);
        exception.CurrentCount.Should().Be(currentCount);
        exception.MaxAllowed.Should().Be(maxAllowed);
    }

    [Fact]
    public void ShareRequestDocumentLimitExceededException_MessageContainsLimitInfo()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();

        // Act
        var exception = new ShareRequestDocumentLimitExceededException(
            shareRequestId,
            currentCount: 10,
            maxAllowed: 10);

        // Assert
        exception.Message.Should().Contain(shareRequestId.ToString());
        exception.Message.Should().Contain("10");
    }

    #endregion
}
