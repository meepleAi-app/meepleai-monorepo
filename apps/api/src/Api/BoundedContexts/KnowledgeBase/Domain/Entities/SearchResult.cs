using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Represents a search result from vector/hybrid search.
/// Not an aggregate root - created during search operations.
/// </summary>
public sealed class SearchResult : Entity<Guid>
{
    public Guid VectorDocumentId { get; private set; }
    public string TextContent { get; private set; }
    public int PageNumber { get; private set; }
    public Confidence RelevanceScore { get; private set; }
    public int Rank { get; private set; }
    public string? SearchMethod { get; private set; } // "vector", "keyword", "hybrid"

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private SearchResult() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new search result.
    /// </summary>
    public SearchResult(
        Guid id,
        Guid vectorDocumentId,
        string textContent,
        int pageNumber,
        Confidence relevanceScore,
        int rank,
        string? searchMethod = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(textContent))
            throw new ArgumentException("Text content cannot be empty", nameof(textContent));

        if (pageNumber < 1)
            throw new ArgumentException("Page number must be positive", nameof(pageNumber));

        if (rank < 1)
            throw new ArgumentException("Rank must be positive", nameof(rank));

        VectorDocumentId = vectorDocumentId;
        TextContent = textContent;
        PageNumber = pageNumber;
        RelevanceScore = relevanceScore ?? throw new ArgumentNullException(nameof(relevanceScore));
        Rank = rank;
        SearchMethod = searchMethod;
    }

    /// <summary>
    /// Creates a citation from this search result.
    /// </summary>
    public Citation ToCitation()
    {
        return new Citation(
            documentId: VectorDocumentId,
            pageNumber: PageNumber,
            snippet: TextContent.Length > 200 ? TextContent[..200] + "..." : TextContent,
            relevanceScore: RelevanceScore.Value
        );
    }

    /// <summary>
    /// Checks if this result meets a minimum confidence threshold.
    /// </summary>
    public bool MeetsThreshold(double minScore)
    {
        return RelevanceScore.Value >= minScore;
    }
}
