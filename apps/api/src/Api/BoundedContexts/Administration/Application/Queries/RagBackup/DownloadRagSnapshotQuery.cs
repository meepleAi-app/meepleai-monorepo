using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.RagBackup;

internal sealed record DownloadRagSnapshotQuery(
    string? SnapshotId = null,
    string? GameSlug = null
) : IRequest<DownloadRagSnapshotResult>;

internal sealed record DownloadRagSnapshotResult(
    List<SnapshotInfo>? Snapshots = null,
    string? DownloadUrl = null,
    string? Error = null);

internal sealed record SnapshotInfo(
    string Id, DateTime? ExportedAt, int? TotalDocuments, int? TotalChunks);
