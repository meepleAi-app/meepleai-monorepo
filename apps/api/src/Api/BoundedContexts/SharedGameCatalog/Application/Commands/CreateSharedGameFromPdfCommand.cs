using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to create a SharedGame from PDF-extracted metadata with optional BGG enrichment.
/// Supports manual overrides, duplicate detection, and approval workflow for Editors.
/// Issue #4138: Backend - Commands and DTOs - PDF Wizard
/// </summary>
/// <param name="PdfDocumentId">ID of the PDF document containing extracted metadata</param>
/// <param name="UserId">ID of the user creating the game (Admin/Editor)</param>
/// <param name="ExtractedTitle">Game title extracted from PDF (required)</param>
/// <param name="MinPlayers">Manual override for minimum players (nullable, default: 1)</param>
/// <param name="MaxPlayers">Manual override for maximum players (nullable, default: 4)</param>
/// <param name="PlayingTimeMinutes">Manual override for playing time in minutes (nullable, default: 60)</param>
/// <param name="MinAge">Manual override for minimum age (nullable, default: 8)</param>
/// <param name="SelectedBggId">Optional BGG ID for enrichment (if user selected a BGG match)</param>
/// <param name="RequiresApproval">Whether the game creation requires admin approval (true for Editors)</param>
public record CreateSharedGameFromPdfCommand(
    Guid PdfDocumentId,
    Guid UserId,
    string ExtractedTitle,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTimeMinutes,
    int? MinAge,
    int? SelectedBggId,
    bool RequiresApproval
) : ICommand<CreateGameFromPdfResult>;
