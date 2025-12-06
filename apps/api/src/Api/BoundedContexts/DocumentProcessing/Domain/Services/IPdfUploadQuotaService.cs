using Api.BoundedContexts.Authentication.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Domain service for managing PDF upload quotas based on user tier.
///
/// <para><strong>Authorization Behavior:</strong></para>
/// <list type="bullet">
/// <item><description>Admin and Editor roles: <strong>Unlimited uploads</strong> (bypass all quota checks)</description></item>
/// <item><description>User role: Subject to tier-based daily and weekly limits</description></item>
/// </list>
///
/// <para><strong>Tier Limits (default values):</strong></para>
/// <list type="bullet">
/// <item><description>Free: 5 PDF/day, 20 PDF/week</description></item>
/// <item><description>Normal: 20 PDF/day, 100 PDF/week</description></item>
/// <item><description>Premium: 100 PDF/day, 500 PDF/week</description></item>
/// </list>
///
/// <para>Limits are configurable via SystemConfiguration with keys: UploadLimits:{tier}:{DailyLimit|WeeklyLimit}</para>
/// </summary>
public interface IPdfUploadQuotaService
{
    /// <summary>
    /// Checks if a user can upload a PDF based on their tier limits.
    ///
    /// <para><strong>Role-Based Bypass:</strong></para>
    /// <para>Admin and Editor roles automatically bypass quota checks and receive unlimited uploads.
    /// This is enforced at the service level for defense in depth, even if endpoint validation exists.</para>
    ///
    /// <para><strong>Quota Enforcement:</strong></para>
    /// <para>Regular users (User role) are subject to tier-based limits:
    /// - Checked against daily limit first (reset at midnight UTC)
    /// - Then checked against weekly limit (reset Monday midnight UTC)
    /// - Returns detailed quota information including usage, limits, and reset times</para>
    ///
    /// <para><strong>Fail-Open Pattern:</strong></para>
    /// <para>If Redis is unavailable, the check succeeds (allows upload) to prioritize availability over strict enforcement.</para>
    /// </summary>
    /// <param name="userId">User ID to check quota for</param>
    /// <param name="userTier">User's subscription tier (free/normal/premium)</param>
    /// <param name="userRole">User's role (determines if quota bypass applies)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result with allowed status, quota usage, limits, and reset times</returns>
    /// <exception cref="ArgumentNullException">Thrown if userTier or userRole is null</exception>
    Task<PdfUploadQuotaResult> CheckQuotaAsync(
        Guid userId,
        UserTier userTier,
        Role userRole,
        CancellationToken ct = default);

    /// <summary>
    /// Increments the upload count for a user.
    /// Should be called after a successful PDF upload.
    /// </summary>
    Task IncrementUploadCountAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets the remaining quota for a user.
    /// </summary>
    Task<PdfUploadQuotaInfo> GetQuotaInfoAsync(
        Guid userId,
        UserTier userTier,
        Role userRole,
        CancellationToken ct = default);

    /// <summary>
    /// Reserves quota for a PDF upload (Two-Phase Commit - Phase 1).
    /// Creates a temporary reservation that expires if not confirmed within TTL.
    /// </summary>
    /// <param name="userId">User ID reserving quota</param>
    /// <param name="pdfId">PDF document ID for tracking</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating if reservation was successful and expiration time</returns>
    Task<QuotaReservationResult> ReserveQuotaAsync(
        Guid userId,
        string pdfId,
        CancellationToken ct = default);

    /// <summary>
    /// Confirms a reserved quota after successful PDF processing (Two-Phase Commit - Phase 2).
    /// Makes the quota consumption permanent and removes the reservation.
    /// </summary>
    /// <param name="userId">User ID that reserved quota</param>
    /// <param name="pdfId">PDF document ID</param>
    /// <param name="ct">Cancellation token</param>
    Task ConfirmQuotaAsync(Guid userId, string pdfId, CancellationToken ct = default);

    /// <summary>
    /// Releases a reserved quota if PDF processing fails (Two-Phase Commit - Rollback).
    /// Decrements the upload count and removes the reservation.
    /// </summary>
    /// <param name="userId">User ID that reserved quota</param>
    /// <param name="pdfId">PDF document ID</param>
    /// <param name="ct">Cancellation token</param>
    Task ReleaseQuotaAsync(Guid userId, string pdfId, CancellationToken ct = default);
}

/// <summary>
/// Result of quota check operation.
/// </summary>
public record PdfUploadQuotaResult
{
    public bool Allowed { get; init; }
    public string? ErrorMessage { get; init; }
    public int DailyUploadsUsed { get; init; }
    public int DailyLimit { get; init; }
    public int WeeklyUploadsUsed { get; init; }
    public int WeeklyLimit { get; init; }
    public DateTime? DailyResetAt { get; init; }
    public DateTime? WeeklyResetAt { get; init; }

    public static PdfUploadQuotaResult Success(
        int dailyUsed,
        int dailyLimit,
        int weeklyUsed,
        int weeklyLimit,
        DateTime dailyReset,
        DateTime weeklyReset)
    {
        return new PdfUploadQuotaResult
        {
            Allowed = true,
            ErrorMessage = null,
            DailyUploadsUsed = dailyUsed,
            DailyLimit = dailyLimit,
            WeeklyUploadsUsed = weeklyUsed,
            WeeklyLimit = weeklyLimit,
            DailyResetAt = dailyReset,
            WeeklyResetAt = weeklyReset
        };
    }

    public static PdfUploadQuotaResult Denied(
        string errorMessage,
        int dailyUsed,
        int dailyLimit,
        int weeklyUsed,
        int weeklyLimit,
        DateTime dailyReset,
        DateTime weeklyReset)
    {
        return new PdfUploadQuotaResult
        {
            Allowed = false,
            ErrorMessage = errorMessage,
            DailyUploadsUsed = dailyUsed,
            DailyLimit = dailyLimit,
            WeeklyUploadsUsed = weeklyUsed,
            WeeklyLimit = weeklyLimit,
            DailyResetAt = dailyReset,
            WeeklyResetAt = weeklyReset
        };
    }
}

/// <summary>
/// Information about user's current quota status.
/// </summary>
public record PdfUploadQuotaInfo
{
    public int DailyUploadsUsed { get; init; }
    public int DailyLimit { get; init; }
    public int DailyRemaining { get; init; }
    public int WeeklyUploadsUsed { get; init; }
    public int WeeklyLimit { get; init; }
    public int WeeklyRemaining { get; init; }
    public DateTime DailyResetAt { get; init; }
    public DateTime WeeklyResetAt { get; init; }
    public bool IsUnlimited { get; init; }
}

/// <summary>
/// Result of quota reservation operation (Two-Phase Commit Phase 1).
/// </summary>
public record QuotaReservationResult
{
    public bool Reserved { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? ExpiresAt { get; init; }

    public static QuotaReservationResult Success(DateTime expiresAt)
    {
        return new QuotaReservationResult
        {
            Reserved = true,
            ErrorMessage = null,
            ExpiresAt = expiresAt
        };
    }

    public static QuotaReservationResult Failed(string errorMessage)
    {
        return new QuotaReservationResult
        {
            Reserved = false,
            ErrorMessage = errorMessage,
            ExpiresAt = null
        };
    }
}
