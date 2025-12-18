using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to upload a PDF document for a game.
/// Supports both legacy gameId (backward compatibility) and new metadata-based auto-creation.
/// </summary>
internal record UploadPdfCommand(
    string? GameId,                    // Legacy: existing game ID (backward compatibility)
    PdfUploadMetadata? Metadata,       // New: game metadata for auto-creation
    Guid UserId,
    IFormFile File
) : ICommand<PdfUploadResult>;
