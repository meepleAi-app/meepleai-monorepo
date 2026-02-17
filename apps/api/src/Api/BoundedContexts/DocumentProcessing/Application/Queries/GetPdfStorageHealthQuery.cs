using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get PDF storage health across PostgreSQL, Qdrant, and file storage.
/// PDF Storage Management Hub: Composes existing resource metrics.
/// </summary>
internal record GetPdfStorageHealthQuery() : IQuery<PdfStorageHealthDto>;

internal record PdfStorageHealthDto(
    PostgresInfoDto Postgres,
    QdrantInfoDto Qdrant,
    FileStorageInfoDto FileStorage,
    string OverallHealth,
    DateTime MeasuredAt
);

internal record PostgresInfoDto(
    int TotalDocuments,
    int TotalChunks,
    double EstimatedChunksSizeMB
);

internal record QdrantInfoDto(
    long VectorCount,
    long MemoryBytes,
    string MemoryFormatted,
    bool IsAvailable
);

internal record FileStorageInfoDto(
    int TotalFiles,
    long TotalSizeBytes,
    string TotalSizeFormatted,
    Dictionary<string, int> SizeByState
);
