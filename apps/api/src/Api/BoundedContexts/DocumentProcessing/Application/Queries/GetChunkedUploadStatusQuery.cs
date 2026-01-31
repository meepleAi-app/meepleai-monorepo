using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get the status of a chunked upload session.
/// </summary>
internal record GetChunkedUploadStatusQuery(
    Guid SessionId,
    Guid UserId
) : IQuery<ChunkedUploadStatusResult?>;

/// <summary>
/// Result containing chunked upload session status.
/// </summary>
internal record ChunkedUploadStatusResult(
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
