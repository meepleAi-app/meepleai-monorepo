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
    string? SearchMethod,
    // SP-C (#3407): region-grounding carriers (primitives, no layer dep). Null for the keyword arm /
    // pre-coordinate corpus. Parsed to CitationRegion + Full-gated at the StreamQa handler boundary.
    string? BoundingBoxesJson = null,
    int? CharStart = null,
    int? CharEnd = null,
    // #4168 (5e-c-2): l'identità del chunk e i segnali grezzi della fusione. Li legge la ricerca fra
    // giochi, che fonde di nuovo sopra questo recupero e ne scrive il dump di taratura.
    string? PdfDocumentId = null,
    int ChunkIndex = 0,
    float? HybridScore = null,
    float? VectorScore = null,
    float? KeywordScore = null,
    string? Language = null,
    IReadOnlyList<string>? MatchedTerms = null,
    // #4288: l'id del text_chunks del passaggio. VectorDocumentId non lo identifica: il braccio
    // vettoriale ci mette l'id del documento vettoriale, quello lessicale l'id del PDF.
    Guid? TextChunkId = null,
    // #4482: il chunk è la traduzione inglese di un chunk di un PDF non inglese.
    bool IsTranslation = false
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
            SearchMethod: searchResult.SearchMethod,
            BoundingBoxesJson: searchResult.BoundingBoxesJson,
            CharStart: searchResult.CharStart,
            CharEnd: searchResult.CharEnd,
            PdfDocumentId: searchResult.PdfDocumentId.ToString(),
            ChunkIndex: searchResult.ChunkIndex,
            HybridScore: searchResult.Signals.HybridScore,
            VectorScore: searchResult.Signals.VectorScore,
            KeywordScore: searchResult.Signals.KeywordScore,
            Language: searchResult.Signals.Language,
            MatchedTerms: searchResult.Signals.MatchedTerms,
            TextChunkId: searchResult.TextChunkId,
            IsTranslation: searchResult.Signals.IsTranslation
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
    RagValidationResultDto? ValidationResult = null,
    string? ErrorCode = null,
    // #4168 (ADR-095 D2): la versione del profilo e la lingua di una risposta generata; null quando
    // la risposta non viene dal modello (documenti in elaborazione, nessun contesto).
    Guid? ProfileVersionId = null,
    string? Language = null
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
    // #3855: nullable perche' una citazione servita dalla cache puo' non portare la pagina, e in
    // quel caso dichiararlo e' onesto mentre un numero plausibile e' una fonte falsa.
    int? PageNumber,
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
