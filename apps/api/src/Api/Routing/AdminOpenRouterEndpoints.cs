using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;

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

        // GET /api/v1/admin/openrouter/usage/timeline?period=
        // Issue #5078: Admin usage page — request timeline chart.
        group.MapGet("/usage/timeline", GetUsageTimeline)
            .WithName("GetUsageTimeline")
            .WithSummary("Get LLM request timeline grouped by source")
            .WithDescription("Returns hourly or daily request counts stacked by RequestSource. Period: 24h | 7d | 30d.");

        // GET /api/v1/admin/openrouter/usage/costs?period=
        // Issue #5080: Admin usage page — cost breakdown panel.
        group.MapGet("/usage/costs", GetUsageCosts)
            .WithName("GetUsageCosts")
            .WithSummary("Get LLM cost breakdown by model, source, and tier")
            .WithDescription("Returns aggregated cost data for the given period. Period: 1d | 7d | 30d.");

        // GET /api/v1/admin/openrouter/requests
        // Issue #5083: Admin usage page — recent requests table.
        group.MapGet("/requests", GetRecentRequests)
            .WithName("GetRecentLlmRequests")
            .WithSummary("Get paginated list of recent LLM request log entries")
            .WithDescription("Supports filtering by source, model, successOnly, and date range. Paginated.");
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

    private static async Task<IResult> GetUsageTimeline(
        [FromServices] IMediator mediator,
        [FromQuery] string period = "24h",
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetUsageTimelineQuery(period), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetUsageCosts(
        [FromServices] IMediator mediator,
        [FromQuery] string period = "7d",
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetUsageCostsQuery(period), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetRecentRequests(
        [FromServices] IMediator mediator,
        [FromQuery] string? source = null,
        [FromQuery] string? model = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] bool? successOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetRecentLlmRequestsQuery(source, model, from, to, successOnly, page, pageSize),
            ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
