using System;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// A GameNight recap photo, with its blob URL already presigned for the caller.
/// Issue #2724.
/// </summary>
public record GameNightPhotoDto(
    Guid Id,
    string PhotoUrl,
    string? ThumbnailUrl,
    string? Caption,
    Guid UploadedByUserId,
    DateTimeOffset UploadedAt);
