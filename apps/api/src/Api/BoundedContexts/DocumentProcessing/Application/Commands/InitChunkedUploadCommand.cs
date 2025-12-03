using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to initialize a chunked upload session for a large PDF file.
/// </summary>
public record InitChunkedUploadCommand(
    Guid GameId,
    Guid UserId,
    string FileName,
    long TotalFileSize
) : ICommand<InitChunkedUploadResult>;

/// <summary>
/// Result of initializing a chunked upload session.
/// </summary>
public record InitChunkedUploadResult(
    bool Success,
    Guid? SessionId,
    int TotalChunks,
    int ChunkSizeBytes,
    DateTime? ExpiresAt,
    string? ErrorMessage
);
