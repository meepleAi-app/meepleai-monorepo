using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for OpenRouter usage monitoring dashboard.
/// Issue #5077: Admin usage page — layout and navigation.
/// </summary>
internal static class AdminOpenRouterEndpoints
{
    public static void MapAdminOpenRouterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/openrouter")
            .WithTags("Admin - OpenRouter")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/openrouter/status
        group.MapGet("/status", GetStatus)
            .WithName("GetOpenRouterStatus")
            .WithSummary("Get current OpenRouter account status and rate-limit utilization")
            .WithDescription(@"
Returns a real-time snapshot aggregated from three sources:
- Account balance and rate-limit ceiling (OpenRouter /auth/key, cached 90s in Redis)
- RPM/TPM utilization (Redis sliding window)
- Today's request count (PostgreSQL LlmRequestLog)

Suitable for admin usage dashboard KPI cards.
");

        // GET /api/v1/admin/openrouter/free-quota
        // Issue #5087: Free model quota tracking — RPM vs RPD distinction.
        group.MapGet("/free-quota", GetFreeQuota)
            .WithName("GetOpenRouterFreeQuota")
            .WithSummary("Get today's free-tier model usage vs daily limits (RPD tracking)")
            .WithDescription(@"
Returns per-model RPD (requests-per-day) usage for free OpenRouter models.
Sources:
- Request counts from PostgreSQL LlmRequestLog (IsFreeModel = true)
- Last rate-limit error type from Redis (RPM or RPD)
- RPD reset timestamp from Redis (set when daily quota is exhausted)

Daily limit is 1000 RPD for accounts with $10+ credits.
");
    }

    private static async Task<IResult> GetStatus(
        [FromServices] IMediator mediator,
        CancellationToken ct)
    {
        var status = await mediator.Send(new GetOpenRouterStatusQuery(), ct).ConfigureAwait(false);
        return Results.Ok(status);
    }

    private static async Task<IResult> GetFreeQuota(
        [FromServices] IMediator mediator,
        CancellationToken ct)
    {
        var quota = await mediator.Send(new GetUsageFreeQuotaQuery(), ct).ConfigureAwait(false);
        return Results.Ok(quota);
    }
}
