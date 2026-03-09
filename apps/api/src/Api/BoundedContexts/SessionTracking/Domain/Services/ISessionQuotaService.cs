using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.SessionTracking.Domain.Services;

/// <summary>
/// Domain service for managing concurrent session quotas based on user tier.
///
/// <para><strong>Authorization Behavior:</strong></para>
/// <list type="bullet">
/// <item><description>Admin and Editor roles: <strong>Unlimited sessions</strong> (bypass all quota checks)</description></item>
/// <item><description>User role: Subject to tier-based session limits</description></item>
/// </list>
///
/// <para><strong>Tier Limits (default values):</strong></para>
/// <list type="bullet">
/// <item><description>Free: 3 concurrent sessions max</description></item>
/// <item><description>Normal: 10 concurrent sessions max</description></item>
/// <item><description>Premium: Unlimited concurrent sessions</description></item>
/// </list>
///
/// <para>Limits are configurable via SystemConfiguration with keys: SessionLimits:{tier}:MaxSessions</para>
/// </summary>
public interface ISessionQuotaService
{
    /// <summary>
    /// Checks if a user can create a new game session based on tier limits.
    ///
    /// <para><strong>Role-Based Bypass:</strong></para>
    /// <para>Admin and Editor roles automatically bypass quota checks and have unlimited sessions.
    /// This is enforced at the service level for defense in depth, even if endpoint validation exists.</para>
    ///
    /// <para><strong>Quota Enforcement:</strong></para>
    /// <para>Regular users (User role) are subject to tier-based limits:
    /// - Free tier: 3 concurrent sessions maximum
    /// - Normal tier: 10 concurrent sessions maximum
    /// - Premium tier: Unlimited concurrent sessions</para>
    /// </summary>
    /// <param name="userId">User ID to check quota for</param>
    /// <param name="userTier">User's subscription tier (free/normal/premium)</param>
    /// <param name="userRole">User's role (determines if quota bypass applies)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with allowed status, current count, and max allowed</returns>
    /// <exception cref="ArgumentNullException">Thrown if userTier or userRole is null</exception>
    Task<SessionQuotaResult> CheckQuotaAsync(
        Guid userId,
        UserTier userTier,
        Role userRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed quota information for a user's concurrent sessions.
    /// </summary>
    /// <param name="userId">User ID to get quota info for</param>
    /// <param name="userTier">User's subscription tier</param>
    /// <param name="userRole">User's role</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed quota information including active sessions, max allowed, and remaining slots</returns>
    Task<SessionQuotaInfo> GetQuotaInfoAsync(
        Guid userId,
        UserTier userTier,
        Role userRole,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of session quota check operation.
/// </summary>
public record SessionQuotaResult
{
    /// <summary>
    /// Whether the user is allowed to create a new session.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Reason for denial, null if allowed.
    /// </summary>
    public string? DenialReason { get; init; }

    /// <summary>
    /// Current number of active sessions for the user.
    /// </summary>
    public int CurrentCount { get; init; }

    /// <summary>
    /// Maximum number of concurrent sessions allowed for this user/tier.
    /// </summary>
    public int MaxAllowed { get; init; }

    /// <summary>
    /// Creates a successful quota check result.
    /// </summary>
    public static SessionQuotaResult Allowed(int currentCount, int maxAllowed)
    {
        return new SessionQuotaResult
        {
            IsAllowed = true,
            DenialReason = null,
            CurrentCount = currentCount,
            MaxAllowed = maxAllowed
        };
    }

    /// <summary>
    /// Creates a denied quota check result.
    /// </summary>
    public static SessionQuotaResult Denied(string reason, int currentCount, int maxAllowed)
    {
        return new SessionQuotaResult
        {
            IsAllowed = false,
            DenialReason = reason,
            CurrentCount = currentCount,
            MaxAllowed = maxAllowed
        };
    }

    /// <summary>
    /// Creates an unlimited quota result for Admin/Editor roles.
    /// </summary>
    public static SessionQuotaResult Unlimited()
    {
        return new SessionQuotaResult
        {
            IsAllowed = true,
            DenialReason = null,
            CurrentCount = 0,
            MaxAllowed = int.MaxValue
        };
    }
}

/// <summary>
/// Information about user's current session quota status.
/// </summary>
public record SessionQuotaInfo
{
    /// <summary>
    /// Number of active sessions currently running for the user.
    /// </summary>
    public int ActiveSessions { get; init; }

    /// <summary>
    /// Maximum number of concurrent sessions allowed for this user/tier.
    /// </summary>
    public int MaxSessions { get; init; }

    /// <summary>
    /// Number of remaining session slots available.
    /// </summary>
    public int RemainingSlots { get; init; }

    /// <summary>
    /// Whether the user has unlimited session slots (Admin/Editor).
    /// </summary>
    public bool IsUnlimited { get; init; }

    /// <summary>
    /// Creates unlimited quota info for Admin/Editor roles.
    /// </summary>
    public static SessionQuotaInfo Unlimited()
    {
        return new SessionQuotaInfo
        {
            ActiveSessions = 0,
            MaxSessions = int.MaxValue,
            RemainingSlots = int.MaxValue,
            IsUnlimited = true
        };
    }
}
