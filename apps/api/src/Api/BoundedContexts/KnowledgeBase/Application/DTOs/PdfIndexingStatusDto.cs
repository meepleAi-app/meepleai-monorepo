namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for PDF indexing/processing status of a game's document.
/// Issue #4943: GET /api/v1/library/games/{gameId}/pdf-status
/// </summary>
/// <param name="Status">Current indexing state: pending | processing | indexed | failed</param>
/// <param name="Progress">0-100 percentage (null if unknown / not tracked)</param>
/// <param name="ChunkCount">Number of chunks indexed (populated when status = indexed)</param>
/// <param name="ErrorMessage">Error details (populated when status = failed)</param>
public record PdfIndexingStatusDto(
    string Status,
    int? Progress,
    int? ChunkCount,
    string? ErrorMessage);
