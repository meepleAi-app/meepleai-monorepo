using Api.BoundedContexts.KbQuality.Application.Authentication;
using Api.BoundedContexts.KbQuality.Application.Configuration;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Application.Services;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;
using Api.BoundedContexts.KbQuality.Domain.Goldset;
using Api.BoundedContexts.KbQuality.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;

/// <summary>
/// Synchronous orchestrator for a per-doc evaluation lifecycle (#1675 Task 14).
///
/// <para>Pre-flight gates (cost cap, rate limit) run as MediatR pipeline behaviors
/// BEFORE this handler executes — see <see cref="Behaviors.EvalRateLimitBehavior{TRequest,TResponse}"/>
/// and <see cref="Behaviors.EvalCostCapBehavior{TRequest,TResponse}"/>. By the time
/// <see cref="Handle"/> runs, the request is authorised and budgeted.</para>
///
/// <para>Lifecycle (per design doc §3.3):</para>
/// <list type="number">
///   <item>Resolve the goldset version against the in-process registry.</item>
///   <item>Load the PDF doc snapshot (404 if missing).</item>
///   <item>Look up a deterministic seed reuse (24h window on same doc+goldsetVersion).</item>
///   <item>Persist a <see cref="DocumentEvaluationRun"/> in <c>Pending</c>.</item>
///   <item>Transition to <c>GoldsetGenerating</c>, generate the goldset.</item>
///   <item>Transition to <c>Running</c>, execute the eval against the KB.</item>
///   <item>Mark <c>Completed</c> with metrics + total cost, or <c>Failed</c> on any error.</item>
/// </list>
///
/// <para>Identity (plan amendment A2): the project has no <c>ICurrentUserService</c>; we
/// extract <c>(UserId, TenantId, IsAdmin)</c> via <see cref="KbQualityCurrentUser.FromHttpContext"/>,
/// which reads the project-standard <c>SessionStatusDto</c> from <c>HttpContext.Items</c>.</para>
///
/// <para>Robustness note: the try/catch envelopes BOTH goldset generation AND eval execution
/// so an LLM failure cannot leave a row orphaned in <c>GoldsetGenerating</c>. This deviates
/// from the plan text (which scopes the try to the executor only) — discussed in the commit
/// message; rationale is to preserve aggregate state-machine invariant
/// "no run stuck in a non-terminal state on uncaught exception."</para>
/// </summary>
public sealed class StartEvaluationCommandHandler(
    IPdfDocumentReadModel pdfRepo,
    IGoldsetGenerator goldsetGen,
    IEvaluationExecutor executor,
    IEvaluationRepository runRepo,
    IHttpContextAccessor httpContextAccessor,
    IOptionsMonitor<EvalQualityOptions> options,
    ILogger<StartEvaluationCommandHandler> logger
) : IRequestHandler<StartEvaluationCommand, EvaluationStartedResult>
{
    public async Task<EvaluationStartedResult> Handle(
        StartEvaluationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (userId, _, _) = KbQualityCurrentUser.FromHttpContext(httpContextAccessor.HttpContext);
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("StartEvaluation requires an authenticated admin session");
        }

        // 1. Resolve goldset version
        var requestedVersion = request.GoldsetVersion ?? GoldsetVersion.AutoCurrent.Version;
        if (!GoldsetVersion.TryGet(requestedVersion, out var goldsetVer))
        {
            throw new InvalidGoldsetVersionException(
                requestedVersion,
                GoldsetVersion.Registry.Select(v => v.Version).ToArray());
        }

        // 2. Load PDF doc snapshot
        var pdf = await pdfRepo.GetSnapshotAsync(request.DocId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("PdfDocument", request.DocId.ToString());

        // 3. Determine seed: reuse latest within 24h on same (docId, goldsetVersion); else null = random.
        var existingSeed = await runRepo
            .GetLatestSeedAsync(request.DocId, goldsetVer.Version, TimeSpan.FromHours(24), cancellationToken)
            .ConfigureAwait(false);

        var run = DocumentEvaluationRun.Create(
            pdfDocumentId: request.DocId,
            goldsetVersion: goldsetVer.Version,
            triggeredByAdminId: userId,
            reuseSeed: existingSeed);

        await runRepo.AddAsync(run, cancellationToken).ConfigureAwait(false);
        await runRepo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 4. Goldset generation + eval execution. Single try-catch covers both so a generator
        //    failure (LLM timeout, malformed JSON, etc.) marks the run Failed rather than
        //    leaving it orphaned in GoldsetGenerating.
        try
        {
            run.TransitionTo(EvaluationStatus.GoldsetGenerating);
            await runRepo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var goldset = await goldsetGen
                .GenerateAsync(pdf, run.GoldsetGenerationSeed, cancellationToken)
                .ConfigureAwait(false);

            run.TransitionTo(EvaluationStatus.Running);
            await runRepo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var outcome = await executor
                .ExecuteAsync(request.DocId, pdf, goldset.Pairs, run.GoldsetGenerationSeed, cancellationToken)
                .ConfigureAwait(false);

            var totalCost = goldset.CostUsd + outcome.AdditionalCostUsd;
            var finalMetrics = outcome.Metrics with { CostUsd = totalCost };
            run.MarkCompleted(finalMetrics, totalCost);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "KbQuality eval failed for doc {DocId} run {RunId}", request.DocId, run.Id);
            run.MarkFailed(ex.Message);
        }

        await runRepo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Quota headers (RateLimitRemaining, CostCapRemaining) are populated by the pipeline
        // behaviors out-of-band; the endpoint will overlay them onto the HTTP response headers.
        return new EvaluationStartedResult(
            EvaluationId: run.Id,
            LocationCreatedAt: run.StartedAt,
            RateLimitRemaining: 0,
            RateLimitReset: DateTime.UtcNow.AddMinutes(options.CurrentValue.RateLimitPerDocMinutes),
            CostCapRemaining: 0m,
            CostCapEstimate: run.CostUsd ?? 0m);
    }
}
