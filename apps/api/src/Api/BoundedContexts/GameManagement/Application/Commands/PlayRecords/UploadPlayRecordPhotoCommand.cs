using System;
using System.IO;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

internal record UploadPlayRecordPhotoCommand(
    Guid RecordId,
    Guid UserId,
    Stream FileStream,
    long FileSizeBytes,
    string MimeType,
    bool ExtractScoreFromPhoto,
    string? Caption
) : ICommand<PlayRecordPhotoUploadResult>;

internal record PlayRecordPhotoUploadResult(
    Guid PhotoId,
    string PhotoUrl,
    string? ThumbnailUrl,
    string? OcrText,
    bool WasDeduplicated);
