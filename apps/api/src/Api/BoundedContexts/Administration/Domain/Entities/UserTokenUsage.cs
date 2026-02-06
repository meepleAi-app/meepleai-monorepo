using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Aggregate Root tracking token usage for a specific user (Issue #3692)
/// </summary>
public sealed class UserTokenUsage
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TierId { get; private set; }

    // Current month tracking
    public int TokensUsed { get; private set; }
    public int MessagesCount { get; private set; }
    public decimal Cost { get; private set; }
    public DateTime LastReset { get; private set; }

    // Status flags
    public bool IsBlocked { get; private set; }
    public bool IsNearLimit { get; private set; }
    public List<DateTime> Warnings { get; private set; }

    // Historical data (JSON column)
    public List<MonthlyUsageSnapshot> History { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // EF Core
    private UserTokenUsage()
    {
        Warnings = new List<DateTime>();
        History = new List<MonthlyUsageSnapshot>();
    }

    private UserTokenUsage(Guid userId, Guid tierId)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TierId = tierId;
        TokensUsed = 0;
        MessagesCount = 0;
        Cost = 0m;
        LastReset = DateTime.UtcNow;
        IsBlocked = false;
        IsNearLimit = false;
        Warnings = new List<DateTime>();
        History = new List<MonthlyUsageSnapshot>();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static UserTokenUsage Create(Guid userId, Guid tierId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User ID cannot be empty", nameof(userId));
        if (tierId == Guid.Empty) throw new ArgumentException("Tier ID cannot be empty", nameof(tierId));

        return new UserTokenUsage(userId, tierId);
    }

    /// <summary>
    /// Record token usage from an AI request
    /// </summary>
    public void RecordUsage(int tokensConsumed, decimal costIncurred)
    {
        if (tokensConsumed < 0) throw new ArgumentException("Tokens consumed cannot be negative", nameof(tokensConsumed));
        if (costIncurred < 0) throw new ArgumentException("Cost cannot be negative", nameof(costIncurred));

        TokensUsed += tokensConsumed;
        MessagesCount++;
        Cost += costIncurred;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Check usage against tier limits and update status flags
    /// </summary>
    public void CheckLimits(TierLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        // Skip check for unlimited tiers
        if (limits.TokensPerMonth == int.MaxValue) return;

        var usagePercent = (double)TokensUsed / limits.TokensPerMonth * 100;

        // Update near-limit flag (80% threshold)
        IsNearLimit = usagePercent >= 80;

        // Add warning if crossing thresholds
        var shouldAddWarning = (usagePercent >= 80 && usagePercent < 90 && !Warnings.Any(w => w.Month == DateTime.UtcNow.Month && w.Day == DateTime.UtcNow.Day))
            || (usagePercent >= 90 && Warnings.Count(w => w.Month == DateTime.UtcNow.Month) < 2);

        if (shouldAddWarning)
        {
            Warnings.Add(DateTime.UtcNow);
        }

        // Block user if exceeding limit
        if (TokensUsed >= limits.TokensPerMonth)
        {
            IsBlocked = true;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reset monthly usage (typically called at month start)
    /// </summary>
    public void ResetMonthlyUsage()
    {
        // Archive current month to history
        var currentMonth = LastReset.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        var snapshot = MonthlyUsageSnapshot.Create(currentMonth, TokensUsed, Cost, MessagesCount);
        History.Add(snapshot);

        // Keep only last 12 months
        if (History.Count > 12)
        {
            History = History.OrderByDescending(h => h.Month, StringComparer.Ordinal).Take(12).ToList();
        }

        // Reset counters
        TokensUsed = 0;
        MessagesCount = 0;
        Cost = 0m;
        LastReset = DateTime.UtcNow;
        IsBlocked = false;
        IsNearLimit = false;
        Warnings.Clear();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Unblock user (admin action)
    /// </summary>
    public void Unblock()
    {
        IsBlocked = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Change user's tier
    /// </summary>
    public void ChangeTier(Guid newTierId)
    {
        if (newTierId == Guid.Empty) throw new ArgumentException("Tier ID cannot be empty", nameof(newTierId));

        TierId = newTierId;
        UpdatedAt = DateTime.UtcNow;
    }
}
