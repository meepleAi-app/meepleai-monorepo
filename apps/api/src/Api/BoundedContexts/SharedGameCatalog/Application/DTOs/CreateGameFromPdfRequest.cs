namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// HTTP request payload for creating a SharedGame from PDF wizard workflow.
/// Maps to CreateSharedGameFromPdfCommand for CQRS handling.
/// Issue #4138: Backend - Commands and DTOs - PDF Wizard
/// </summary>
public record CreateGameFromPdfRequest
{
    /// <summary>
    /// ID of the uploaded PDF document (from Step 1: Upload PDF)
    /// </summary>
    public required Guid PdfDocumentId { get; init; }

    /// <summary>
    /// Game title extracted from PDF (required field)
    /// </summary>
    public required string ExtractedTitle { get; init; }

    /// <summary>
    /// Manual override for minimum number of players (nullable).
    /// If null, defaults to 1.
    /// </summary>
    public int? MinPlayers { get; init; }

    /// <summary>
    /// Manual override for maximum number of players (nullable).
    /// If null, defaults to 4.
    /// </summary>
    public int? MaxPlayers { get; init; }

    /// <summary>
    /// Manual override for playing time in minutes (nullable).
    /// If null, defaults to 60.
    /// </summary>
    public int? PlayingTimeMinutes { get; init; }

    /// <summary>
    /// Manual override for minimum age (nullable).
    /// If null, defaults to 8.
    /// </summary>
    public int? MinAge { get; init; }

    /// <summary>
    /// Selected BoardGameGeek game ID for enrichment (nullable).
    /// If provided, BGG metadata will be merged with PDF-extracted data.
    /// Manual overrides take precedence over BGG data.
    /// </summary>
    public int? SelectedBggId { get; init; }

    /// <summary>
    /// Whether the game creation requires admin approval.
    /// Set to true for Editor users, false for Admin users.
    /// Determines if game is created as Draft (pending approval) or Published.
    /// </summary>
    public bool RequiresApproval { get; init; }
}
