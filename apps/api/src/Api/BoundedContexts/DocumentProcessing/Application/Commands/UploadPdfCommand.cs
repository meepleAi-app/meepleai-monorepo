using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to upload a PDF document for a game.
/// </summary>
public record UploadPdfCommand(
    string GameId,
    Guid UserId,
    IFormFile File
) : ICommand<PdfUploadResult>;
