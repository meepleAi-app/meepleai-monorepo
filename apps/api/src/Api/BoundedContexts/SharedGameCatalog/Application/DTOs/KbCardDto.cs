namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO for a Knowledge Base card (indexed vector document) associated with a shared game.
/// Issue #4925
/// </summary>
public sealed record KbCardDto(
    Guid Id,
    Guid PdfDocumentId,
    string FileName,
    string IndexingStatus,
    int ChunkCount,
    DateTime? IndexedAt,
    string? DocumentType,
    string? Version,
    bool IsActive
);
