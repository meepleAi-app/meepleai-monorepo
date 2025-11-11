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
);

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
    List<CitationDto> Citations
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
