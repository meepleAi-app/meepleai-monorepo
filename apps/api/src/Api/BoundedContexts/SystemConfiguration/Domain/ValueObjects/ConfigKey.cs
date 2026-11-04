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

    /// <remarks>
    /// 🔴 Il parametro si chiama <c>value</c> come la proprietà <see cref="Value"/>, e non è un caso: i
    /// <c>Configuration*Event</c> portano un <see cref="ConfigKey"/>, e l'outbox li riscrive con
    /// <c>System.Text.Json</c>, che lega ogni parametro del costruttore a una proprietà dello stesso nome. Con il nome
    /// <c>key</c> la deserializzazione lanciava (#4608): in produzione la riga andava in retry e poi in Failed, e
    /// nessun handler di quegli eventi partiva. <c>ConfigurationEventsJsonRoundTripTests</c> lo tiene fermo.
    /// Il JSON scritto non cambia: <c>{"value": ..., "category": ...}</c>, e le righe già salvate restano leggibili.
    /// </remarks>
    public ConfigKey(string value)
    {
        Value = value
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
