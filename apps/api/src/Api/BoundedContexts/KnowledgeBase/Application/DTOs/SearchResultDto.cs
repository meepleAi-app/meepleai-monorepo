using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for search result information.
/// </summary>
public record SearchResultDto(
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
        if (searchResult == null)
            throw new ArgumentNullException(nameof(searchResult));

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
/// DTO for Q&A response.
/// </summary>
public record QaResponseDto(
    string Answer,
    List<SearchResultDto> Sources,
    double SearchConfidence,
    double LlmConfidence,
    double OverallConfidence,
    bool IsLowQuality,
    List<CitationDto> Citations,
    RagValidationResultDto? ValidationResult = null
);

/// <summary>
/// DTO for RAG validation pipeline results (ISSUE-977: BGAI-035)
/// </summary>
public record RagValidationResultDto(
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
public record CitationDto(
    string DocumentId,
    int PageNumber,
    string Snippet,
    double RelevanceScore
);

/// <summary>
/// DTO for explanation response.
/// </summary>
public record ExplainResponseDto(
    string Outline,
    string Script,
    List<CitationDto> Citations,
    double Confidence,
    int EstimatedReadingTimeSeconds
);
