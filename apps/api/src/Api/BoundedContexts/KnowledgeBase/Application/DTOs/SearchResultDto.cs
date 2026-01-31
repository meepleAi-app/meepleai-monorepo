using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for search result information.
/// </summary>
internal record SearchResultDto(
    string VectorDocumentId,
    string TextContent,
    int PageNumber,
    double RelevanceScore,
    int Rank,
    string? SearchMethod
)
{
    /// <summary>
    /// Creates a DTO from domain SearchResult entity.
    /// </summary>
    public static SearchResultDto FromDomain(SearchResult searchResult)
    {
        ArgumentNullException.ThrowIfNull(searchResult);

        return new SearchResultDto(
            VectorDocumentId: searchResult.VectorDocumentId.ToString(),
            TextContent: searchResult.TextContent,
            PageNumber: searchResult.PageNumber,
            RelevanceScore: searchResult.RelevanceScore.Value,
            Rank: searchResult.Rank,
            SearchMethod: searchResult.SearchMethod
        );
    }
};

/// <summary>
/// DTO for Q&amp;A response.
/// </summary>
internal record QaResponseDto(
    string Answer,
    IReadOnlyList<SearchResultDto> Sources,
    double SearchConfidence,
    double LlmConfidence,
    double OverallConfidence,
    bool IsLowQuality,
    IReadOnlyList<CitationDto> Citations,
    RagValidationResultDto? ValidationResult = null
);

/// <summary>
/// DTO for RAG validation pipeline results (ISSUE-977: BGAI-035)
/// </summary>
internal record RagValidationResultDto(
    bool IsValid,
    int LayersPassed,
    int TotalLayers,
    string Message,
    string Severity,
    long DurationMs
);

/// <summary>
/// DTO for citation information.
/// </summary>
internal record CitationDto(
    string DocumentId,
    int PageNumber,
    string Snippet,
    double RelevanceScore
);

/// <summary>
/// DTO for explanation response.
/// </summary>
internal record ExplainResponseDto(
    string Outline,
    string Script,
    IReadOnlyList<CitationDto> Citations,
    double Confidence,
    int EstimatedReadingTimeSeconds
);
