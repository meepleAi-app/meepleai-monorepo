using System.Text.RegularExpressions;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// When a claim applies: canonical (trimmed, lower-case, single-spaced) names from the
/// EntityExtractor vocabulary (Phase / Action / Component). Null field = wildcard.
/// </summary>
public sealed record MechanicTrigger(string? Phase, string? Action, string? Component)
{
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    /// <summary>True when every field is null. Computed, so it is never serialized (e.g. into the jsonb column).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => Phase is null && Action is null && Component is null;

    public static string Normalize(string raw) =>
        Spaces.Replace(raw.Trim(), " ").ToLowerInvariant();

    private static string? NormalizeOrNull(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : Normalize(raw);

    /// <summary>Returns null when every field is blank, so callers store "no trigger" as null.</summary>
    public static MechanicTrigger? Create(string? phase, string? action, string? component)
    {
        var t = new MechanicTrigger(NormalizeOrNull(phase), NormalizeOrNull(action), NormalizeOrNull(component));
        return t.IsEmpty ? null : t;
    }
}
