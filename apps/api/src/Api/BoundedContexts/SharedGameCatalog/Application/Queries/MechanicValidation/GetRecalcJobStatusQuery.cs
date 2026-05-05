using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Loads the live status of a <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicRecalcJob"/>
/// by id (ADR-051 M2.1, Sprint 2 / Task 9).
/// </summary>
/// <remarks>
/// <para>
/// Used by the admin UI's status-polling loop and by the endpoint surfaced for the client-side
/// progress bar. Returns a <see cref="RecalcJobStatusDto"/> snapshot of the aggregate state plus a
/// computed <c>EtaSeconds</c> projection.
/// </para>
/// <para>
/// Intentionally <b>not</b> cached: the aggregate's counters and heartbeat timestamp mutate on
/// every worker iteration (sub-second cadence under load), so any cache TTL would either be too
/// stale to be useful or so short that it adds overhead without benefit. The endpoint expects to
/// hit the database on every poll.
/// </para>
/// <para>
/// The handler raises <see cref="Api.Middleware.Exceptions.NotFoundException"/> when no job with
/// the given id exists, so the endpoint surface returns HTTP 404 consistently with the rest of the
/// validation API.
/// </para>
/// </remarks>
internal sealed record GetRecalcJobStatusQuery(Guid JobId) : IQuery<RecalcJobStatusDto>;
