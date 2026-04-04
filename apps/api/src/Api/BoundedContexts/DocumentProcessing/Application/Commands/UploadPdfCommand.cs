using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to upload a PDF document for a game.
/// Supports legacy gameId, metadata-based auto-creation, and private games (Issue #3664).
/// </summary>
internal record UploadPdfCommand(
    string? GameId,                    // Legacy: existing game ID (backward compatibility)
    PdfUploadMetadata? Metadata,       // New: game metadata for auto-creation
    Guid? PrivateGameId,               // Issue #3664: Private game ID
    Guid UserId,
    IFormFile File,
    string? Priority = null            // "normal" | "urgent" | null — admin priority override
) : ICommand<PdfUploadResult>;
