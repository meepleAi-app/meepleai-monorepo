using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.ValueObjects;

/// <summary>
/// Tests for the NotificationType value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class NotificationTypeTests
{
    #region Static Instance Tests

    [Fact]
    public void StaticInstances_HaveCorrectValues()
    {
        // Assert
        NotificationType.PdfUploadCompleted.Value.Should().Be("pdf_upload_completed");
        NotificationType.RuleSpecGenerated.Value.Should().Be("rule_spec_generated");
        NotificationType.ProcessingFailed.Value.Should().Be("processing_failed");
        NotificationType.NewComment.Value.Should().Be("new_comment");
        NotificationType.SharedLinkAccessed.Value.Should().Be("shared_link_accessed");
    }

    [Fact]
    public void ShareRequestTypes_HaveCorrectValues()
    {
        // Assert
        NotificationType.ShareRequestCreated.Value.Should().Be("share_request_created");
        NotificationType.ShareRequestApproved.Value.Should().Be("share_request_approved");
        NotificationType.ShareRequestRejected.Value.Should().Be("share_request_rejected");
        NotificationType.ShareRequestChangesRequested.Value.Should().Be("share_request_changes_requested");
    }

    [Fact]
    public void AdminTypes_HaveCorrectValues()
    {
        // Assert
        NotificationType.AdminNewShareRequest.Value.Should().Be("admin_new_share_request");
        NotificationType.AdminStaleShareRequests.Value.Should().Be("admin_stale_share_requests");
        NotificationType.AdminReviewLockExpiring.Value.Should().Be("admin_review_lock_expiring");
    }

    [Fact]
    public void RateLimitTypes_HaveCorrectValues()
    {
        // Assert
        NotificationType.RateLimitApproaching.Value.Should().Be("rate_limit_approaching");
        NotificationType.RateLimitReached.Value.Should().Be("rate_limit_reached");
        NotificationType.CooldownEnded.Value.Should().Be("cooldown_ended");
    }

    [Fact]
    public void OtherTypes_HaveCorrectValues()
    {
        // Assert
        NotificationType.BadgeEarned.Value.Should().Be("badge_earned");
        NotificationType.LoanReminder.Value.Should().Be("loan_reminder");
    }

    #endregion

    #region FromString Tests

    [Theory]
    [InlineData("pdf_upload_completed")]
    [InlineData("PDF_UPLOAD_COMPLETED")]
    [InlineData("Pdf_Upload_Completed")]
    public void FromString_WithPdfUploadCompleted_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationType.FromString(value);

        // Assert
        result.Should().Be(NotificationType.PdfUploadCompleted);
    }

    [Theory]
    [InlineData("rule_spec_generated")]
    [InlineData("RULE_SPEC_GENERATED")]
    public void FromString_WithRuleSpecGenerated_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationType.FromString(value);

        // Assert
        result.Should().Be(NotificationType.RuleSpecGenerated);
    }

    [Theory]
    [InlineData("processing_failed")]
    [InlineData("PROCESSING_FAILED")]
    public void FromString_WithProcessingFailed_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationType.FromString(value);

        // Assert
        result.Should().Be(NotificationType.ProcessingFailed);
    }

    [Fact]
    public void FromString_WithNewComment_ReturnsSameInstance()
    {
        // Act
        var result = NotificationType.FromString("new_comment");

        // Assert
        result.Should().Be(NotificationType.NewComment);
    }

    [Fact]
    public void FromString_WithSharedLinkAccessed_ReturnsSameInstance()
    {
        // Act
        var result = NotificationType.FromString("shared_link_accessed");

        // Assert
        result.Should().Be(NotificationType.SharedLinkAccessed);
    }

    [Fact]
    public void FromString_WithShareRequestTypes_ReturnsSameInstances()
    {
        // Act & Assert
        NotificationType.FromString("share_request_created").Should().Be(NotificationType.ShareRequestCreated);
        NotificationType.FromString("share_request_approved").Should().Be(NotificationType.ShareRequestApproved);
        NotificationType.FromString("share_request_rejected").Should().Be(NotificationType.ShareRequestRejected);
        NotificationType.FromString("share_request_changes_requested").Should().Be(NotificationType.ShareRequestChangesRequested);
    }

    [Fact]
    public void FromString_WithAdminTypes_ReturnsSameInstances()
    {
        // Act & Assert
        NotificationType.FromString("admin_new_share_request").Should().Be(NotificationType.AdminNewShareRequest);
        NotificationType.FromString("admin_stale_share_requests").Should().Be(NotificationType.AdminStaleShareRequests);
        NotificationType.FromString("admin_review_lock_expiring").Should().Be(NotificationType.AdminReviewLockExpiring);
    }

    [Fact]
    public void FromString_WithRateLimitTypes_ReturnsSameInstances()
    {
        // Act & Assert
        NotificationType.FromString("rate_limit_approaching").Should().Be(NotificationType.RateLimitApproaching);
        NotificationType.FromString("rate_limit_reached").Should().Be(NotificationType.RateLimitReached);
        NotificationType.FromString("cooldown_ended").Should().Be(NotificationType.CooldownEnded);
    }

    [Fact]
    public void FromString_WithBadgeEarned_ReturnsSameInstance()
    {
        // Act
        var result = NotificationType.FromString("badge_earned");

        // Assert
        result.Should().Be(NotificationType.BadgeEarned);
    }

    [Fact]
    public void FromString_WithLoanReminder_ReturnsSameInstance()
    {
        // Act
        var result = NotificationType.FromString("loan_reminder");

        // Assert
        result.Should().Be(NotificationType.LoanReminder);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("invalid_type")]
    [InlineData("")]
    public void FromString_WithUnknownType_ThrowsArgumentException(string value)
    {
        // Act
        var action = () => NotificationType.FromString(value);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage($"Unknown notification type: {value}*");
    }

    #endregion

    #region Boolean Property Tests

    [Fact]
    public void IsPdfUploadCompleted_WhenPdfUploadCompleted_ReturnsTrue()
    {
        // Assert
        NotificationType.PdfUploadCompleted.IsPdfUploadCompleted.Should().BeTrue();
    }

    [Fact]
    public void IsPdfUploadCompleted_WhenOtherType_ReturnsFalse()
    {
        // Assert
        NotificationType.ProcessingFailed.IsPdfUploadCompleted.Should().BeFalse();
        NotificationType.NewComment.IsPdfUploadCompleted.Should().BeFalse();
    }

    [Fact]
    public void IsRuleSpecGenerated_WhenRuleSpecGenerated_ReturnsTrue()
    {
        // Assert
        NotificationType.RuleSpecGenerated.IsRuleSpecGenerated.Should().BeTrue();
    }

    [Fact]
    public void IsRuleSpecGenerated_WhenOtherType_ReturnsFalse()
    {
        // Assert
        NotificationType.PdfUploadCompleted.IsRuleSpecGenerated.Should().BeFalse();
        NotificationType.ProcessingFailed.IsRuleSpecGenerated.Should().BeFalse();
    }

    [Fact]
    public void IsProcessingFailed_WhenProcessingFailed_ReturnsTrue()
    {
        // Assert
        NotificationType.ProcessingFailed.IsProcessingFailed.Should().BeTrue();
    }

    [Fact]
    public void IsProcessingFailed_WhenOtherType_ReturnsFalse()
    {
        // Assert
        NotificationType.PdfUploadCompleted.IsProcessingFailed.Should().BeFalse();
        NotificationType.RuleSpecGenerated.IsProcessingFailed.Should().BeFalse();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameType_ReturnsTrue()
    {
        // Arrange
        var type1 = NotificationType.PdfUploadCompleted;
        var type2 = NotificationType.PdfUploadCompleted;

        // Act & Assert
        type1.Should().Be(type2);
    }

    [Fact]
    public void Equals_WithDifferentTypes_ReturnsFalse()
    {
        // Arrange
        var type1 = NotificationType.PdfUploadCompleted;
        var type2 = NotificationType.ProcessingFailed;

        // Act & Assert
        type1.Should().NotBe(type2);
    }

    [Fact]
    public void GetHashCode_SameTypes_ReturnsSameHash()
    {
        // Arrange
        var type1 = NotificationType.PdfUploadCompleted;
        var type2 = NotificationType.PdfUploadCompleted;

        // Act & Assert
        type1.GetHashCode().Should().Be(type2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Assert
        NotificationType.PdfUploadCompleted.ToString().Should().Be("pdf_upload_completed");
        NotificationType.ProcessingFailed.ToString().Should().Be("processing_failed");
        NotificationType.BadgeEarned.ToString().Should().Be("badge_earned");
    }

    #endregion
}
