namespace Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// Policy for comparing the pages a generated answer actually cites against a sample's
/// <see cref="ExpectedCitations.PrimaryPages"/> ground truth. Mirrors the Libro-Game golden-set
/// schema (<c>tests/llm-eval/golden-set/schema.md</c>).
/// </summary>
internal enum CitationMatchPolicy
{
    /// <summary>Actual cited pages equal the expected pages exactly (set equality, order-independent).</summary>
    Exact,

    /// <summary>Intersection of actual and expected pages is non-empty. Pragmatic default.</summary>
    OverlapAtLeastOne,

    /// <summary>Actual cited pages are a subset of expected (no extra pages cited).</summary>
    Subset,

    /// <summary>Actual cited pages are a superset of expected (all expected pages present).</summary>
    Superset
}
