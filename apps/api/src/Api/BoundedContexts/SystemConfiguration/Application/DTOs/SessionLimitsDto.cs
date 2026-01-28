#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs

namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// Response DTO for session tier limits configuration.
/// Issue #3070: Session limits backend implementation.
/// </summary>
/// <param name="FreeTierLimit">Maximum active sessions for free tier (default: 3)</param>
/// <param name="NormalTierLimit">Maximum active sessions for normal tier (default: 10)</param>
/// <param name="PremiumTierLimit">Maximum active sessions for premium tier (-1 = unlimited)</param>
/// <param name="LastUpdatedAt">When limits were last updated</param>
/// <param name="LastUpdatedByUserId">User who last updated the limits</param>
internal record SessionLimitsDto(
    int FreeTierLimit,
    int NormalTierLimit,
    int PremiumTierLimit,
    DateTime LastUpdatedAt,
    string? LastUpdatedByUserId
);

/// <summary>
/// Request DTO for updating session tier limits.
/// </summary>
/// <param name="FreeTierLimit">Maximum active sessions for free tier (must be >= 1)</param>
/// <param name="NormalTierLimit">Maximum active sessions for normal tier (must be >= 1)</param>
/// <param name="PremiumTierLimit">Maximum active sessions for premium tier (-1 = unlimited, or >= 1)</param>
internal record UpdateSessionLimitsRequest(
    int FreeTierLimit,
    int NormalTierLimit,
    int PremiumTierLimit
);

/// <summary>
/// Response DTO for user session quota status.
/// </summary>
/// <param name="CurrentSessions">Number of active sessions for the user</param>
/// <param name="MaxSessions">Maximum allowed sessions for user's tier (-1 = unlimited)</param>
/// <param name="RemainingSlots">Number of sessions user can still create (-1 = unlimited)</param>
/// <param name="CanCreateNew">Whether user can create a new session</param>
/// <param name="IsUnlimited">Whether user has unlimited sessions (premium tier or admin/editor role)</param>
/// <param name="UserTier">User's current subscription tier</param>
internal record SessionQuotaDto(
    int CurrentSessions,
    int MaxSessions,
    int RemainingSlots,
    bool CanCreateNew,
    bool IsUnlimited,
    string UserTier
);
