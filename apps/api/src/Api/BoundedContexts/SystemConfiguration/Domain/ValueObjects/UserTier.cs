namespace Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// User tier for rate limiting configuration in the Game Sharing system.
/// Different tiers have different rate limits for share requests.
/// </summary>
public enum UserTier
{
    /// <summary>
    /// Free tier users with basic limits.
    /// Max 2 pending requests, 5 per month, 7 day cooldown after rejection.
    /// </summary>
    Free = 0,

    /// <summary>
    /// Premium tier users with enhanced limits.
    /// Max 5 pending requests, 15 per month, 3 day cooldown after rejection.
    /// </summary>
    Premium = 1,

    /// <summary>
    /// Pro tier users with high limits.
    /// Max 10 pending requests, 30 per month, 1 day cooldown after rejection.
    /// </summary>
    Pro = 2,

    /// <summary>
    /// Admin users with unlimited access.
    /// No rate limits apply.
    /// </summary>
    Admin = 3
}
