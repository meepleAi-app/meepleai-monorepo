using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to upload game images (icon or cover) to storage.
/// Issue #2255: File upload implementation for game creation wizard.
/// </summary>
internal record UploadGameImageCommand(
    Stream FileStream,
    string FileName,
    string GameId,
    ImageType ImageType
) : ICommand<UploadGameImageResult>;

/// <summary>
/// Type of game image being uploaded.
/// </summary>
public enum ImageType
{
    /// <summary>
    /// Small icon/thumbnail (max 2MB, recommended 128x128 or 256x256)
    /// </summary>
    Icon,

    /// <summary>
    /// Cover/hero image (max 5MB, recommended 800x600 or larger)
    /// </summary>
    Image
}
