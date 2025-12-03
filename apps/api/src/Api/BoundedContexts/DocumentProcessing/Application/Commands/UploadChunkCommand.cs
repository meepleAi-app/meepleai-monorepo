using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to upload a single chunk of a large PDF file.
/// </summary>
public record UploadChunkCommand(
    Guid SessionId,
    Guid UserId,
    int ChunkIndex,
    byte[] ChunkData
) : ICommand<UploadChunkResult>;

/// <summary>
/// Result of uploading a chunk.
/// </summary>
public record UploadChunkResult(
    bool Success,
    int ReceivedChunks,
    int TotalChunks,
    double ProgressPercentage,
    bool IsComplete,
    string? ErrorMessage
);
