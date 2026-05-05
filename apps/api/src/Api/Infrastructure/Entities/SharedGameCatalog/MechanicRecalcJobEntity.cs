using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities;

namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicRecalcJob"/>.
/// Plain POCO — all invariants are enforced by the domain aggregate; this type exists only to be
/// shaped by EF Core. Column names mirror the SQL DDL in migration M2_1_MechanicRecalcJobs.
/// </summary>
public class MechanicRecalcJobEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Persisted as an integer: 0=Pending, 1=Running, 2=Completed, 3=Failed, 4=Cancelled.
    /// EF maps the <see cref="RecalcJobStatus"/> enum to/from int natively.
    /// </summary>
    public RecalcJobStatus Status { get; set; }

    public Guid TriggeredByUserId { get; set; }

    public int Total { get; set; }
    public int Processed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int ConsecutiveFailures { get; set; }

    public string? LastError { get; set; }
    public Guid? LastProcessedAnalysisId { get; set; }

    public bool CancellationRequested { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? HeartbeatAt { get; set; }

    // === Navigation ===
    public UserEntity TriggeredByUser { get; set; } = default!;
}
