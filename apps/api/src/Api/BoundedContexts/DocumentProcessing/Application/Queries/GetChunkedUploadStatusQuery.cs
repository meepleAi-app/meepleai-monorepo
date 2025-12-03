using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get the status of a chunked upload session.
/// </summary>
public record GetChunkedUploadStatusQuery(
    Guid SessionId,
    Guid UserId
) : IQuery<ChunkedUploadStatusResult?>;

/// <summary>
/// Result containing chunked upload session status.
/// </summary>
public record ChunkedUploadStatusResult(
    Guid SessionId,
    string FileName,
    long TotalFileSize,
    int TotalChunks,
    int ReceivedChunks,
    double ProgressPercentage,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    IReadOnlyList<int> MissingChunks
);
