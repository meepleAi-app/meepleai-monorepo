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
    }

    private static async Task<IResult> GetStatus(
        [FromServices] IMediator mediator,
        CancellationToken ct)
    {
        var status = await mediator.Send(new GetOpenRouterStatusQuery(), ct).ConfigureAwait(false);
        return Results.Ok(status);
    }
}
