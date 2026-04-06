using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Result of the ImportGameFromPdfCommand saga.
/// </summary>
public record ImportGameFromPdfResult(
    Guid GameId,
    Guid PdfDocumentId,
    string IndexingStatus,
    string? Warning = null
);

/// <summary>
/// Orchestrating saga command that imports a board game from an already-uploaded (orphaned) PDF.
///
/// Saga steps (Nygard Production Resilience pattern):
///   Step 1 — CreateSharedGame(metadata)        → gameId
///   Step 2 — AddDocumentToSharedGame(gameId, pdfId)
///             ON FAILURE → DeleteSharedGame(gameId) [compensation], re-throw
///   Step 3 — Enqueue IndexDocumentCommand(pdfId, gameId)  [fire-and-forget, async]
///             ON FAILURE → return result with warning (game + PDF linked, indexing deferred)
///
/// The game is created in Draft status; caller must explicitly publish it.
/// </summary>
/// <param name="Metadata">Reviewed metadata (from wizard step 2)</param>
/// <param name="PdfDocumentId">ID of the already-uploaded orphaned PdfDocument</param>
/// <param name="CoverImageUrl">Optional cover image URL (null = placeholder)</param>
/// <param name="RequestedBy">UserId of the admin executing the import</param>
internal record ImportGameFromPdfCommand(
    ImportGameMetadataDto Metadata,
    Guid PdfDocumentId,
    string? CoverImageUrl,
    Guid RequestedBy
) : ICommand<ImportGameFromPdfResult>;

/// <summary>
/// Metadata DTO for game creation in the import wizard.
/// </summary>
public record ImportGameMetadataDto(
    string Title,
    int? YearPublished,
    string? Description,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTimeMinutes,
    int? MinAge,
    List<string>? Publishers,
    List<string>? Designers,
    List<string>? Categories,
    List<string>? Mechanics
);
