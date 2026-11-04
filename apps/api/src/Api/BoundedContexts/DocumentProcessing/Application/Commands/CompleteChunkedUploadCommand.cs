using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Command and result in same file
namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to complete a chunked upload session and process the assembled PDF.
/// </summary>
/// <remarks>
/// <c>Priority</c>: "normal" | "urgent" | null (normal) — the processing job's priority (#4443 R2). The
/// chunked routes are admin and editor only, so no role check is needed here.
/// </remarks>
internal record CompleteChunkedUploadCommand(
    Guid SessionId,
    Guid UserId,
    string? Priority = null
) : ICommand<CompleteChunkedUploadResult>;

/// <summary>
/// Result of completing a chunked upload.
/// </summary>
internal record CompleteChunkedUploadResult(
    bool Success,
    Guid? DocumentId,
    string? FileName,
    string? ErrorMessage,
    IReadOnlyList<int>? MissingChunks
);
