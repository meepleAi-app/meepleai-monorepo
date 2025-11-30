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

    private static readonly HashSet<string> ValidTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "free", "normal", "premium"
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
    public bool IsPremium() => string.Equals(Value, "premium", StringComparison.Ordinal);

    /// <summary>
    /// Gets the tier level for comparison (0 = free, 1 = normal, 2 = premium).
    /// </summary>
    public int GetLevel()
    {
        return Value switch
        {
            "free" => 0,
            "normal" => 1,
            "premium" => 2,
            _ => 0
        };
    }

    /// <summary>
    /// Checks if this tier has at least the level of the specified tier.
    /// </summary>
    public bool HasLevel(UserTier requiredTier)
    {
        return GetLevel() >= requiredTier.GetLevel();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(UserTier tier) => tier.Value;
}
