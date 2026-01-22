namespace Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Represents the current rate limit status for a user.
/// Contains both the effective limits (after overrides) and current usage.
/// </summary>
public sealed record RateLimitStatus
{
    /// <summary>
    /// The user this status applies to.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// The user's tier level.
    /// </summary>
    public UserTier Tier { get; init; }

    /// <summary>
    /// Whether the user has a custom override applied.
    /// </summary>
    public bool HasOverride { get; init; }

    /// <summary>
    /// Current number of pending share requests.
    /// </summary>
    public int CurrentPendingCount { get; init; }

    /// <summary>
    /// Total number of share requests made this month.
    /// </summary>
    public int CurrentMonthlyCount { get; init; }

    /// <summary>
    /// When the user was last rejected (if applicable).
    /// </summary>
    public DateTime? LastRejectionAt { get; init; }

    /// <summary>
    /// Maximum pending requests allowed (after override if applicable).
    /// </summary>
    public int EffectiveMaxPending { get; init; }

    /// <summary>
    /// Maximum requests per month allowed (after override if applicable).
    /// </summary>
    public int EffectiveMaxPerMonth { get; init; }

    /// <summary>
    /// Cooldown period after rejection (after override if applicable).
    /// </summary>
    public TimeSpan EffectiveCooldown { get; init; }

    /// <summary>
    /// Whether the user can currently create a new share request.
    /// </summary>
    public bool CanCreateRequest { get; init; }

    /// <summary>
    /// If blocked, the reason why the user cannot create a request.
    /// </summary>
    public string? BlockReason { get; init; }

    /// <summary>
    /// When the cooldown period ends (if user is in cooldown).
    /// </summary>
    public DateTime? CooldownEndsAt { get; init; }

    /// <summary>
    /// When the monthly counter resets (first of next month).
    /// </summary>
    public DateTime MonthResetAt { get; init; }

    /// <summary>
    /// Percentage of pending limit used (0-100).
    /// </summary>
    public decimal PendingUsagePercent => EffectiveMaxPending > 0
        ? Math.Round((decimal)CurrentPendingCount / EffectiveMaxPending * 100, 2)
        : 0;

    /// <summary>
    /// Percentage of monthly limit used (0-100).
    /// </summary>
    public decimal MonthlyUsagePercent => EffectiveMaxPerMonth > 0
        ? Math.Round((decimal)CurrentMonthlyCount / EffectiveMaxPerMonth * 100, 2)
        : 0;

    /// <summary>
    /// Remaining pending requests available.
    /// </summary>
    public int RemainingPendingRequests => Math.Max(0, EffectiveMaxPending - CurrentPendingCount);

    /// <summary>
    /// Remaining monthly requests available.
    /// </summary>
    public int RemainingMonthlyRequests => Math.Max(0, EffectiveMaxPerMonth - CurrentMonthlyCount);

    /// <summary>
    /// Whether the user is in a cooldown period.
    /// </summary>
    public bool IsInCooldown => CooldownEndsAt.HasValue && DateTime.UtcNow < CooldownEndsAt.Value;

    /// <summary>
    /// Whether this user has unlimited access (Admin tier).
    /// </summary>
    public bool HasUnlimitedAccess => Tier == UserTier.Admin;

    /// <summary>
    /// Creates a status for a user with unlimited access (Admin tier).
    /// </summary>
    public static RateLimitStatus CreateUnlimited(Guid userId)
    {
        var now = DateTime.UtcNow;
        return new RateLimitStatus
        {
            UserId = userId,
            Tier = UserTier.Admin,
            HasOverride = false,
            CurrentPendingCount = 0,
            CurrentMonthlyCount = 0,
            LastRejectionAt = null,
            EffectiveMaxPending = int.MaxValue,
            EffectiveMaxPerMonth = int.MaxValue,
            EffectiveCooldown = TimeSpan.Zero,
            CanCreateRequest = true,
            BlockReason = null,
            CooldownEndsAt = null,
            MonthResetAt = GetNextMonthStart(now)
        };
    }

    /// <summary>
    /// Creates a status for a user based on their tier and current usage.
    /// </summary>
    public static RateLimitStatus Create(
        Guid userId,
        UserTier tier,
        bool hasOverride,
        int currentPendingCount,
        int currentMonthlyCount,
        DateTime? lastRejectionAt,
        int effectiveMaxPending,
        int effectiveMaxPerMonth,
        TimeSpan effectiveCooldown)
    {
        var now = DateTime.UtcNow;

        // Admin tier has unlimited access
        if (tier == UserTier.Admin)
        {
            return CreateUnlimited(userId);
        }

        // Calculate cooldown end time
        DateTime? cooldownEndsAt = null;
        if (lastRejectionAt.HasValue && effectiveCooldown > TimeSpan.Zero)
        {
            cooldownEndsAt = lastRejectionAt.Value.Add(effectiveCooldown);
        }

        // Determine if user can create a request
        var canCreate = true;
        string? blockReason = null;

        if (cooldownEndsAt.HasValue && now < cooldownEndsAt.Value)
        {
            canCreate = false;
            blockReason = BlockReasons.InCooldown;
        }
        else if (currentPendingCount >= effectiveMaxPending)
        {
            canCreate = false;
            blockReason = BlockReasons.MaxPendingReached;
        }
        else if (currentMonthlyCount >= effectiveMaxPerMonth)
        {
            canCreate = false;
            blockReason = BlockReasons.MaxMonthlyReached;
        }

        return new RateLimitStatus
        {
            UserId = userId,
            Tier = tier,
            HasOverride = hasOverride,
            CurrentPendingCount = currentPendingCount,
            CurrentMonthlyCount = currentMonthlyCount,
            LastRejectionAt = lastRejectionAt,
            EffectiveMaxPending = effectiveMaxPending,
            EffectiveMaxPerMonth = effectiveMaxPerMonth,
            EffectiveCooldown = effectiveCooldown,
            CanCreateRequest = canCreate,
            BlockReason = blockReason,
            CooldownEndsAt = cooldownEndsAt,
            MonthResetAt = GetNextMonthStart(now)
        };
    }

    private static DateTime GetNextMonthStart(DateTime from)
    {
        var nextMonth = from.AddMonths(1);
        return new DateTime(nextMonth.Year, nextMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}

/// <summary>
/// Standard reasons why a user might be blocked from creating share requests.
/// </summary>
public static class BlockReasons
{
    /// <summary>
    /// The user has reached the maximum number of pending requests.
    /// </summary>
    public const string MaxPendingReached = "Maximum pending requests reached";

    /// <summary>
    /// The user has reached the maximum number of requests for this month.
    /// </summary>
    public const string MaxMonthlyReached = "Monthly request limit reached";

    /// <summary>
    /// The user is in a cooldown period after a rejection.
    /// </summary>
    public const string InCooldown = "Cooldown period after rejection active";
}
