using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get PDF storage health across PostgreSQL (pgvector) and file storage.
/// PDF Storage Management Hub: Composes existing resource metrics.
/// </summary>
internal record GetPdfStorageHealthQuery() : IQuery<PdfStorageHealthDto>;

internal record PdfStorageHealthDto(
    PostgresInfoDto Postgres,
    VectorStoreInfoDto VectorStore,
    FileStorageInfoDto FileStorage,
    string OverallHealth,
    DateTime MeasuredAt
);

internal record PostgresInfoDto(
    int TotalDocuments,
    int TotalChunks,
    double EstimatedChunksSizeMB
);

internal record VectorStoreInfoDto(
    long VectorCount,
    bool IsAvailable
);

internal record FileStorageInfoDto(
    int TotalFiles,
    long TotalSizeBytes,
    string TotalSizeFormatted,
    Dictionary<string, int> SizeByState
);
