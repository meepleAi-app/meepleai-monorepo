using System.Diagnostics.CodeAnalysis;

namespace Api.BoundedContexts.KbQuality.Domain.Goldset;

/// <summary>
/// Value object identifying a goldset generation strategy version.
/// Code-resident registry parity con IndexerVersion #1673 (R-2).
/// Retention SLA: 18 months post-supersession.
/// </summary>
public sealed record GoldsetVersion(string Version, string DisplayName, GoldsetStrategy Strategy)
{
    public static GoldsetVersion AutoCurrent { get; } =
        new("auto-v1", "Auto LLM v1", GoldsetStrategy.LlmAutoGen);

    public static IReadOnlyList<GoldsetVersion> Registry { get; } = [AutoCurrent];

    public static bool TryGet(string? version, [NotNullWhen(true)] out GoldsetVersion? result)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            result = null;
            return false;
        }

        result = Registry.FirstOrDefault(v =>
            string.Equals(v.Version, version, StringComparison.OrdinalIgnoreCase));

        return result is not null;
    }
}
