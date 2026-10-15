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
/// Step 4 of the PDF import wizard: completes the draft game created at step 1
/// (<see cref="StartGameImportCommand"/>) with the metadata the admin reviewed.
///
/// #4401: the game comes first. The rulebook was uploaded FOR it (chunked upload with its id), so
/// the PDF is already tied to the game and indexed by the upload itself (#4395): this command does
/// not create the game, link the PDF or start an indexing. Steps, each safe to repeat when the
/// wizard retries:
///   1 — the reviewed metadata goes on the game (<c>UpdateSharedGameCommand</c>);
///   2 — the skeleton becomes complete, out of the BGG enrichment queue;
///   3 — the PDF is registered as the game's rulebook, unless it already is.
///
/// The game stays in Draft status; the caller publishes it.
/// </summary>
/// <param name="GameId">The draft game created at step 1.</param>
/// <param name="Metadata">Reviewed metadata (from wizard step 2)</param>
/// <param name="PdfDocumentId">The rulebook uploaded for <paramref name="GameId"/>.</param>
/// <param name="CoverImageUrl">Optional absolute cover URL (null = keep the game's, a placeholder for a new one)</param>
/// <param name="RequestedBy">UserId of the admin executing the import</param>
internal record ImportGameFromPdfCommand(
    Guid GameId,
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
