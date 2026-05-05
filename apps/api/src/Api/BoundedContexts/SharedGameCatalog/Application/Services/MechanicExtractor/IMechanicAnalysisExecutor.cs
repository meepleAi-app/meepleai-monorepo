namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Background executor that runs the full Mechanic Extractor pipeline for a persisted
/// <c>Draft</c> aggregate (ISSUE-524 / M1.2 decision B5=B — async 202 Accepted flow).
/// </summary>
/// <remarks>
/// The command handler creates the aggregate, persists it as <c>Draft</c>, then enqueues a
/// call to <see cref="ExecuteAsync"/> via <see cref="Api.Services.IBackgroundTaskService"/>.
/// The executor owns its own DI scope (obtained from <c>IServiceScopeFactory</c>) so the
/// HTTP request's scope can complete while the pipeline runs. All mutations — claim/citation
/// persistence, section-run rows, status transitions, cost-cap abort — are made against a
/// freshly-loaded aggregate and committed in a single <c>SaveChangesAsync</c> call, which
/// also publishes the domain events (InReview / Rejected).
/// </remarks>
public interface IMechanicAnalysisExecutor
{
    /// <summary>
    /// Runs the six-section pipeline for the given analysis id. Idempotent with respect to
    /// aggregate state: if the aggregate has already transitioned out of <c>Draft</c> (e.g.,
    /// due to a prior execution short-circuit), the method exits without further work.
    /// </summary>
    /// <param name="analysisId">Primary key of the <c>Draft</c> aggregate to process.</param>
    /// <param name="cancellationToken">Cancellation token supplied by the background task
    /// service. When cancelled, the executor marks the aggregate as <c>Rejected</c> with
    /// reason <c>LlmGenerationFailed</c> so operators see a terminal state rather than a
    /// stuck draft.</param>
    Task ExecuteAsync(Guid analysisId, CancellationToken cancellationToken);
}
