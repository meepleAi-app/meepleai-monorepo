using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;

/// <summary>
/// Command to upload a user-custom cover image for a game in their library (L3).
/// Issue #1824 (umbrella #1821, cover stack L3).
/// </summary>
internal record UploadCustomCoverCommand(
    Guid UserId,
    Guid GameId,
    Stream FileStream,
    long FileSizeBytes,
    string MimeType
) : ICommand<CustomCoverUploadResult>;
