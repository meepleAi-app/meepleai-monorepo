using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Command and result in same file
namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to complete a chunked upload session and process the assembled PDF.
/// </summary>
public record CompleteChunkedUploadCommand(
    Guid SessionId,
    Guid UserId
) : ICommand<CompleteChunkedUploadResult>;

/// <summary>
/// Result of completing a chunked upload.
/// </summary>
public record CompleteChunkedUploadResult(
    bool Success,
    Guid? DocumentId,
    string? FileName,
    string? ErrorMessage,
    IReadOnlyList<int>? MissingChunks
);
