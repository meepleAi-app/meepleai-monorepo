using MediatR;

namespace Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;

/// <summary>
/// Starts an asynchronous per-document quality evaluation run (#1675).
/// Pre-flight validation (cost cap + rate limit) is enforced by
/// <see cref="Behaviors.EvalCostCapBehavior{TRequest,TResponse}"/> (D-H) and the
/// rate-limit behavior (Task 12). Full handler ships in Task 14.
///
/// <para>Fields:</para>
/// <list type="bullet">
/// <item><c>DocId</c> — target <c>PdfDocumentEntity.Id</c></item>
/// <item><c>GoldsetVersion</c> — pinned goldset semver (null = registry default per D-G)</item>
/// <item><c>OverrideCostCap</c> — admin-only opt-out from the per-tenant monthly cap (D-H, A1)</item>
/// </list>
/// </summary>
public sealed record StartEvaluationCommand(
    Guid DocId,
    string? GoldsetVersion,
    bool OverrideCostCap) : IRequest<EvaluationStartedResult>;

/// <summary>
/// Synchronous accept response for <see cref="StartEvaluationCommand"/>. The eval itself
/// runs out-of-band; the caller polls a status endpoint keyed on <see cref="EvaluationId"/>.
///
/// <para>Quota headers (D-H) are surfaced here so the endpoint can map them into HTTP response
/// headers (<c>X-RateLimit-Remaining</c>, <c>X-RateLimit-Reset</c>, <c>X-Cost-Cap-Remaining</c>).</para>
/// </summary>
public sealed record EvaluationStartedResult(
    Guid EvaluationId,
    DateTime LocationCreatedAt,
    int RateLimitRemaining,
    DateTime RateLimitReset,
    decimal CostCapRemaining,
    decimal CostCapEstimate);
