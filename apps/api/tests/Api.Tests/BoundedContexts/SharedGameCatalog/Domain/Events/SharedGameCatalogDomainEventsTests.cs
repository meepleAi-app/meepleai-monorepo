using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Tests for SharedGameCatalog domain events.
/// Issue #3025: Backend 90% Coverage Target - Phase 26
/// </summary>
[Trait("Category", "Unit")]
public sealed class SharedGameCatalogDomainEventsTests
{
    #region SharedGameCreatedEvent Tests

    [Fact]
    public void SharedGameCreatedEvent_SetsAllProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var title = "Catan";

        // Act
        var evt = new SharedGameCreatedEvent(gameId, title, createdBy);

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.Title.Should().Be(title);
        evt.CreatedBy.Should().Be(createdBy);
    }

    #endregion

    #region SharedGameUpdatedEvent Tests

    [Fact]
    public void SharedGameUpdatedEvent_SetsAllProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var modifiedBy = Guid.NewGuid();

        // Act
        var evt = new SharedGameUpdatedEvent(gameId, modifiedBy);

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.ModifiedBy.Should().Be(modifiedBy);
    }

    #endregion

    #region SharedGameArchivedEvent Tests

    [Fact]
    public void SharedGameArchivedEvent_SetsAllProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var archivedBy = Guid.NewGuid();

        // Act
        var evt = new SharedGameArchivedEvent(gameId, archivedBy);

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.ArchivedBy.Should().Be(archivedBy);
    }

    #endregion

    #region SharedGameDocumentAddedEvent Tests

    [Fact]
    public void SharedGameDocumentAddedEvent_SetsAllProperties()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var documentType = SharedGameDocumentType.Rulebook;
        var version = "1.0";
        var createdBy = Guid.NewGuid();

        // Act
        var evt = new SharedGameDocumentAddedEvent(
            sharedGameId,
            documentId,
            pdfDocumentId,
            documentType,
            version,
            createdBy);

        // Assert
        evt.SharedGameId.Should().Be(sharedGameId);
        evt.DocumentId.Should().Be(documentId);
        evt.PdfDocumentId.Should().Be(pdfDocumentId);
        evt.DocumentType.Should().Be(documentType);
        evt.Version.Should().Be(version);
        evt.CreatedBy.Should().Be(createdBy);
    }

    [Fact]
    public void SharedGameDocumentAddedEvent_WithErrataType_SetsCorrectType()
    {
        // Act
        var evt = new SharedGameDocumentAddedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            SharedGameDocumentType.Errata,
            "2.0",
            Guid.NewGuid());

        // Assert
        evt.DocumentType.Should().Be(SharedGameDocumentType.Errata);
    }

    #endregion

    #region BadgeEarnedEvent Tests

    [Fact]
    public void BadgeEarnedEvent_SetsAllProperties()
    {
        // Arrange
        var userBadgeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var badgeId = Guid.NewGuid();
        var badgeCode = "FIRST_CONTRIBUTION";
        var earnedAt = DateTime.UtcNow.AddHours(-1);

        // Act
        var evt = new BadgeEarnedEvent(userBadgeId, userId, badgeId, badgeCode, earnedAt);

        // Assert
        evt.UserBadgeId.Should().Be(userBadgeId);
        evt.UserId.Should().Be(userId);
        evt.BadgeId.Should().Be(badgeId);
        evt.BadgeCode.Should().Be(badgeCode);
        evt.EarnedAt.Should().Be(earnedAt);
    }

    #endregion

    #region BadgeRevokedEvent Tests

    [Fact]
    public void BadgeRevokedEvent_SetsAllProperties()
    {
        // Arrange
        var userBadgeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var badgeId = Guid.NewGuid();
        var reason = "Badge requirements no longer met";

        // Act
        var evt = new BadgeRevokedEvent(userBadgeId, userId, badgeId, reason);

        // Assert
        evt.UserBadgeId.Should().Be(userBadgeId);
        evt.UserId.Should().Be(userId);
        evt.BadgeId.Should().Be(badgeId);
        evt.Reason.Should().Be(reason);
    }

    #endregion

    #region ContributorAddedEvent Tests

    [Fact]
    public void ContributorAddedEvent_WithPrimaryContributor_SetsProperties()
    {
        // Arrange
        var contributorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        // Act
        var evt = new ContributorAddedEvent(contributorId, userId, sharedGameId, isPrimary: true);

        // Assert
        evt.ContributorId.Should().Be(contributorId);
        evt.UserId.Should().Be(userId);
        evt.SharedGameId.Should().Be(sharedGameId);
        evt.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void ContributorAddedEvent_WithSecondaryContributor_SetsProperties()
    {
        // Act
        var evt = new ContributorAddedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            isPrimary: false);

        // Assert
        evt.IsPrimary.Should().BeFalse();
    }

    #endregion

    #region ContributionRecordedEvent Tests

    [Fact]
    public void ContributionRecordedEvent_SetsAllProperties()
    {
        // Arrange
        var contributorId = Guid.NewGuid();
        var contributionId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var type = ContributionRecordType.InitialSubmission;
        var version = 1;

        // Act
        var evt = new ContributionRecordedEvent(
            contributorId,
            contributionId,
            sharedGameId,
            type,
            version);

        // Assert
        evt.ContributorId.Should().Be(contributorId);
        evt.ContributionId.Should().Be(contributionId);
        evt.SharedGameId.Should().Be(sharedGameId);
        evt.Type.Should().Be(type);
        evt.Version.Should().Be(version);
    }

    [Theory]
    [InlineData(ContributionRecordType.InitialSubmission)]
    [InlineData(ContributionRecordType.DocumentAddition)]
    [InlineData(ContributionRecordType.MetadataUpdate)]
    [InlineData(ContributionRecordType.ContentEnhancement)]
    public void ContributionRecordedEvent_WithDifferentTypes_SetsCorrectType(ContributionRecordType type)
    {
        // Act
        var evt = new ContributionRecordedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            type,
            version: 1);

        // Assert
        evt.Type.Should().Be(type);
    }

    #endregion

    #region ShareRequestCreatedEvent Tests

    [Fact]
    public void ShareRequestCreatedEvent_SetsAllProperties()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sourceGameId = Guid.NewGuid();
        var contributionType = ContributionType.NewGame;

        // Act
        var evt = new ShareRequestCreatedEvent(
            shareRequestId,
            userId,
            sourceGameId,
            contributionType);

        // Assert
        evt.ShareRequestId.Should().Be(shareRequestId);
        evt.UserId.Should().Be(userId);
        evt.SourceGameId.Should().Be(sourceGameId);
        evt.ContributionType.Should().Be(contributionType);
    }

    [Fact]
    public void ShareRequestCreatedEvent_WithAdditionalContent_SetsCorrectType()
    {
        // Act
        var evt = new ShareRequestCreatedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ContributionType.AdditionalContent);

        // Assert
        evt.ContributionType.Should().Be(ContributionType.AdditionalContent);
    }

    #endregion

    #region ShareRequestReviewStartedEvent Tests

    [Fact]
    public void ShareRequestReviewStartedEvent_SetsProperties()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        // Act
        var evt = new ShareRequestReviewStartedEvent(shareRequestId, adminId);

        // Assert
        evt.ShareRequestId.Should().Be(shareRequestId);
        evt.AdminId.Should().Be(adminId);
    }

    #endregion

    #region ShareRequestReviewReleasedEvent Tests

    [Fact]
    public void ShareRequestReviewReleasedEvent_SetsProperties()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        // Act
        var evt = new ShareRequestReviewReleasedEvent(shareRequestId, adminId);

        // Assert
        evt.ShareRequestId.Should().Be(shareRequestId);
        evt.AdminId.Should().Be(adminId);
    }

    #endregion

    #region ShareRequestApprovedEvent Tests

    [Fact]
    public void ShareRequestApprovedEvent_WithTargetGame_SetsProperties()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var targetGameId = Guid.NewGuid();

        // Act
        var evt = new ShareRequestApprovedEvent(shareRequestId, adminId, targetGameId);

        // Assert
        evt.ShareRequestId.Should().Be(shareRequestId);
        evt.AdminId.Should().Be(adminId);
        evt.TargetSharedGameId.Should().Be(targetGameId);
    }

    [Fact]
    public void ShareRequestApprovedEvent_WithoutTargetGame_SetsNullTargetId()
    {
        // Act
        var evt = new ShareRequestApprovedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            targetSharedGameId: null);

        // Assert
        evt.TargetSharedGameId.Should().BeNull();
    }

    #endregion

    #region ShareRequestRejectedEvent Tests

    [Fact]
    public void ShareRequestRejectedEvent_SetsAllProperties()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var reason = "Content does not meet quality standards";

        // Act
        var evt = new ShareRequestRejectedEvent(shareRequestId, adminId, reason);

        // Assert
        evt.ShareRequestId.Should().Be(shareRequestId);
        evt.AdminId.Should().Be(adminId);
        evt.Reason.Should().Be(reason);
    }

    #endregion

    #region ShareRequestChangesRequestedEvent Tests

    [Fact]
    public void ShareRequestChangesRequestedEvent_SetsAllProperties()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var feedback = "Please improve image quality and add player count";

        // Act
        var evt = new ShareRequestChangesRequestedEvent(shareRequestId, adminId, feedback);

        // Assert
        evt.ShareRequestId.Should().Be(shareRequestId);
        evt.AdminId.Should().Be(adminId);
        evt.Feedback.Should().Be(feedback);
    }

    #endregion

    #region ShareRequestResubmittedEvent Tests

    [Fact]
    public void ShareRequestResubmittedEvent_SetsShareRequestId()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();

        // Act
        var evt = new ShareRequestResubmittedEvent(shareRequestId);

        // Assert
        evt.ShareRequestId.Should().Be(shareRequestId);
    }

    #endregion

    #region ShareRequestWithdrawnEvent Tests

    [Fact]
    public void ShareRequestWithdrawnEvent_SetsShareRequestId()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();

        // Act
        var evt = new ShareRequestWithdrawnEvent(shareRequestId);

        // Assert
        evt.ShareRequestId.Should().Be(shareRequestId);
    }

    #endregion

    #region ShareRequestLockExpiredEvent Tests

    [Fact]
    public void ShareRequestLockExpiredEvent_SetsAllProperties()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var expiredAt = DateTime.UtcNow.AddMinutes(-5);
        var returnedToStatus = ShareRequestStatus.Pending;

        // Act
        var evt = new ShareRequestLockExpiredEvent(
            shareRequestId,
            adminId,
            expiredAt,
            returnedToStatus);

        // Assert
        evt.ShareRequestId.Should().Be(shareRequestId);
        evt.AdminId.Should().Be(adminId);
        evt.ExpiredAt.Should().Be(expiredAt);
        evt.ReturnedToStatus.Should().Be(returnedToStatus);
    }

    [Fact]
    public void ShareRequestLockExpiredEvent_WithChangesRequestedReturn_SetsCorrectStatus()
    {
        // Act
        var evt = new ShareRequestLockExpiredEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            ShareRequestStatus.ChangesRequested);

        // Assert
        evt.ReturnedToStatus.Should().Be(ShareRequestStatus.ChangesRequested);
    }

    #endregion

    #region ShareRequestLockExtendedEvent Tests

    [Fact]
    public void ShareRequestLockExtendedEvent_SetsAllProperties()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var newExpirationTime = DateTime.UtcNow.AddMinutes(30);

        // Act
        var evt = new ShareRequestLockExtendedEvent(
            shareRequestId,
            adminId,
            newExpirationTime);

        // Assert
        evt.ShareRequestId.Should().Be(shareRequestId);
        evt.AdminId.Should().Be(adminId);
        evt.NewExpirationTime.Should().Be(newExpirationTime);
    }

    #endregion
}