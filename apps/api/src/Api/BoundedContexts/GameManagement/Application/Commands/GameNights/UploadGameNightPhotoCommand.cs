using System;
using System.IO;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Uploads a photo to a GameNight recap gallery (Issue #2724). Mirrors the
/// PlayRecord photo flow but persists a standalone entity via its own tracked
/// repository (the aggregate's detached Update cannot insert new children).
/// </summary>
internal record UploadGameNightPhotoCommand(
    Guid GameNightId,
    Guid UserId,
    Stream FileStream,
    long FileSizeBytes,
    string MimeType,
    bool ExtractScoreFromPhoto,
    string? Caption
) : ICommand<GameNightPhotoUploadResult>;

internal record GameNightPhotoUploadResult(
    Guid PhotoId,
    string PhotoUrl,
    string? ThumbnailUrl,
    string? OcrText,
    bool WasDeduplicated);
