namespace Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// Wire-format (snake_case) mapping for <see cref="CitationMatchPolicy"/>, matching the golden-set
/// schema (<c>tests/llm-eval/golden-set/schema.md</c>): "exact", "overlap_at_least_one", "subset",
/// "superset". Mapping is explicit (not naming-policy-derived) to keep the JSON contract stable.
/// </summary>
internal static class CitationMatchPolicyWire
{
    /// <summary>Renders the policy as its snake_case wire value.</summary>
    public static string ToWireValue(this CitationMatchPolicy policy) => policy switch
    {
        CitationMatchPolicy.Exact => "exact",
        CitationMatchPolicy.OverlapAtLeastOne => "overlap_at_least_one",
        CitationMatchPolicy.Subset => "subset",
        CitationMatchPolicy.Superset => "superset",
        _ => "overlap_at_least_one"
    };

    /// <summary>
    /// Parses a wire value. A null/blank value (an omitted match_policy) falls back to the pragmatic
    /// default (overlap). A non-blank UNRECOGNIZED token throws — a typo must not silently degrade to the
    /// loosest comparison, which would weaken the citation-accuracy gate in the false-negative direction (#3467).
    /// </summary>
    public static CitationMatchPolicy Parse(string? wireValue) => wireValue?.Trim().ToLowerInvariant() switch
    {
        null or "" => CitationMatchPolicy.OverlapAtLeastOne,
        "exact" => CitationMatchPolicy.Exact,
        "overlap_at_least_one" => CitationMatchPolicy.OverlapAtLeastOne,
        "subset" => CitationMatchPolicy.Subset,
        "superset" => CitationMatchPolicy.Superset,
        _ => throw new ArgumentException(
            $"Unrecognized citation match_policy '{wireValue}'. Expected one of: exact, overlap_at_least_one, subset, superset.",
            nameof(wireValue))
    };
}
