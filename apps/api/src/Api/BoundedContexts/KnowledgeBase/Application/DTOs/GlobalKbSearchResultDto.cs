namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// A single enriched search result from the cross-game KB search.
/// Mirrors the FE contract §5 shape (kb-globale-hooks.md).
/// Issue #1661: cross-game KB search (Task 4).
/// </summary>
/// <param name="ChunkId">
/// Best-effort chunk identifier: <c>"{PdfDocumentId}_{ChunkIndex}"</c>
/// sourced from <see cref="Services.MultiGameSearchResultItem.ChunkId"/>.
/// </param>
/// <param name="DocId">PdfDocument.Id (the KB document).</param>
/// <param name="DocTitle">PdfDocument.FileName — human-readable document name.</param>
/// <param name="GameId">SharedGame.Id that owns the document.</param>
/// <param name="GameName">SharedGame.Title.</param>
/// <param name="DocType">PdfDocument.DocumentType (e.g. "base", "expansion", "errata").</param>
/// <param name="HeadingPath">
/// Best-effort section heading path. Currently <c>null</c> because chunk-level heading
/// metadata is not materialised in the pgvector result set (EC-6 / D2 known limitation).
/// Will be non-null in a future follow-up if materialisation is added.
/// </param>
/// <param name="Snippet">Raw text snippet of the chunk.</param>
/// <param name="PageNumber">1-based page number; null when not available from the vector store.</param>
/// <param name="Score">Final hybrid score after RRF fusion.</param>
internal sealed record GlobalKbSearchResultDto(
    string ChunkId,
    Guid DocId,
    string DocTitle,
    Guid GameId,
    string GameName,
    string DocType,
    string? HeadingPath,
    string Snippet,
    int? PageNumber,
    float Score);
