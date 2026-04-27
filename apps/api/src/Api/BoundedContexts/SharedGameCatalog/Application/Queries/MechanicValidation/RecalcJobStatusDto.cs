using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Read model returned by <see cref="GetRecalcJobStatusQuery"/>. Surfaces the live progress of an
/// asynchronous mechanic-recalc job to the admin UI's status-polling client (ADR-051 M2.1,
/// Sprint 2 / Task 9).
/// </summary>
/// <remarks>
/// <para>
/// All timestamps are <see cref="DateTimeOffset"/> in UTC, mirroring the aggregate. The DTO
/// intentionally exposes raw counters (Total/Processed/Failed/Skipped) so the client can render
/// progress bars, percentages, or per-bucket breakdowns without additional round trips.
/// </para>
/// <para>
/// <see cref="EtaSeconds"/> is a server-side projection: a rough completion estimate derived from
/// observed throughput (<c>(Total - Processed) * (elapsed / Processed)</c>). It is non-null only
/// when the job is <see cref="RecalcJobStatus.Running"/>, has at least one processed item, and has
/// a populated <c>StartedAt</c>. Callers must treat it as advisory — it is recomputed on every
/// fetch and naturally stabilises as more items are processed.
/// </para>
/// </remarks>
internal sealed record RecalcJobStatusDto(
    Guid Id,
    RecalcJobStatus Status,
    Guid TriggeredByUserId,
    int Total,
    int Processed,
    int Failed,
    int Skipped,
    int ConsecutiveFailures,
    string? LastError,
    bool CancellationRequested,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? HeartbeatAt,
    double? EtaSeconds);
