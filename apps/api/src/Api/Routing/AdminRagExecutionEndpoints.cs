using Api.BoundedContexts.KnowledgeBase.Application.Queries.RagExecution;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for RAG execution history browsing and stats.
/// Issue #4458: RAG Execution History
/// </summary>
internal static class AdminRagExecutionEndpoints
{
    public static void MapAdminRagExecutionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/rag-executions")
            .WithTags("Admin - RAG Executions")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapGet("/", async (
            [FromQuery] int skip,
            [FromQuery] int take,
            [FromQuery] string? strategy,
            [FromQuery] string? status,
            [FromQuery] int? minLatencyMs,
            [FromQuery] int? maxLatencyMs,
            [FromQuery] double? minConfidence,
            [FromQuery] DateTime? dateFrom,
            [FromQuery] DateTime? dateTo,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetRagExecutionsQuery(
                Skip: Math.Max(0, skip),
                Take: Math.Clamp(take, 1, 100),
                Strategy: strategy,
                Status: status,
                MinLatencyMs: minLatencyMs,
                MaxLatencyMs: maxLatencyMs,
                MinConfidence: minConfidence,
                From: dateFrom,
                To: dateTo);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AdminGetRagExecutions")
        .WithSummary("List RAG executions with filters (Admin)")
        .Produces<GetRagExecutionsResult>();

        group.MapGet("/{id:guid}", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetRagExecutionByIdQuery(id), ct).ConfigureAwait(false);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("AdminGetRagExecutionById")
        .WithSummary("Get RAG execution detail with trace (Admin)")
        .Produces<RagExecutionDetailDto>()
        .ProducesProblem(404);

        group.MapGet("/stats", async (
            [FromQuery] DateTime? dateFrom,
            [FromQuery] DateTime? dateTo,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetRagExecutionStatsQuery(dateFrom, dateTo), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AdminGetRagExecutionStats")
        .WithSummary("Aggregated RAG execution stats (Admin)")
        .Produces<RagExecutionStatsDto>();
    }
}
