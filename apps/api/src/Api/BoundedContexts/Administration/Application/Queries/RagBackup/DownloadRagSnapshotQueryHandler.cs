using Api.BoundedContexts.Administration.Application.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.RagBackup;

internal sealed class DownloadRagSnapshotQueryHandler
    : IRequestHandler<DownloadRagSnapshotQuery, DownloadRagSnapshotResult>
{
    private readonly IRagBackupStorageService _storage;
    private readonly IRagExportService _exportService;
    private readonly ILogger<DownloadRagSnapshotQueryHandler> _logger;

    public DownloadRagSnapshotQueryHandler(
        IRagBackupStorageService storage,
        IRagExportService exportService,
        ILogger<DownloadRagSnapshotQueryHandler> logger)
    {
        _storage = storage;
        _exportService = exportService;
        _logger = logger;
    }

    public async Task<DownloadRagSnapshotResult> Handle(
        DownloadRagSnapshotQuery query,
        CancellationToken cancellationToken)
    {
        // List mode: no SnapshotId provided
        if (query.SnapshotId is null)
        {
            return await ListSnapshotsAsync(cancellationToken).ConfigureAwait(false);
        }

        // Download mode: return pre-signed URL for snapshot or game-specific directory
        return await GetDownloadUrlAsync(query.SnapshotId, query.GameSlug, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DownloadRagSnapshotResult> ListSnapshotsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Listing available RAG backup snapshots");

        var snapshotIds = await _storage.ListSnapshotsAsync(ct).ConfigureAwait(false);

        var snapshots = new List<SnapshotInfo>(snapshotIds.Count);

        foreach (var id in snapshotIds)
        {
            try
            {
                var manifest = await _exportService.ReadManifestAsync(id, ct).ConfigureAwait(false);

                snapshots.Add(new SnapshotInfo(
                    Id: id,
                    ExportedAt: manifest?.ExportedAt.UtcDateTime,
                    TotalDocuments: manifest?.TotalDocuments,
                    TotalChunks: manifest?.TotalChunks));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read manifest for snapshot {SnapshotId}, including with null metadata", id);

                snapshots.Add(new SnapshotInfo(
                    Id: id,
                    ExportedAt: null,
                    TotalDocuments: null,
                    TotalChunks: null));
            }
        }

        _logger.LogInformation("Found {Count} RAG backup snapshots", snapshots.Count);

        return new DownloadRagSnapshotResult(Snapshots: snapshots);
    }

    private async Task<DownloadRagSnapshotResult> GetDownloadUrlAsync(
        string snapshotId,
        string? gameSlug,
        CancellationToken ct)
    {
        string path = gameSlug is not null
            ? $"rag-exports/{snapshotId}/games/{gameSlug}/"
            : $"rag-exports/{snapshotId}/manifest.json";

        _logger.LogInformation(
            "Generating pre-signed download URL for snapshot {SnapshotId}, path: {Path}",
            snapshotId, path);

        var url = await _storage.GetDownloadUrlAsync(path, ct: ct).ConfigureAwait(false);

        if (url is null)
        {
            _logger.LogWarning(
                "Could not generate download URL for snapshot {SnapshotId} path {Path} — S3 not configured or object not found",
                snapshotId, path);

            return new DownloadRagSnapshotResult(
                Error: "Download URL unavailable: S3 is not configured or the requested snapshot path was not found.");
        }

        return new DownloadRagSnapshotResult(DownloadUrl: url);
    }
}
