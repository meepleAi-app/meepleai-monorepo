using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Represents a citation to a source document.
/// Ensures citation has required information for traceability.
/// </summary>
public sealed class Citation : ValueObject
{
    public Guid DocumentId { get; }
    public int PageNumber { get; }
    public string Snippet { get; }
    public double RelevanceScore { get; }

    public Citation(Guid documentId, int pageNumber, string snippet, double relevanceScore)
    {
        if (pageNumber < 1)
            throw new ValidationException(nameof(PageNumber), "Page number must be positive");

        if (string.IsNullOrWhiteSpace(snippet))
            throw new ValidationException(nameof(Snippet), "Snippet cannot be empty");

        if (relevanceScore < 0 || relevanceScore > 1)
            throw new ValidationException(nameof(RelevanceScore), "Relevance score must be between 0 and 1");

        DocumentId = documentId;
        PageNumber = pageNumber;
        Snippet = snippet.Trim();
        RelevanceScore = relevanceScore;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DocumentId;
        yield return PageNumber;
        yield return Snippet;
        yield return RelevanceScore;
    }

    public override string ToString() => $"Page {PageNumber} (Score: {RelevanceScore:P0})";
}
