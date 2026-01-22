using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Constants;

/// <summary>
/// Default rate limit configurations for each user tier.
/// These values are used when no custom configuration exists in the database.
/// </summary>
public static class DefaultRateLimitConfigs
{
    /// <summary>
    /// Default configuration for Free tier users.
    /// Conservative limits to prevent spam while allowing contribution.
    /// </summary>
    public static readonly RateLimitTierDefaults Free = new(
        Tier: UserTier.Free,
        MaxPendingRequests: 2,
        MaxRequestsPerMonth: 5,
        CooldownAfterRejection: TimeSpan.FromDays(7));

    /// <summary>
    /// Default configuration for Premium tier users.
    /// Enhanced limits for paying users.
    /// </summary>
    public static readonly RateLimitTierDefaults Premium = new(
        Tier: UserTier.Premium,
        MaxPendingRequests: 5,
        MaxRequestsPerMonth: 15,
        CooldownAfterRejection: TimeSpan.FromDays(3));

    /// <summary>
    /// Default configuration for Pro tier users.
    /// High limits for power contributors.
    /// </summary>
    public static readonly RateLimitTierDefaults Pro = new(
        Tier: UserTier.Pro,
        MaxPendingRequests: 10,
        MaxRequestsPerMonth: 30,
        CooldownAfterRejection: TimeSpan.FromDays(1));

    /// <summary>
    /// Default configuration for Admin users.
    /// Unlimited access - bypasses all rate limits.
    /// </summary>
    public static readonly RateLimitTierDefaults Admin = new(
        Tier: UserTier.Admin,
        MaxPendingRequests: int.MaxValue,
        MaxRequestsPerMonth: int.MaxValue,
        CooldownAfterRejection: TimeSpan.Zero);

    /// <summary>
    /// All tier defaults for iteration.
    /// </summary>
    private static readonly RateLimitTierDefaults[] _all = { Free, Premium, Pro, Admin };

    /// <summary>
    /// Gets all default tier configurations.
    /// </summary>
    public static IReadOnlyList<RateLimitTierDefaults> GetAll() => _all;

    /// <summary>
    /// Gets the default configuration for a specific tier.
    /// </summary>
    /// <param name="tier">The tier to get defaults for.</param>
    /// <returns>The default configuration for the tier.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when tier is unknown.</exception>
    public static RateLimitTierDefaults GetByTier(UserTier tier) => tier switch
    {
        UserTier.Free => Free,
        UserTier.Premium => Premium,
        UserTier.Pro => Pro,
        UserTier.Admin => Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown user tier")
    };

    /// <summary>
    /// Checks if a tier has unlimited access (bypasses all limits).
    /// </summary>
    /// <param name="tier">The tier to check.</param>
    /// <returns>True if the tier has unlimited access.</returns>
    public static bool HasUnlimitedAccess(UserTier tier) => tier == UserTier.Admin;
}

/// <summary>
/// Represents the default rate limit values for a user tier.
/// </summary>
/// <param name="Tier">The user tier these defaults apply to.</param>
/// <param name="MaxPendingRequests">Maximum simultaneous pending requests.</param>
/// <param name="MaxRequestsPerMonth">Maximum requests per calendar month.</param>
/// <param name="CooldownAfterRejection">Cooldown period after a rejection.</param>
public record RateLimitTierDefaults(
    UserTier Tier,
    int MaxPendingRequests,
    int MaxRequestsPerMonth,
    TimeSpan CooldownAfterRejection)
{
    /// <summary>
    /// Gets the cooldown period in days for display purposes.
    /// </summary>
    public int CooldownDays => (int)CooldownAfterRejection.TotalDays;

    /// <summary>
    /// Checks if this tier has unlimited access.
    /// </summary>
    public bool HasUnlimitedAccess => Tier == UserTier.Admin;

    /// <summary>
    /// Gets a human-readable description of this tier's limits.
    /// </summary>
    public string GetDescription()
    {
        if (HasUnlimitedAccess)
        {
            return "Unlimited access - no rate limits apply";
        }

        return $"Max {MaxPendingRequests} pending, {MaxRequestsPerMonth}/month, {CooldownDays} day cooldown";
    }
}
