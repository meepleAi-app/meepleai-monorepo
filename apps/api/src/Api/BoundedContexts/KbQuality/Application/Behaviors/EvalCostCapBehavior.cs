using Api.BoundedContexts.KbQuality.Application.Authentication;
using Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Application.Services;
using Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KbQuality.Application.Behaviors;

/// <summary>
/// Pre-flight cost cap check for <see cref="StartEvaluationCommand"/> (D-H, plan amendment A1).
///
/// <para>Flow:</para>
/// <list type="number">
/// <item>Estimate the cost of the requested eval via <see cref="IEvaluationCostEstimator"/>.</item>
/// <item>Read the remaining tenant budget via <see cref="IEvalCostBudgetChecker"/> (self-contained
/// counter store per A1).</item>
/// <item>If <c>estimated &gt; remaining</c> and the caller did NOT request override, throw
/// <see cref="CostCapExceededException"/> — the endpoint maps this to <c>402 Payment Required</c>.</item>
/// <item>If override was requested but the caller is not an admin, throw
/// <see cref="UnauthorizedAccessException"/>.</item>
/// <item>Run the inner handler.</item>
/// <item>Increment the spent counter by the estimate. Acceptable estimate-vs-actual drift is noted in
/// the plan — the handler may correct via direct repository call upon completion (Task 14).</item>
/// </list>
///
/// <para>Identity extraction uses <see cref="KbQualityCurrentUser.FromHttpContext"/>, which reads
/// the project-standard <c>SessionStatusDto</c> from <c>HttpContext.Items</c> (mirrors
/// <c>AuditLoggingBehavior</c> and <c>TwoFactorEnforcementBehavior</c>). The literal
/// <c>ClaimsPrincipal</c> wording in the task description is approximate — the actual project
/// convention is the dual-principal session DTO.</para>
///
/// <para>Pattern: open-generic over <c>IRequest&lt;TResponse&gt;</c> with a runtime gate that
/// short-circuits to <c>next()</c> for any request that is not a <see cref="StartEvaluationCommand"/>.
/// Mirrors <c>TwoFactorEnforcementBehavior</c> which uses an attribute-based runtime gate for the
/// same reason: MediatR's <c>AddOpenBehavior</c> registration cannot accept a sealed-record
/// constraint, so the gate runs in <c>Handle</c>.</para>
/// </summary>
public sealed class EvalCostCapBehavior<TRequest, TResponse>(
    IEvalCostBudgetChecker budget,
    IEvaluationCostEstimator estimator,
    IHttpContextAccessor httpContext,
    ILogger<EvalCostCapBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        // Runtime gate: this behavior only applies to StartEvaluationCommand. Other requests
        // flow through untouched. (Open-generic registration via AddOpenBehavior cannot encode
        // a sealed-record constraint, so the type check happens here. Same pattern as
        // TwoFactorEnforcementBehavior's [RequireTwoFactor] gate.)
        if (request is not StartEvaluationCommand evalCommand)
        {
            return await next().ConfigureAwait(false);
        }

        var (_, tenantId, isAdmin) = KbQualityCurrentUser.FromHttpContext(httpContext.HttpContext);

        var estimated = await estimator.EstimateAsync(evalCommand.DocId, cancellationToken).ConfigureAwait(false);
        var remaining = await budget.GetRemainingAsync(tenantId, cancellationToken).ConfigureAwait(false);

        if (estimated > remaining && !evalCommand.OverrideCostCap)
        {
            logger.LogWarning(
                "KbQuality cost cap reject: estimated={Estimated} remaining={Remaining} doc={DocId}",
                estimated, remaining, evalCommand.DocId);
            throw new CostCapExceededException(estimated, remaining);
        }

        if (evalCommand.OverrideCostCap && !isAdmin)
        {
            throw new UnauthorizedAccessException("Override eval cost cap requires Admin role");
        }

        if (evalCommand.OverrideCostCap)
        {
            logger.LogWarning(
                "KbQuality cost cap OVERRIDDEN: tenant={TenantId} doc={DocId} estimated={Estimated} remaining={Remaining}",
                tenantId, evalCommand.DocId, estimated, remaining);
        }

        var result = await next().ConfigureAwait(false);

        // Increment uses estimated as upper bound. Plan acknowledges potential drift between
        // estimate and actual cost; the handler may correct via direct repo call upon completion.
        await budget.IncrementSpentAsync(tenantId, estimated, cancellationToken).ConfigureAwait(false);

        return result;
    }
}
