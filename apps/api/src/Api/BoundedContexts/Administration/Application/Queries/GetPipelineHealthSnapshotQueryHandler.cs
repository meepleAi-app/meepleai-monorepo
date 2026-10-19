using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for <see cref="GetPipelineHealthSnapshotQuery"/>: four queries, sent one at a time, and
/// the embedding metrics, read alongside them outside the MediatR pipeline.
/// </summary>
internal sealed class GetPipelineHealthSnapshotQueryHandler
    : IQueryHandler<GetPipelineHealthSnapshotQuery, PipelineHealthSnapshot>
{
    private readonly IMediator _mediator;
    private readonly IEmbeddingServiceMetricsReader _embeddingMetrics;

    public GetPipelineHealthSnapshotQueryHandler(
        IMediator mediator,
        IEmbeddingServiceMetricsReader embeddingMetrics)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _embeddingMetrics = embeddingMetrics ?? throw new ArgumentNullException(nameof(embeddingMetrics));
    }

    public async Task<PipelineHealthSnapshot> Handle(
        GetPipelineHealthSnapshotQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // CQRS #3176: infrastructure health and step metrics are fetched via IMediator
        // (pass-through queries) instead of injecting the services.
        //
        // 🔴 One send at a time (#4059). These four queries used to be started together and awaited
        // with Task.WhenAll, and /admin/kb/pipeline/health then answered 500 on EVERY call. Measured
        // 2026-10-04 on the local stack, three consecutive requests, all 500:
        //     System.InvalidOperationException: A second operation was started on this context
        //     instance before a previous operation completed.
        //       at GetPdfStorageHealthQueryHandler.Handle(GetPdfStorageHealthQuery, ...)
        //       at AdminPipelineEndpoints.GetPipelineHealth(...)
        // GetStepDurationStats, GetPdfStorageHealth and GetProcessingQueue all read through the
        // SAME request-scoped MeepleAiDbContext, which EF Core forbids using concurrently.
        //
        // The queries are serialized rather than given a context each, and ALL FOUR are serialized
        // rather than only the three that read EF today, because "this send does not touch the
        // DbContext" is a property of the whole MediatR pipeline, not of the handler: every send
        // also runs AuditLoggingBehavior, which holds the request-scoped context and uses it on the
        // [AuditableAction] path — on queries too: GetDocumentEmbeddingsMetaQuery carries the
        // attribute. A behaviour added later, or an attribute added to one of these queries, would
        // silently re-open the defect. One send at a time has no such precondition.
        //
        // Same defect and same resolution as #3843 (GetDashboardQueryHandler), where the parallel
        // version answered 200 with EMPTY sections instead of 500 — quieter, not better. As there:
        // if the sequential reads ever breach a latency target, the fix is one DbContext per query
        // (IDbContextFactory), not shared-context parallelism.
        //
        // #4512: the embedding /metrics read is NOT a fifth send, and that is why it can start first
        // and run alongside the four. As a send it would have to wait its turn like them, and with
        // the embedding service down the endpoint would wait for its timeout on top of the queries.
        // The reader is plain HTTP over IHttpClientFactory — no DbContext, no MediatR — and its
        // interface says it must stay so.
        //
        // ⚠️ #4059 was first attributed to the health checks, on the reasoning that
        // DefaultHealthCheckService runs them in parallel and three of them inject the scoped
        // MeepleAiDbContext. That is NOT the cause: the service opens a DI scope per check, so each
        // gets a context of its own — measured in HealthCheckScopePerCheckContractTests, and visible
        // in the log of the request that answered 500, where every health check completed normally.
        // Do not "fix" those three checks on the strength of this endpoint's 500.
        var embeddingMetricsTask = _embeddingMetrics.ReadAsync(cancellationToken);

        var allHealth = await _mediator.Send(new GetServiceHealthStatusesQuery(), cancellationToken).ConfigureAwait(false);
        var stepMetrics = await _mediator.Send(new GetStepDurationStatsQuery(), cancellationToken).ConfigureAwait(false);
        var storageHealth = await _mediator.Send(new GetPdfStorageHealthQuery(), cancellationToken).ConfigureAwait(false);
        var queueResult = await _mediator.Send(new GetProcessingQueueQuery(
            StatusFilter: null,
            SearchText: null,
            FromDate: null,
            ToDate: null,
            Page: 1,
            PageSize: 10), cancellationToken).ConfigureAwait(false);

        var embeddingMetrics = await embeddingMetricsTask.ConfigureAwait(false);

        return new PipelineHealthSnapshot(allHealth, stepMetrics, storageHealth, queueResult, embeddingMetrics);
    }
}
