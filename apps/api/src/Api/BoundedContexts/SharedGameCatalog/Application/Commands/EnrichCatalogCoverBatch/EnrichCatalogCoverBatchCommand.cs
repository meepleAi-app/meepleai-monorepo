using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCoverBatch;

/// <summary>
/// Issue #2123 (Phase B + Phase C) — wraps the M8 single-entry
/// <see cref="EnrichCatalogCover.EnrichCatalogCoverCommand"/> in a one-shot
/// batch dispatcher used by the admin endpoint
/// <c>POST /api/v1/admin/catalog/covers/enrich-batch</c>.
/// </summary>
/// <remarks>
/// <para>
/// The handler iterates <paramref name="GameIds"/> in order, dispatching one
/// child command per id through <see cref="MediatR.IMediator"/>. Per-game
/// outcomes are independent — an unhandled exception inside one M8 invocation
/// is captured as a <c>failed/unhandled-exception</c> entry and the batch
/// continues. Cancellation aborts the loop immediately and surfaces an
/// <see cref="OperationCanceledException"/> to the caller.
/// </para>
/// <para>
/// Rate-limit posture: the M8 handler already enforces the 1 req/sec Wikimedia
/// SPARQL cap via the shared <c>IWikimediaRateLimiter</c>. This batch wrapper
/// adds NO additional throttling — it is the admin's responsibility to chunk
/// invocations if needed. The validator caps the input list at 200 entries to
/// keep a single batch's worst-case duration under ~10 minutes.
/// </para>
/// <para>
/// Idempotency: every child command is idempotent (deterministic R2 key, ADR
/// DEC-3i freshness window). A retry of the full batch is therefore safe.
/// </para>
/// </remarks>
internal sealed record EnrichCatalogCoverBatchCommand(IReadOnlyList<Guid> GameIds)
    : ICommand<EnrichCatalogCoverBatchResult>;
