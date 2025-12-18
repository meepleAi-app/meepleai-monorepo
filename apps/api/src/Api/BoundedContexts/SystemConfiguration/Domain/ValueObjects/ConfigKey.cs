using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Validation;

namespace Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Value object representing a validated configuration key.
/// Uses colon-separated hierarchy (e.g., "RateLimit:Admin:MaxTokens").
/// Made public to support public SystemConfiguration entity (Issue #2188).
/// </summary>
public sealed class ConfigKey : ValueObject
{
    public string Value { get; }

    public ConfigKey(string key)
    {
        Value = key
            .NotNullOrWhiteSpace(nameof(ConfigKey), "Configuration key cannot be empty")
            .Then(k => k.Trim().MaxLength(200, nameof(ConfigKey), "Configuration key cannot exceed 200 characters"))
            .Then(k => k.MatchesPattern(
                @"^[a-zA-Z0-9:_\-\.]+$",
                nameof(ConfigKey),
                "Configuration key can only contain alphanumeric characters, colons, underscores, hyphens, and dots"))
            .ThrowIfFailure(nameof(ConfigKey));
    }

    /// <summary>
    /// Gets the hierarchy levels (e.g., "RateLimit:Admin:MaxTokens" → ["RateLimit", "Admin", "MaxTokens"]).
    /// </summary>
    public string[] GetHierarchy() => Value.Split(':', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Gets the category (first hierarchy level, e.g., "RateLimit:Admin:MaxTokens" → "RateLimit").
    /// </summary>
    public string Category => GetHierarchy().FirstOrDefault() ?? Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value.ToLowerInvariant();
    }

    public override string ToString() => Value;

    public static implicit operator string(ConfigKey key) => key.Value;
}
