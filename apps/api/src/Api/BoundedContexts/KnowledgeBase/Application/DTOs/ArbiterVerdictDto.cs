namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Structured verdict returned by the Arbiter agent for dispute resolution.
/// Issue #5585: Arbiter Mode — dispute arbitration with citations and verdict.
/// </summary>
public record ArbiterVerdictDto
{
    /// <summary>
    /// The verdict: who is right and why.
    /// </summary>
    public required string Verdict { get; init; }

    /// <summary>
    /// Confidence score (0-1) based on retrieval quality.
    /// Formula: avg(chunk_scores) * (citations > 0 ? 1.0 : 0.5)
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// True if confidence >= 0.85, indicating a conclusive verdict.
    /// </summary>
    public bool IsConclusive { get; init; }

    /// <summary>
    /// Exact rulebook citations supporting the verdict.
    /// </summary>
    public List<ArbiterCitationDto> Citations { get; init; } = new();

    /// <summary>
    /// Warning message when active expansions are missing analyzed PDFs.
    /// </summary>
    public string? ExpansionWarning { get; init; }
}

/// <summary>
/// A single citation from the rulebook used to support an arbiter verdict.
/// Issue #5585: Arbiter Mode — dispute arbitration with citations and verdict.
/// </summary>
public record ArbiterCitationDto
{
    /// <summary>
    /// Title of the source document (PDF file name).
    /// </summary>
    public required string DocumentTitle { get; init; }

    /// <summary>
    /// Section or page reference within the document.
    /// </summary>
    public required string Section { get; init; }

    /// <summary>
    /// Exact text passage from the rulebook.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Relevance score of this citation (0-1).
    /// </summary>
    public double RelevanceScore { get; init; }
}
