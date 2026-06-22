namespace Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;

/// <summary>
/// Value object identifying an LLM provider whose API key can be rotated via
/// <c>POST /api/v1/admin/providers/{name}/rotate-key</c> (issue #1859).
/// Whitelist enforced: only providers with rotatable API keys are allowed.
/// Ollama (local, no auth) is intentionally excluded.
/// </summary>
public sealed record ProviderName
{
    public static readonly IReadOnlySet<string> Allowed =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "deepseek", "openrouter" };

    public string Value { get; }

    private ProviderName(string value) => Value = value;

    public static ProviderName Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException(
                $"Provider name cannot be empty. Allowed: {string.Join(", ", Allowed.OrderBy(x => x, StringComparer.Ordinal))}",
                nameof(raw));

        var normalized = raw.Trim().ToLowerInvariant();
        if (!Allowed.Contains(normalized))
            throw new ArgumentException(
                $"Provider '{raw}' is not in the allowed set: {string.Join(", ", Allowed.OrderBy(x => x, StringComparer.Ordinal))}",
                nameof(raw));

        return new ProviderName(normalized);
    }

    public override string ToString() => Value;
}
