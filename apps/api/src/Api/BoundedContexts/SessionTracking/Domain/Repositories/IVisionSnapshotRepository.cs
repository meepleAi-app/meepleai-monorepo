using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

/// <summary>
/// Repository interface for VisionSnapshot persistence operations.
/// Session Vision AI feature.
/// </summary>
internal interface IVisionSnapshotRepository
{
    Task<VisionSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<VisionSnapshot>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<VisionSnapshot?> GetLatestBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task AddAsync(VisionSnapshot snapshot, CancellationToken ct = default);
    Task UpdateAsync(VisionSnapshot snapshot, CancellationToken ct = default);
    Task<int> CountBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
