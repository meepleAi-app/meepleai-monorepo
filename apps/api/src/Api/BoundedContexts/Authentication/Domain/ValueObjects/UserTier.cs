using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Represents a user subscription tier in the system.
/// Determines upload quotas and feature access.
/// Tiers are independent from roles (admin/editor/user).
/// </summary>
public sealed class UserTier : ValueObject
{
    public static readonly UserTier Free = new("free");
    public static readonly UserTier Normal = new("normal");
    public static readonly UserTier Premium = new("premium");
    public static readonly UserTier Pro = new("pro"); // Alias for Premium
    public static readonly UserTier Enterprise = new("enterprise"); // Epic #4068

    private static readonly HashSet<string> ValidTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "free", "normal", "premium", "pro", "enterprise"
    };

    public string Value { get; }

    private UserTier(string value)
    {
        Value = value.ToLowerInvariant();
    }

    public static UserTier Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(nameof(UserTier), "User tier cannot be empty");

        var normalized = value.ToLowerInvariant();
        if (!ValidTiers.Contains(normalized))
            throw new ValidationException(nameof(UserTier), $"Invalid user tier: {value}. Valid tiers are: free, normal, premium");

        return new UserTier(normalized);
    }

    public bool IsFree() => string.Equals(Value, "free", StringComparison.Ordinal);
    public bool IsNormal() => string.Equals(Value, "normal", StringComparison.Ordinal);
    public bool IsPremium() => string.Equals(Value, "premium", StringComparison.Ordinal) || string.Equals(Value, "pro", StringComparison.Ordinal);
    public bool IsPro() => IsPremium(); // Alias (Epic #4068)
    public bool IsEnterprise() => string.Equals(Value, "enterprise", StringComparison.Ordinal); // Epic #4068

    /// <summary>
    /// Gets the tier level for comparison (0 = free, 1 = normal, 2 = premium/pro, 3 = enterprise).
    /// Epic #4068: Added Enterprise tier
    /// </summary>
    public int GetLevel()
    {
        return Value switch
        {
            "free" => 0,
            "normal" => 1,
            "premium" => 2,
            "pro" => 2, // Alias for premium
            "enterprise" => 3,
            _ => 0
        };
    }

    /// <summary>
    /// Gets collection limits for this tier (Epic #4068)
    /// </summary>
    public CollectionLimits GetLimits()
    {
        return Value switch
        {
            "free" => new CollectionLimits(MaxGames: 50, StorageQuotaMB: 100),
            "normal" => new CollectionLimits(MaxGames: 100, StorageQuotaMB: 500),
            "premium" or "pro" => new CollectionLimits(MaxGames: 500, StorageQuotaMB: 5000),
            "enterprise" => new CollectionLimits(MaxGames: int.MaxValue, StorageQuotaMB: int.MaxValue),
            _ => new CollectionLimits(MaxGames: 50, StorageQuotaMB: 100) // Default to free
        };
    }

    /// <summary>
    /// Checks if this tier has at least the level of the specified tier.
    /// </summary>
    public bool HasLevel(UserTier requiredTier)
    {
        ArgumentNullException.ThrowIfNull(requiredTier);
        return GetLevel() >= requiredTier.GetLevel();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(UserTier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);
        return tier.Value;
    }
}

/// <summary>
/// Collection limits per tier (Epic #4068 - Issue #4177)
/// </summary>
public record CollectionLimits(int MaxGames, int StorageQuotaMB);
