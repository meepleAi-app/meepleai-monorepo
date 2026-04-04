using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.EstimateAgentCost;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for Knowledge Base management.
/// Issues #4654, #4655, #4785: KB endpoints with real data from Qdrant, DB, and processing queue.
/// </summary>
internal static class AdminKnowledgeBaseEndpoints
{
    public static RouteGroupBuilder MapAdminKnowledgeBaseEndpoints(this RouteGroupBuilder group)
    {
        var kbGroup = group.MapGroup("/admin/kb")
            .WithTags("Admin", "KnowledgeBase")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/kb/vector-stats — pgvector statistics grouped by game
        kbGroup.MapGet("/vector-stats", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetVectorStatsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetVectorStats")
        .WithSummary("Get pgvector statistics grouped by game");

        // POST /api/v1/admin/kb/vector-search — semantic search over pgvector embeddings
        kbGroup.MapPost("/vector-search", async (
            [FromBody] VectorSearchRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new VectorSemanticSearchQuery(
                request.Query,
                Math.Clamp(request.Limit ?? 10, 1, 100),
                request.GameId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("VectorSearch")
        .WithSummary("Semantic search over pgvector embeddings");

        // GET /api/v1/admin/kb/processing-queue (#4655, #4785)
        kbGroup.MapGet("/processing-queue", async (
            IMediator mediator,
            string? statusFilter,
            string? searchText,
            DateTimeOffset? fromDate,
            DateTimeOffset? toDate,
            int? page,
            int? pageSize,
            CancellationToken cancellationToken) =>
        {
            var query = new GetProcessingQueueQuery(
                StatusFilter: statusFilter,
                SearchText: searchText,
                FromDate: fromDate,
                ToDate: toDate,
                Page: page ?? 1,
                PageSize: pageSize ?? 20
            );

            var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        });

        // POST /api/v1/admin/kb/agents/estimate-cost - Pre-chat cost estimation
        kbGroup.MapPost("/agents/estimate-cost", async (
            [FromBody] EstimateAgentCostByDocumentsRequest request,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var query = new EstimateAgentCostQuery(
                request.GameId,
                request.DocumentIds,
                request.StrategyName ?? "HybridSearch");

            var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("EstimateAgentCostByDocuments")
        .WithSummary("Estimate token cost before starting a RAG chat session")
        .WithDescription("Calculates estimated cost per query based on document chunks, model pricing, and retrieval strategy.")
        .Produces<AgentCostEstimateDto>();

        // GET /api/v1/admin/shared-games (extended for admin - #4654, #4785)
        var gamesGroup = group.MapGroup("/admin/shared-games")
            .WithTags("Admin", "SharedGames")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        gamesGroup.MapGet("/categories", async (
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var categories = await mediator.Send(new GetGameCategoriesQuery(), cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new { categories });
        });

        return group;
    }

}

/// <summary>
/// Request model for semantic vector search.
/// </summary>
internal record VectorSearchRequest(
    string Query,
    int? Limit,
    Guid? GameId
);

/// <summary>
/// Request model for pre-chat agent cost estimation by document selection.
/// </summary>
internal record EstimateAgentCostByDocumentsRequest(
    Guid GameId,
    List<Guid> DocumentIds,
    string? StrategyName
);
