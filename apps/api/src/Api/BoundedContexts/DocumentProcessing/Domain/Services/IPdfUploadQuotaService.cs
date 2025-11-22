using Api.BoundedContexts.Authentication.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Domain service for managing PDF upload quotas based on user tier.
/// </summary>
public interface IPdfUploadQuotaService
{
    /// <summary>
    /// Checks if a user can upload a PDF based on their tier limits.
    /// Admin and Editor roles bypass quota checks.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="userTier">User's subscription tier</param>
    /// <param name="userRole">User's role</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result with allowed status and quota information</returns>
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
