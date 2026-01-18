using Api.BoundedContexts.Authentication.Domain.ValueObjects;

namespace Api.BoundedContexts.UserLibrary.Domain.Services;

/// <summary>
/// Domain service for managing game library quotas based on user tier.
///
/// <para><strong>Authorization Behavior:</strong></para>
/// <list type="bullet">
/// <item><description>Admin and Editor roles: <strong>Unlimited games</strong> (bypass all quota checks)</description></item>
/// <item><description>User role: Subject to tier-based game limits</description></item>
/// </list>
///
/// <para><strong>Tier Limits (default values):</strong></para>
/// <list type="bullet">
/// <item><description>Free: 5 games max</description></item>
/// <item><description>Normal: 20 games max</description></item>
/// <item><description>Premium: 50 games max</description></item>
/// </list>
///
/// <para>Limits are configurable via SystemConfiguration with keys: LibraryLimits:{tier}:MaxGames</para>
/// </summary>
internal interface IGameLibraryQuotaService
{
    /// <summary>
    /// Checks if a user can add a game to their library based on tier limits.
    ///
    /// <para><strong>Role-Based Bypass:</strong></para>
    /// <para>Admin and Editor roles automatically bypass quota checks and have unlimited games.
    /// This is enforced at the service level for defense in depth, even if endpoint validation exists.</para>
    ///
    /// <para><strong>Quota Enforcement:</strong></para>
    /// <para>Regular users (User role) are subject to tier-based limits:
    /// - Free tier: 5 games maximum
    /// - Normal tier: 20 games maximum
    /// - Premium tier: 50 games maximum</para>
    /// </summary>
    /// <param name="userId">User ID to check quota for</param>
    /// <param name="userTier">User's subscription tier (free/normal/premium)</param>
    /// <param name="userRole">User's role (determines if quota bypass applies)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with allowed status, current count, and max allowed</returns>
    /// <exception cref="ArgumentNullException">Thrown if userTier or userRole is null</exception>
    Task<LibraryQuotaResult> CheckQuotaAsync(
        Guid userId,
        UserTier userTier,
        Role userRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed quota information for a user's library.
    /// </summary>
    /// <param name="userId">User ID to get quota info for</param>
    /// <param name="userTier">User's subscription tier</param>
    /// <param name="userRole">User's role</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed quota information including games in library, max allowed, and remaining slots</returns>
    Task<LibraryQuotaInfo> GetQuotaInfoAsync(
        Guid userId,
        UserTier userTier,
        Role userRole,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of library quota check operation.
/// </summary>
internal record LibraryQuotaResult
{
    /// <summary>
    /// Whether the user is allowed to add a game to their library.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Reason for denial, null if allowed.
    /// </summary>
    public string? DenialReason { get; init; }

    /// <summary>
    /// Current number of games in the user's library.
    /// </summary>
    public int CurrentCount { get; init; }

    /// <summary>
    /// Maximum number of games allowed for this user/tier.
    /// </summary>
    public int MaxAllowed { get; init; }

    /// <summary>
    /// Creates a successful quota check result.
    /// </summary>
    public static LibraryQuotaResult Allowed(int currentCount, int maxAllowed)
    {
        return new LibraryQuotaResult
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
    public static LibraryQuotaResult Denied(string reason, int currentCount, int maxAllowed)
    {
        return new LibraryQuotaResult
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
    public static LibraryQuotaResult Unlimited()
    {
        return new LibraryQuotaResult
        {
            IsAllowed = true,
            DenialReason = null,
            CurrentCount = 0,
            MaxAllowed = int.MaxValue
        };
    }
}

/// <summary>
/// Information about user's current library quota status.
/// </summary>
internal record LibraryQuotaInfo
{
    /// <summary>
    /// Number of games currently in the user's library.
    /// </summary>
    public int GamesInLibrary { get; init; }

    /// <summary>
    /// Maximum number of games allowed for this user/tier.
    /// </summary>
    public int MaxGames { get; init; }

    /// <summary>
    /// Number of remaining slots available to add games.
    /// </summary>
    public int RemainingSlots { get; init; }

    /// <summary>
    /// Whether the user has unlimited game slots (Admin/Editor).
    /// </summary>
    public bool IsUnlimited { get; init; }

    /// <summary>
    /// Creates unlimited quota info for Admin/Editor roles.
    /// </summary>
    public static LibraryQuotaInfo Unlimited()
    {
        return new LibraryQuotaInfo
        {
            GamesInLibrary = 0,
            MaxGames = int.MaxValue,
            RemainingSlots = int.MaxValue,
            IsUnlimited = true
        };
    }
}
