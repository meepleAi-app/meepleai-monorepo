namespace Api.BoundedContexts.Administration.Domain.Entities;

public sealed class DatabaseMetricsSnapshot
{
    public Guid Id { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public long TotalSizeBytes { get; private set; }
    public int TableCount { get; private set; }
    public long IndexSizeBytes { get; private set; }
    public int ActiveConnections { get; private set; }

    public static DatabaseMetricsSnapshot Create(
        long totalSizeBytes, int tableCount, long indexSizeBytes, int activeConnections)
    {
        return new DatabaseMetricsSnapshot
        {
            Id = Guid.NewGuid(),
            RecordedAt = DateTime.UtcNow,
            TotalSizeBytes = totalSizeBytes,
            TableCount = tableCount,
            IndexSizeBytes = indexSizeBytes,
            ActiveConnections = activeConnections
        };
    }
}
