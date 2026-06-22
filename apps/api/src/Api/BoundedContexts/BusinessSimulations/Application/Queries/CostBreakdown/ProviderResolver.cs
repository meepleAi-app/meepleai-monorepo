using System.Text.Json;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries.CostBreakdown;

/// <summary>
/// Maps a <see cref="Domain.Entities.LedgerEntry"/> to a stable provider
/// display name used by the admin Business charts (Issue #1838 SP5 F4-C5).
///
/// <para>Logic, in priority order:</para>
/// <list type="number">
///   <item>Category fast-path: <c>Infrastructure</c> → "Infrastructure",
///     <c>Subscription</c>/<c>TokenPurchase</c>/<c>PlatformFee</c>/<c>Refund</c>/
///     <c>Marketing</c>/<c>Operational</c>/<c>Other</c> → their category name.</item>
///   <item>For <c>TokenUsage</c> entries: parse <c>metadata.modelId</c>
///     (shape <c>provider/model-name</c>) and return the provider segment
///     title-cased (e.g. <c>openai/gpt-4o-mini</c> → "OpenAI").</item>
///   <item>Anything we cannot resolve → "Unknown" so the chart still aggregates
///     spend without losing rows.</item>
/// </list>
///
/// <para>Logic is centralised here so both the by-provider and by-feature
/// breakdowns stay consistent — the two queries will share this resolver to
/// avoid drift in provider naming.</para>
/// </summary>
internal static class ProviderResolver
{
    /// <summary>Provider name returned when the row carries no resolvable provider.</summary>
    public const string Unknown = "Unknown";

    /// <summary>Resolve the provider display name for an aggregated row.</summary>
    public static string Resolve(LedgerCategory category, string? metadataJson)
    {
        switch (category)
        {
            case LedgerCategory.Infrastructure:
                return "Infrastructure";
            case LedgerCategory.Subscription:
                return "Subscription";
            case LedgerCategory.TokenPurchase:
                return "TokenPurchase";
            case LedgerCategory.PlatformFee:
                return "PlatformFee";
            case LedgerCategory.Refund:
                return "Refund";
            case LedgerCategory.Marketing:
                return "Marketing";
            case LedgerCategory.Operational:
                return "Operational";
            case LedgerCategory.Other:
                return "Other";
            case LedgerCategory.TokenUsage:
                return ResolveTokenUsageProvider(metadataJson);
            default:
                return Unknown;
        }
    }

    /// <summary>Feature display name (used by the by-feature breakdown query).
    /// One-to-one map of <see cref="LedgerCategory"/> values to a stable
    /// human-readable label.</summary>
    public static string ResolveFeatureName(LedgerCategory category) => category switch
    {
        LedgerCategory.Subscription => "Subscription",
        LedgerCategory.TokenPurchase => "Token Purchase",
        LedgerCategory.TokenUsage => "AI Token Usage",
        LedgerCategory.PlatformFee => "Platform Fee",
        LedgerCategory.Refund => "Refund",
        LedgerCategory.Operational => "Operational",
        LedgerCategory.Marketing => "Marketing",
        LedgerCategory.Infrastructure => "Infrastructure",
        LedgerCategory.Other => "Other",
        _ => Unknown,
    };

    private static string ResolveTokenUsageProvider(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return Unknown;

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (!doc.RootElement.TryGetProperty("modelId", out var modelEl)) return Unknown;
            var modelId = modelEl.GetString();
            if (string.IsNullOrWhiteSpace(modelId)) return Unknown;

            var slash = modelId.IndexOf('/', StringComparison.Ordinal);
            var rawProvider = slash > 0 ? modelId[..slash] : modelId;

            return NormalizeProviderName(rawProvider);
        }
        catch (JsonException)
        {
            return Unknown;
        }
    }

    private static string NormalizeProviderName(string raw)
    {
        return raw.ToLowerInvariant() switch
        {
            "openai" => "OpenAI",
            "anthropic" => "Anthropic",
            "deepseek" => "DeepSeek",
            "openrouter" => "OpenRouter",
            "google" => "Google",
            "meta" or "meta-llama" => "Meta",
            "mistral" or "mistralai" => "Mistral",
            "cohere" => "Cohere",
            _ => char.ToUpperInvariant(raw[0]) + raw[1..].ToLowerInvariant(),
        };
    }
}
