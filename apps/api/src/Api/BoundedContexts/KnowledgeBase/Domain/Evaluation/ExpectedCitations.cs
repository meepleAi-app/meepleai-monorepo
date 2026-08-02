namespace Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// Page-level citation ground truth for an <see cref="EvaluationSample"/>. Page-level (not chunk-id)
/// so it stays stable across corpus re-index (#3427). Citation-accuracy compares the pages actually
/// cited inline in a generated answer against <see cref="PrimaryPages"/> per <see cref="MatchPolicy"/>.
/// </summary>
internal sealed record ExpectedCitations
{
    /// <summary>
    /// Ground-truth source page(s) where the answer is found. An empty list encodes the
    /// "no answer expected" edge case (typically paired with <see cref="CitationMatchPolicy.Exact"/>).
    /// </summary>
    public IReadOnlyList<int> PrimaryPages { get; init; } = [];

    /// <summary>How actual cited pages are compared to <see cref="PrimaryPages"/>. Defaults to overlap.</summary>
    public CitationMatchPolicy MatchPolicy { get; init; } = CitationMatchPolicy.OverlapAtLeastOne;

    /// <summary>
    /// Whether the pages an answer actually cited satisfy this ground truth under <see cref="MatchPolicy"/>.
    /// Comparison is set-based (page order and duplicates are irrelevant).
    /// </summary>
    public bool IsSatisfiedBy(IEnumerable<int> actualPages)
    {
        ArgumentNullException.ThrowIfNull(actualPages);

        var actual = actualPages.ToHashSet();
        var expected = PrimaryPages.ToHashSet();

        return MatchPolicy switch
        {
            CitationMatchPolicy.Exact => actual.SetEquals(expected),
            CitationMatchPolicy.OverlapAtLeastOne => actual.Overlaps(expected),
            CitationMatchPolicy.Subset => actual.IsSubsetOf(expected),
            CitationMatchPolicy.Superset => actual.IsSupersetOf(expected),
            _ => actual.Overlaps(expected)
        };
    }
}
