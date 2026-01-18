using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to upload a custom PDF rulebook for a game in user's library.
/// Replaces any existing custom PDF.
/// </summary>
/// <param name="UserId">The user who owns the library entry</param>
/// <param name="GameId">The game to upload PDF for</param>
/// <param name="PdfUrl">URL where the PDF is stored</param>
/// <param name="FileSizeBytes">Size of the PDF file in bytes</param>
/// <param name="OriginalFileName">Original filename provided by user</param>
internal record UploadCustomGamePdfCommand(
    Guid UserId,
    Guid GameId,
    string PdfUrl,
    long FileSizeBytes,
    string OriginalFileName
) : ICommand<UserLibraryEntryDto>;
