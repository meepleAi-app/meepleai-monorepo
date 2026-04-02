using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminGameKbDocuments;

/// <summary>
/// Query to list all KB-indexed documents for a specific game (admin view).
/// KB-01: Admin per-game KB backend.
/// </summary>
internal sealed record GetAdminGameKbDocumentsQuery(Guid GameId)
    : IQuery<AdminGameKbDocumentsDto>;

/// <summary>
/// Result DTO containing all indexed documents for a game.
/// </summary>
internal sealed record AdminGameKbDocumentsDto(
    Guid GameId,
    List<AdminKbDocumentItemDto> Documents);

/// <summary>
/// Individual document item in the admin KB list.
/// </summary>
internal sealed record AdminKbDocumentItemDto(
    Guid Id,
    Guid PdfDocumentId,
    string Language,
    int ChunkCount,
    DateTime IndexedAt,
    Guid? SharedGameId);
