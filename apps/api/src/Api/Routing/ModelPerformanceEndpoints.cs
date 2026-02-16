using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Model Performance endpoints for admin dashboard.
/// Issue #3716: Model comparison dashboard - latency, cost, quality metrics,
/// usage breakdown per model.
/// </summary>
internal static class ModelPerformanceEndpoints
{
    internal static void MapModelPerformanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/model-performance")
            .WithTags("Admin - Model Performance")
            .AddEndpointFilter<Filters.RequireAdminSessionFilter>();

        group.MapGet("/", HandleGetModelPerformance)
            .WithName("GetModelPerformance")
            .WithSummary("Get model performance analytics (Admin only)")
            .WithDescription(
                "Returns aggregated model performance metrics including per-model " +
                "latency, cost, token usage, success rates, usage percentages, " +
                "and daily trends. Supports time range filtering. (Issue #3716)")
            .Produces<ModelPerformanceDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> HandleGetModelPerformance(
        HttpContext context,
        IMediator mediator,
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetModelPerformanceQuery(
            TimeRangeDays: Math.Clamp(days, 1, 365));

        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
