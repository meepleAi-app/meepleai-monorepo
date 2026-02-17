using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for GetPdfStorageHealthQuery.
/// PDF Storage Management Hub: Composes PG + Qdrant + file storage metrics.
/// </summary>
internal sealed class GetPdfStorageHealthQueryHandler
    : IQueryHandler<GetPdfStorageHealthQuery, PdfStorageHealthDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IQdrantClientAdapter _qdrantClient;
    private readonly TimeProvider _timeProvider;

    public GetPdfStorageHealthQueryHandler(
        MeepleAiDbContext dbContext,
        IQdrantClientAdapter qdrantClient,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<PdfStorageHealthDto> Handle(
        GetPdfStorageHealthQuery request, CancellationToken cancellationToken)
    {
        // PostgreSQL metrics
        var totalDocuments = await _dbContext.PdfDocuments
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var totalChunks = await _dbContext.TextChunks
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var totalChunkChars = await _dbContext.TextChunks
            .SumAsync(tc => (long)tc.CharacterCount, cancellationToken).ConfigureAwait(false);

        // Estimate chunk size: ~2 bytes per character (UTF-8 avg)
        var estimatedChunksSizeMB = (totalChunkChars * 2.0) / (1024.0 * 1024.0);

        var postgres = new PostgresInfoDto(totalDocuments, totalChunks, Math.Round(estimatedChunksSizeMB, 1));

        // Qdrant metrics (best-effort)
        QdrantInfoDto qdrant;
        var qdrantAvailable = true;
        try
        {
            var info = await _qdrantClient
                .GetCollectionInfoAsync("meepleai_documents", cancellationToken)
                .ConfigureAwait(false);

            var vectorCount = (long)info.PointsCount;
            var memoryBytes = vectorCount * 384 * 4L; // 384 dimensions * 4 bytes per float

            qdrant = new QdrantInfoDto(
                VectorCount: vectorCount,
                MemoryBytes: memoryBytes,
                MemoryFormatted: FormatBytes(memoryBytes),
                IsAvailable: true
            );
        }
        catch
        {
            qdrantAvailable = false;
            qdrant = new QdrantInfoDto(0, 0, "0 B", false);
        }

        // File storage metrics
        var totalSizeBytes = await _dbContext.PdfDocuments
            .SumAsync(p => p.FileSizeBytes, cancellationToken).ConfigureAwait(false);

        var sizeByState = await _dbContext.PdfDocuments
            .GroupBy(p => p.ProcessingState)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.State, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        var fileStorage = new FileStorageInfoDto(
            TotalFiles: totalDocuments,
            TotalSizeBytes: totalSizeBytes,
            TotalSizeFormatted: FormatBytes(totalSizeBytes),
            SizeByState: sizeByState
        );

        // Overall health determination
        var overallHealth = "healthy";
        if (!qdrantAvailable)
        {
            overallHealth = "critical";
        }
        else if (totalDocuments > 0)
        {
            // Warning if >10% of docs are in failed state
            sizeByState.TryGetValue("Failed", out var failedCount);
            if (failedCount > 0 && (double)failedCount / totalDocuments > 0.1)
            {
                overallHealth = "warning";
            }
        }

        return new PdfStorageHealthDto(
            Postgres: postgres,
            Qdrant: qdrant,
            FileStorage: fileStorage,
            OverallHealth: overallHealth,
            MeasuredAt: _timeProvider.GetUtcNow().DateTime
        );
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
