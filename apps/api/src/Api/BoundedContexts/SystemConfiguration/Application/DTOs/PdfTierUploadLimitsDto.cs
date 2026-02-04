namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// DTO for PDF upload tier limits configuration.
/// Contains daily and weekly upload limits per subscription tier.
/// Issue #3333: PDF Upload Limits Configuration UI
/// </summary>
/// <param name="FreeDailyLimit">Free tier daily upload limit</param>
/// <param name="FreeWeeklyLimit">Free tier weekly upload limit</param>
/// <param name="NormalDailyLimit">Normal tier daily upload limit</param>
/// <param name="NormalWeeklyLimit">Normal tier weekly upload limit</param>
/// <param name="PremiumDailyLimit">Premium tier daily upload limit</param>
/// <param name="PremiumWeeklyLimit">Premium tier weekly upload limit</param>
/// <param name="LastUpdatedAt">When the configuration was last updated</param>
/// <param name="LastUpdatedByUserId">User ID who last updated the configuration (null if never updated)</param>
internal record PdfTierUploadLimitsDto(
    int FreeDailyLimit,
    int FreeWeeklyLimit,
    int NormalDailyLimit,
    int NormalWeeklyLimit,
    int PremiumDailyLimit,
    int PremiumWeeklyLimit,
    DateTime LastUpdatedAt,
    string? LastUpdatedByUserId
);

/// <summary>
/// Request DTO for updating PDF upload tier limits.
/// </summary>
internal record UpdatePdfTierUploadLimitsRequest(
    int FreeDailyLimit,
    int FreeWeeklyLimit,
    int NormalDailyLimit,
    int NormalWeeklyLimit,
    int PremiumDailyLimit,
    int PremiumWeeklyLimit
);
