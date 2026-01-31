using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Tests for SharedGameCatalog domain events.
/// Issue #3025: Backend 90% Coverage Target - Phase 21 PR#4
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

    [Fact]
    public void SharedGameCreatedEvent_WithLongTitle_PreservesFullTitle()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var title = "Twilight Imperium: Fourth Edition - Prophecy of Kings Expansion";
        var createdBy = Guid.NewGuid();

        // Act
        var evt = new SharedGameCreatedEvent(gameId, title, createdBy);

        // Assert
        evt.Title.Should().Be(title);
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

    [Fact]
    public void SharedGameUpdatedEvent_WithDifferentModifiers_TracksDifferentEditors()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        // Act
        var evt1 = new SharedGameUpdatedEvent(gameId, user1);
        var evt2 = new SharedGameUpdatedEvent(gameId, user2);

        // Assert
        evt1.ModifiedBy.Should().Be(user1);
        evt2.ModifiedBy.Should().Be(user2);
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

    [Fact]
    public void SharedGameArchivedEvent_WithDifferentAdmins_TracksDifferentArchivers()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var admin1 = Guid.NewGuid();
        var admin2 = Guid.NewGuid();

        // Act
        var evt1 = new SharedGameArchivedEvent(gameId, admin1);
        var evt2 = new SharedGameArchivedEvent(gameId, admin2);

        // Assert
        evt1.ArchivedBy.Should().Be(admin1);
        evt2.ArchivedBy.Should().Be(admin2);
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

    [Fact]
    public void BadgeEarnedEvent_WithDifferentBadgeCodes_SetsCorrectly()
    {
        // Arrange
        var userBadgeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var badgeId = Guid.NewGuid();
        var earnedAt = DateTime.UtcNow;

        // Act
        var evt1 = new BadgeEarnedEvent(userBadgeId, userId, badgeId, "POWER_CONTRIBUTOR", earnedAt);
        var evt2 = new BadgeEarnedEvent(userBadgeId, userId, badgeId, "QUALITY_REVIEWER", earnedAt);

        // Assert
        evt1.BadgeCode.Should().Be("POWER_CONTRIBUTOR");
        evt2.BadgeCode.Should().Be("QUALITY_REVIEWER");
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

    [Fact]
    public void BadgeRevokedEvent_WithDetailedReason_PreservesReason()
    {
        // Arrange
        var userBadgeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var badgeId = Guid.NewGuid();
        var reason = "Multiple reports of low-quality contributions leading to community concerns";

        // Act
        var evt = new BadgeRevokedEvent(userBadgeId, userId, badgeId, reason);

        // Assert
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

    [Fact]
    public void ShareRequestReviewStartedEvent_WithDifferentAdmins_TracksDifferentReviewers()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var admin1 = Guid.NewGuid();
        var admin2 = Guid.NewGuid();

        // Act
        var evt1 = new ShareRequestReviewStartedEvent(shareRequestId, admin1);
        var evt2 = new ShareRequestReviewStartedEvent(shareRequestId, admin2);

        // Assert
        evt1.AdminId.Should().Be(admin1);
        evt2.AdminId.Should().Be(admin2);
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

    [Fact]
    public void ShareRequestReviewReleasedEvent_WithDifferentReviews_TracksCorrectly()
    {
        // Arrange
        var shareRequestId1 = Guid.NewGuid();
        var shareRequestId2 = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        // Act
        var evt1 = new ShareRequestReviewReleasedEvent(shareRequestId1, adminId);
        var evt2 = new ShareRequestReviewReleasedEvent(shareRequestId2, adminId);

        // Assert
        evt1.ShareRequestId.Should().Be(shareRequestId1);
        evt2.ShareRequestId.Should().Be(shareRequestId2);
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

    [Fact]
    public void ShareRequestRejectedEvent_WithDetailedReason_PreservesFullReason()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var reason = "Insufficient documentation quality: missing player count, unclear rule descriptions, and no setup instructions";

        // Act
        var evt = new ShareRequestRejectedEvent(shareRequestId, adminId, reason);

        // Assert
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

    [Fact]
    public void ShareRequestChangesRequestedEvent_WithDetailedFeedback_PreservesFullFeedback()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var feedback = "1. Update player count (currently missing)\n2. Add setup instructions\n3. Clarify win conditions";

        // Act
        var evt = new ShareRequestChangesRequestedEvent(shareRequestId, adminId, feedback);

        // Assert
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

    [Fact]
    public void ShareRequestResubmittedEvent_WithDifferentRequests_TracksCorrectly()
    {
        // Arrange
        var shareRequestId1 = Guid.NewGuid();
        var shareRequestId2 = Guid.NewGuid();

        // Act
        var evt1 = new ShareRequestResubmittedEvent(shareRequestId1);
        var evt2 = new ShareRequestResubmittedEvent(shareRequestId2);

        // Assert
        evt1.ShareRequestId.Should().Be(shareRequestId1);
        evt2.ShareRequestId.Should().Be(shareRequestId2);
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

    [Fact]
    public void ShareRequestWithdrawnEvent_WithDifferentRequests_TracksCorrectly()
    {
        // Arrange
        var shareRequestId1 = Guid.NewGuid();
        var shareRequestId2 = Guid.NewGuid();

        // Act
        var evt1 = new ShareRequestWithdrawnEvent(shareRequestId1);
        var evt2 = new ShareRequestWithdrawnEvent(shareRequestId2);

        // Assert
        evt1.ShareRequestId.Should().Be(shareRequestId1);
        evt2.ShareRequestId.Should().Be(shareRequestId2);
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

    [Fact]
    public void ShareRequestLockExtendedEvent_WithFutureExpiration_SetsCorrectTime()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var newExpirationTime = DateTime.UtcNow.AddHours(2);

        // Act
        var evt = new ShareRequestLockExtendedEvent(shareRequestId, adminId, newExpirationTime);

        // Assert
        evt.NewExpirationTime.Should().BeAfter(DateTime.UtcNow);
    }

    #endregion

    #region GameErrataAddedEvent Tests

    [Fact]
    public void GameErrataAddedEvent_WithValidParameters_SetsProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var errataId = Guid.NewGuid();
        var description = "Corrected misprinted card text in rule book";

        // Act
        var evt = new GameErrataAddedEvent(gameId, errataId, description);

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.ErrataId.Should().Be(errataId);
        evt.Description.Should().Be(description);
    }

    [Fact]
    public void GameErrataAddedEvent_WithDetailedDescription_PreservesFullDescription()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var errataId = Guid.NewGuid();
        var description = "Page 15, paragraph 3: 'Draw 2 cards' should read 'Draw 3 cards'. This affects the resource gathering phase.";

        // Act
        var evt = new GameErrataAddedEvent(gameId, errataId, description);

        // Assert
        evt.Description.Should().Be(description);
    }

    #endregion

    #region GameFaqAddedEvent Tests

    [Fact]
    public void GameFaqAddedEvent_WithValidParameters_SetsProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var faqId = Guid.NewGuid();
        var question = "Can I trade resources with other players?";

        // Act
        var evt = new GameFaqAddedEvent(gameId, faqId, question);

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.FaqId.Should().Be(faqId);
        evt.Question.Should().Be(question);
    }

    [Fact]
    public void GameFaqAddedEvent_WithDetailedQuestion_PreservesFullQuestion()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var faqId = Guid.NewGuid();
        var question = "If I play a special action card during another player's turn, do I still get to take my regular turn afterward?";

        // Act
        var evt = new GameFaqAddedEvent(gameId, faqId, question);

        // Assert
        evt.Question.Should().Be(question);
    }

    #endregion

    #region SharedGameDeletedEvent Tests

    [Fact]
    public void SharedGameDeletedEvent_WithValidParameters_SetsProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var deletedBy = Guid.NewGuid();

        // Act
        var evt = new SharedGameDeletedEvent(gameId, deletedBy);

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.DeletedBy.Should().Be(deletedBy);
    }

    [Fact]
    public void SharedGameDeletedEvent_WithDifferentAdmins_TracksDifferentDeleters()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var admin1 = Guid.NewGuid();
        var admin2 = Guid.NewGuid();

        // Act
        var evt1 = new SharedGameDeletedEvent(gameId, admin1);
        var evt2 = new SharedGameDeletedEvent(gameId, admin2);

        // Assert
        evt1.DeletedBy.Should().Be(admin1);
        evt2.DeletedBy.Should().Be(admin2);
    }

    #endregion

    #region SharedGameDeleteRequestedEvent Tests

    [Fact]
    public void SharedGameDeleteRequestedEvent_WithValidParameters_SetsProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var requestedBy = Guid.NewGuid();

        // Act
        var evt = new SharedGameDeleteRequestedEvent(gameId, requestId, requestedBy);

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.RequestId.Should().Be(requestId);
        evt.RequestedBy.Should().Be(requestedBy);
    }

    [Fact]
    public void SharedGameDeleteRequestedEvent_WithDifferentRequests_TracksCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var requestId1 = Guid.NewGuid();
        var requestId2 = Guid.NewGuid();
        var requestedBy = Guid.NewGuid();

        // Act
        var evt1 = new SharedGameDeleteRequestedEvent(gameId, requestId1, requestedBy);
        var evt2 = new SharedGameDeleteRequestedEvent(gameId, requestId2, requestedBy);

        // Assert
        evt1.RequestId.Should().Be(requestId1);
        evt2.RequestId.Should().Be(requestId2);
    }

    #endregion

    #region SharedGamePublicationApprovedEvent Tests

    [Fact]
    public void SharedGamePublicationApprovedEvent_WithValidParameters_SetsProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var approvedBy = Guid.NewGuid();

        // Act
        var evt = new SharedGamePublicationApprovedEvent(gameId, approvedBy);

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.ApprovedBy.Should().Be(approvedBy);
    }

    [Fact]
    public void SharedGamePublicationApprovedEvent_WithDifferentAdmins_TracksDifferentApprovers()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var admin1 = Guid.NewGuid();
        var admin2 = Guid.NewGuid();

        // Act
        var evt1 = new SharedGamePublicationApprovedEvent(gameId, admin1);
        var evt2 = new SharedGamePublicationApprovedEvent(gameId, admin2);

        // Assert
        evt1.ApprovedBy.Should().Be(admin1);
        evt2.ApprovedBy.Should().Be(admin2);
    }

    #endregion

    #region SharedGamePublicationRejectedEvent Tests

    [Fact]
    public void SharedGamePublicationRejectedEvent_WithValidParameters_SetsProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var rejectedBy = Guid.NewGuid();
        var reason = "Incomplete documentation";

        // Act
        var evt = new SharedGamePublicationRejectedEvent(gameId, rejectedBy, reason);

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.RejectedBy.Should().Be(rejectedBy);
        evt.Reason.Should().Be(reason);
    }

    [Fact]
    public void SharedGamePublicationRejectedEvent_WithDetailedReason_PreservesFullReason()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var rejectedBy = Guid.NewGuid();
        var reason = "Missing critical information: player count, age range, and setup instructions are incomplete";

        // Act
        var evt = new SharedGamePublicationRejectedEvent(gameId, rejectedBy, reason);

        // Assert
        evt.Reason.Should().Be(reason);
    }

    #endregion

    #region SharedGameSubmittedForApprovalEvent Tests

    [Fact]
    public void SharedGameSubmittedForApprovalEvent_WithValidParameters_SetsProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var submittedBy = Guid.NewGuid();

        // Act
        var evt = new SharedGameSubmittedForApprovalEvent(gameId, submittedBy);

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.SubmittedBy.Should().Be(submittedBy);
    }

    [Fact]
    public void SharedGameSubmittedForApprovalEvent_WithDifferentUsers_TracksDifferentSubmitters()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        // Act
        var evt1 = new SharedGameSubmittedForApprovalEvent(gameId, user1);
        var evt2 = new SharedGameSubmittedForApprovalEvent(gameId, user2);

        // Assert
        evt1.SubmittedBy.Should().Be(user1);
        evt2.SubmittedBy.Should().Be(user2);
    }

    #endregion
}
