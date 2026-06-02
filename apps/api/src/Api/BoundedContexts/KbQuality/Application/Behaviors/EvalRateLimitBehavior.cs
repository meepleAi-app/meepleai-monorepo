using Api.BoundedContexts.KbQuality.Application.Authentication;
using Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;
using Api.BoundedContexts.KbQuality.Application.Configuration;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KbQuality.Application.Behaviors;

/// <summary>
/// Sliding-window rate limit for <see cref="StartEvaluationCommand"/>: at most 1 eval per
/// (docId, adminId) per <c>EvalQuality:RateLimitPerDocMinutes</c> (default 10min).
///
/// <para>Pattern mirrors <see cref="EvalCostCapBehavior{TRequest,TResponse}"/>: open-generic
/// over <c>IRequest&lt;TResponse&gt;</c> with a runtime gate that short-circuits non-eval
/// requests. Identity extraction via <see cref="KbQualityCurrentUser.FromHttpContext"/>.</para>
///
/// <para>Throws <see cref="EvalRateLimitedException"/> when the window has not yet elapsed —
/// the endpoint maps this to <c>429 Too Many Requests</c> with <c>Retry-After</c>.</para>
///
/// <para>Registration order matters: register this BEFORE <see cref="EvalCostCapBehavior{TRequest,TResponse}"/>
/// so that rate-limited requests do not consume budget estimate calls.</para>
/// </summary>
public sealed class EvalRateLimitBehavior<TRequest, TResponse>(
    IEvaluationRateLimitStore store,
    IHttpContextAccessor httpContext,
    IOptionsMonitor<EvalQualityOptions> options
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

        if (request is not StartEvaluationCommand evalCommand)
        {
            return await next().ConfigureAwait(false);
        }

        var (userId, _, _) = KbQualityCurrentUser.FromHttpContext(httpContext.HttpContext);
        var window = TimeSpan.FromMinutes(options.CurrentValue.RateLimitPerDocMinutes);

        var lastStartedAt = await store
            .GetLastStartedAtAsync(evalCommand.DocId, userId, window, cancellationToken)
            .ConfigureAwait(false);

        if (lastStartedAt is { } last)
        {
            var elapsed = DateTime.UtcNow - last;
            if (elapsed < window)
            {
                throw new EvalRateLimitedException(window - elapsed);
            }
        }

        return await next().ConfigureAwait(false);
    }
}
