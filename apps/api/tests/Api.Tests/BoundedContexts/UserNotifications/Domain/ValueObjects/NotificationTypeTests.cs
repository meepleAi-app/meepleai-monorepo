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
        NotificationType.DocumentReady.Value.Should().Be("document_ready");
        NotificationType.RuleSpecGenerated.Value.Should().Be("rule_spec_generated");
        NotificationType.DocumentProcessingFailed.Value.Should().Be("document_processing_failed");
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
    [InlineData("document_ready")]
    [InlineData("DOCUMENT_READY")]
    public void FromString_WithDocumentReady_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationType.FromString(value);

        // Assert
        result.Should().Be(NotificationType.DocumentReady);
    }

    [Theory]
    [InlineData("pdf_upload_completed")]
    [InlineData("processing_job_completed")]
    public void FromString_WithLegacyDocumentReadyAliases_ReturnsDocumentReady(string value)
    {
        // Act
        var result = NotificationType.FromString(value);

        // Assert — legacy aliases map to DocumentReady
        result.Should().Be(NotificationType.DocumentReady);
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
    [InlineData("document_processing_failed")]
    [InlineData("DOCUMENT_PROCESSING_FAILED")]
    public void FromString_WithDocumentProcessingFailed_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationType.FromString(value);

        // Assert
        result.Should().Be(NotificationType.DocumentProcessingFailed);
    }

    [Theory]
    [InlineData("processing_failed")]
    [InlineData("processing_job_failed")]
    public void FromString_WithLegacyDocumentProcessingFailedAliases_ReturnsDocumentProcessingFailed(string value)
    {
        // Act
        var result = NotificationType.FromString(value);

        // Assert — legacy aliases map to DocumentProcessingFailed
        result.Should().Be(NotificationType.DocumentProcessingFailed);
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
    public void IsDocumentReady_WhenDocumentReady_ReturnsTrue()
    {
        // Assert
        NotificationType.DocumentReady.IsDocumentReady.Should().BeTrue();
    }

    [Fact]
    public void IsDocumentReady_WhenOtherType_ReturnsFalse()
    {
        // Assert
        NotificationType.DocumentProcessingFailed.IsDocumentReady.Should().BeFalse();
        NotificationType.SharedLinkAccessed.IsDocumentReady.Should().BeFalse();
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
        NotificationType.DocumentReady.IsRuleSpecGenerated.Should().BeFalse();
        NotificationType.DocumentProcessingFailed.IsRuleSpecGenerated.Should().BeFalse();
    }

    [Fact]
    public void IsDocumentProcessingFailed_WhenDocumentProcessingFailed_ReturnsTrue()
    {
        // Assert
        NotificationType.DocumentProcessingFailed.IsDocumentProcessingFailed.Should().BeTrue();
    }

    [Fact]
    public void IsDocumentProcessingFailed_WhenOtherType_ReturnsFalse()
    {
        // Assert
        NotificationType.DocumentReady.IsDocumentProcessingFailed.Should().BeFalse();
        NotificationType.RuleSpecGenerated.IsDocumentProcessingFailed.Should().BeFalse();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameType_ReturnsTrue()
    {
        // Arrange
        var type1 = NotificationType.DocumentReady;
        var type2 = NotificationType.DocumentReady;

        // Act & Assert
        type1.Should().Be(type2);
    }

    [Fact]
    public void Equals_WithDifferentTypes_ReturnsFalse()
    {
        // Arrange
        var type1 = NotificationType.DocumentReady;
        var type2 = NotificationType.DocumentProcessingFailed;

        // Act & Assert
        type1.Should().NotBe(type2);
    }

    [Fact]
    public void GetHashCode_SameTypes_ReturnsSameHash()
    {
        // Arrange
        var type1 = NotificationType.DocumentReady;
        var type2 = NotificationType.DocumentReady;

        // Act & Assert
        type1.GetHashCode().Should().Be(type2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Assert
        NotificationType.DocumentReady.ToString().Should().Be("document_ready");
        NotificationType.DocumentProcessingFailed.ToString().Should().Be("document_processing_failed");
        NotificationType.BadgeEarned.ToString().Should().Be("badge_earned");
    }

    #endregion
}
