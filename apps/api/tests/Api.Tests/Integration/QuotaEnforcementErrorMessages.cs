using FluentAssertions;

namespace Api.Tests.Integration;

/// <summary>
/// Expected error messages for PDF upload quota enforcement.
/// Defined as constants to avoid magic strings in test assertions (Issue #1757).
///
/// IMPORTANT: These constants represent the API contract for quota error messages.
/// Changes to these messages should be coordinated with frontend/API consumers.
/// </summary>
public static class QuotaEnforcementErrorMessages
{
    /// <summary>
    /// Error when daily upload limit is reached.
    /// Source: PdfUploadQuotaService.cs
    /// </summary>
    public const string DailyLimitReached = "Daily upload limit reached";

    /// <summary>
    /// Error when weekly upload limit is reached.
    /// Source: PdfUploadQuotaService.cs
    /// </summary>
    public const string WeeklyLimitReached = "Weekly upload limit reached";
    /// <summary>
    /// Free tier identifier in quota messages.
    /// </summary>
    public const string FreeTier = "free tier";

    /// <summary>
    /// Normal tier identifier in quota messages.
    /// </summary>
    public const string NormalTier = "normal tier";

    /// <summary>
    /// Premium tier identifier in quota messages.
    /// </summary>
    public const string PremiumTier = "premium tier";
    /// <summary>
    /// Free tier daily limit description.
    /// </summary>
    public const string FreeTierDailyLimit = "5 PDF/day";

    /// <summary>
    /// Free tier weekly limit description.
    /// </summary>
    public const string FreeTierWeeklyLimit = "20 PDF/week";

    /// <summary>
    /// Normal tier daily limit description.
    /// </summary>
    public const string NormalTierDailyLimit = "20 PDF/day";

    /// <summary>
    /// Normal tier weekly limit description.
    /// </summary>
    public const string NormalTierWeeklyLimit = "100 PDF/week";

    /// <summary>
    /// Premium tier daily limit description.
    /// </summary>
    public const string PremiumTierDailyLimit = "100 PDF/day";
}

/// <summary>
/// Helper extension methods for FluentAssertions to validate quota enforcement error messages.
/// Provides specific, non-magic-string assertions that catch regressions (Issue #1757).
/// </summary>
public static class QuotaEnforcementAssertionExtensions
{
    /// <summary>
    /// Asserts that the message indicates daily upload limit has been reached.
    /// </summary>
    public static void ShouldIndicateDailyLimitReached(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Contain(QuotaEnforcementErrorMessages.DailyLimitReached, because);
    }

    /// <summary>
    /// Asserts that the message indicates weekly upload limit has been reached.
    /// </summary>
    public static void ShouldIndicateWeeklyLimitReached(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Contain(QuotaEnforcementErrorMessages.WeeklyLimitReached, because);
    }

    /// <summary>
    /// Asserts that the message indicates either daily or weekly limit reached.
    /// Useful for tests that may hit either limit depending on timing.
    /// </summary>
    public static void ShouldIndicateQuotaLimitReached(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Match(m =>
            m.Contains(QuotaEnforcementErrorMessages.DailyLimitReached, StringComparison.OrdinalIgnoreCase) ||
            m.Contains(QuotaEnforcementErrorMessages.WeeklyLimitReached, StringComparison.OrdinalIgnoreCase),
            because.Length > 0 ? because : "should indicate quota limit (daily or weekly) reached");
    }

    /// <summary>
    /// Asserts that the message contains the free tier identifier.
    /// </summary>
    public static void ShouldIndicateFreeTier(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Contain(QuotaEnforcementErrorMessages.FreeTier, because);
    }

    /// <summary>
    /// Asserts that the message contains the normal tier identifier.
    /// </summary>
    public static void ShouldIndicateNormalTier(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Contain(QuotaEnforcementErrorMessages.NormalTier, because);
    }

    /// <summary>
    /// Asserts that the message contains the premium tier identifier.
    /// </summary>
    public static void ShouldIndicatePremiumTier(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Contain(QuotaEnforcementErrorMessages.PremiumTier, because);
    }

    /// <summary>
    /// Asserts that the message contains the free tier daily limit (5 PDF/day).
    /// </summary>
    public static void ShouldIndicateFreeTierDailyLimit(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Contain(QuotaEnforcementErrorMessages.FreeTierDailyLimit, because);
    }

    /// <summary>
    /// Asserts that the message contains the free tier weekly limit (20 PDF/week).
    /// </summary>
    public static void ShouldIndicateFreeTierWeeklyLimit(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Contain(QuotaEnforcementErrorMessages.FreeTierWeeklyLimit, because);
    }

    /// <summary>
    /// Asserts that the message contains either free tier daily or weekly limit.
    /// </summary>
    public static void ShouldIndicateFreeTierLimit(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Match(m =>
            m.Contains(QuotaEnforcementErrorMessages.FreeTierDailyLimit, StringComparison.OrdinalIgnoreCase) ||
            m.Contains(QuotaEnforcementErrorMessages.FreeTierWeeklyLimit, StringComparison.OrdinalIgnoreCase),
            because.Length > 0 ? because : "should indicate free tier limit (daily or weekly)");
    }

    /// <summary>
    /// Asserts that the message contains the normal tier daily limit (20 PDF/day).
    /// </summary>
    public static void ShouldIndicateNormalTierDailyLimit(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Contain(QuotaEnforcementErrorMessages.NormalTierDailyLimit, because);
    }

    /// <summary>
    /// Asserts that the message contains the normal tier weekly limit (100 PDF/week).
    /// </summary>
    public static void ShouldIndicateNormalTierWeeklyLimit(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Contain(QuotaEnforcementErrorMessages.NormalTierWeeklyLimit, because);
    }

    /// <summary>
    /// Asserts that the message contains either normal tier daily or weekly limit.
    /// </summary>
    public static void ShouldIndicateNormalTierLimit(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Match(m =>
            m.Contains(QuotaEnforcementErrorMessages.NormalTierDailyLimit, StringComparison.OrdinalIgnoreCase) ||
            m.Contains(QuotaEnforcementErrorMessages.NormalTierWeeklyLimit, StringComparison.OrdinalIgnoreCase),
            because.Length > 0 ? because : "should indicate normal tier limit (daily or weekly)");
    }

    /// <summary>
    /// Asserts that the message contains the premium tier daily limit (100 PDF/day).
    /// </summary>
    public static void ShouldIndicatePremiumTierDailyLimit(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Contain(QuotaEnforcementErrorMessages.PremiumTierDailyLimit, because);
    }
}
