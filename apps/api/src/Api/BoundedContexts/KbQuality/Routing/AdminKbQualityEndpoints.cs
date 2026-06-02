using System.Globalization;
using Api.BoundedContexts.KbQuality.Application.Behaviors;
using Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;
using Api.BoundedContexts.KbQuality.Application.Queries.GetEvaluation;
using Api.BoundedContexts.KbQuality.Application.Queries.ListEvaluations;
using Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.BoundedContexts.KbQuality.Routing;

/// <summary>
/// Admin REST surface for per-doc evaluations (#1675 Task 19).
///
/// <para>Route group is mounted under <c>/api/v1</c> via the caller (Program.cs) so the
/// effective paths are <c>POST /api/v1/admin/kb/docs/{docId}/evaluations</c>,
/// <c>GET /api/v1/admin/kb/docs/{docId}/evaluations</c>, and
/// <c>GET /api/v1/admin/kb/docs/{docId}/evaluations/{evaluationId}</c>.</para>
///
/// <para>Authorization is delegated to the <c>RequireAdminOrAbove</c> policy registered by
/// <see cref="Api.Extensions.AuthenticationServiceExtensions"/>. The MediatR pipeline behaviors
/// (rate limit + cost cap) gate the actual evaluation start.</para>
///
/// <para>Status code mapping:</para>
/// <list type="bullet">
///   <item><see cref="InvalidGoldsetVersionException"/> → 400 Bad Request</item>
///   <item><see cref="CostCapExceededException"/> → 402 Payment Required (admin can retry
///         with <c>overrideCostCap=true</c>)</item>
///   <item><see cref="EvalRateLimitedException"/> → 429 Too Many Requests with the
///         <c>Retry-After</c> header in seconds (per RFC 9110)</item>
///   <item>Non-terminal eval state on GET → 423 Locked so polling clients can distinguish
///         "still working" from "really gone".</item>
/// </list>
/// </summary>
public static class AdminKbQualityEndpoints
{
    public static IEndpointRouteBuilder MapAdminKbQualityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/admin/kb/docs/{docId:guid}/evaluations")
            .RequireAuthorization("RequireAdminOrAbove")
            .WithTags("Admin KB Quality");

        group.MapPost("", StartEvaluation).WithName("StartKbQualityEvaluation");
        group.MapGet("", ListEvaluations).WithName("ListKbQualityEvaluations");
        group.MapGet("{evaluationId:guid}", GetEvaluation).WithName("GetKbQualityEvaluation");

        return app;
    }

    public sealed record StartEvaluationRequestBody(string? GoldsetVersion, bool OverrideCostCap = false);

    private static async Task<IResult> StartEvaluation(
        Guid docId,
        [FromBody] StartEvaluationRequestBody body,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        try
        {
            var result = await mediator.Send(
                new StartEvaluationCommand(docId, body.GoldsetVersion, body.OverrideCostCap),
                ct).ConfigureAwait(false);

            // Overlay the live quota values that the pipeline behaviors wrote into
            // HttpContext.Items so the response body + standard headers reflect actuals
            // rather than the handler's hardcoded zeros.
            var enriched = OverlayQuotaFromHttpItems(result, httpContext);
            WriteQuotaHeaders(httpContext, enriched);

            return Results.Accepted(
                uri: $"/api/v1/admin/kb/docs/{docId}/evaluations/{enriched.EvaluationId}",
                value: enriched);
        }
        catch (InvalidGoldsetVersionException ex)
        {
            return Results.BadRequest(new
            {
                error = "InvalidGoldsetVersion",
                message = ex.Message,
                requested = ex.RequestedVersion,
                available = ex.AvailableVersions,
            });
        }
        catch (CostCapExceededException ex)
        {
            return Results.Json(
                new
                {
                    error = "CostCapExceeded",
                    estimated = ex.EstimatedCostUsd,
                    remaining = ex.RemainingBudgetUsd,
                    hint = "Retry with overrideCostCap=true (admin role required).",
                },
                statusCode: StatusCodes.Status402PaymentRequired);
        }
        catch (EvalRateLimitedException ex)
        {
            var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(ex.RetryAfter.TotalSeconds));
            httpContext.Response.Headers["Retry-After"] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

            return Results.Json(
                new
                {
                    error = "RateLimited",
                    retryAfterSeconds,
                },
                statusCode: StatusCodes.Status429TooManyRequests);
        }
    }

    private static EvaluationStartedResult OverlayQuotaFromHttpItems(
        EvaluationStartedResult result,
        HttpContext httpContext)
    {
        var items = httpContext.Items;
        var rateLimitRemaining = items[KbQualityHttpItemKeys.RateLimitRemaining] is int rlr
            ? rlr
            : result.RateLimitRemaining;
        var rateLimitReset = items[KbQualityHttpItemKeys.RateLimitReset] is DateTime rls
            ? rls
            : result.RateLimitReset;
        var costCapRemaining = items[KbQualityHttpItemKeys.CostCapRemaining] is decimal ccr
            ? ccr
            : result.CostCapRemaining;
        var costCapEstimate = items[KbQualityHttpItemKeys.CostCapEstimate] is decimal cce
            ? cce
            : result.CostCapEstimate;

        return result with
        {
            RateLimitRemaining = rateLimitRemaining,
            RateLimitReset = rateLimitReset,
            CostCapRemaining = costCapRemaining,
            CostCapEstimate = costCapEstimate,
        };
    }

    private static void WriteQuotaHeaders(HttpContext httpContext, EvaluationStartedResult result)
    {
        httpContext.Response.Headers["X-RateLimit-Remaining"] =
            result.RateLimitRemaining.ToString(CultureInfo.InvariantCulture);
        httpContext.Response.Headers["X-RateLimit-Reset"] =
            result.RateLimitReset.ToString("o", CultureInfo.InvariantCulture);
        httpContext.Response.Headers["X-Cost-Cap-Remaining"] =
            result.CostCapRemaining.ToString(CultureInfo.InvariantCulture);
    }

    private static async Task<IResult> GetEvaluation(
        Guid docId,
        Guid evaluationId,
        IMediator mediator,
        CancellationToken ct)
    {
        var dto = await mediator.Send(new GetEvaluationQuery(docId, evaluationId), ct).ConfigureAwait(false);
        if (dto is null)
        {
            return Results.NotFound();
        }

        // 423 Locked signals to polling clients that the run is still in flight; the body still
        // returns the partial projection so the UI can render Pending/Running progress states.
        if (dto.Status is "Pending" or "GoldsetGenerating" or "Running")
        {
            return Results.Json(dto, statusCode: StatusCodes.Status423Locked);
        }

        return Results.Ok(dto);
    }

    private static async Task<IResult> ListEvaluations(
        Guid docId,
        IMediator mediator,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var dto = await mediator.Send(new ListEvaluationsQuery(docId, page, pageSize), ct).ConfigureAwait(false);
        return Results.Ok(dto);
    }
}
