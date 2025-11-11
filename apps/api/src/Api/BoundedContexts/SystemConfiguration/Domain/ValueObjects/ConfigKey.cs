using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Value object representing a validated configuration key.
/// Uses colon-separated hierarchy (e.g., "RateLimit:Admin:MaxTokens").
/// </summary>
public sealed class ConfigKey : ValueObject
{
    public string Value { get; }

    public ConfigKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ValidationException("Configuration key cannot be empty");

        var trimmed = key.Trim();

        if (trimmed.Length > 200)
            throw new ValidationException("Configuration key cannot exceed 200 characters");

        // Validate format: alphanumeric, colons, underscores, hyphens only
        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[a-zA-Z0-9:_\-\.]+$"))
            throw new ValidationException("Configuration key can only contain alphanumeric characters, colons, underscores, hyphens, and dots");

        Value = trimmed;
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
