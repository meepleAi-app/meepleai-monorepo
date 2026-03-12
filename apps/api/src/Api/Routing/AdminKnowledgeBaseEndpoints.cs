using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.EstimateAgentCost;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Filters;
using Api.Services;
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

        // GET /api/v1/admin/kb/vector-collections (#4655, #4785)
        kbGroup.MapGet("/vector-collections", async (
            IQdrantClientAdapter qdrantClient,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var collectionNames = await qdrantClient.ListCollectionsAsync(cancellationToken)
                    .ConfigureAwait(false);

                var collections = new List<object>();
                foreach (var name in collectionNames)
                {
                    try
                    {
                        var info = await qdrantClient.GetCollectionInfoAsync(name, cancellationToken)
                            .ConfigureAwait(false);

                        var vectorCount = (long)info.PointsCount;
                        var dimensions = 384; // Default for sentence-transformers
                        var memoryBytes = vectorCount * dimensions * 4L;
                        var indexedCount = (long)info.IndexedVectorsCount;
                        var health = vectorCount > 0 && indexedCount > 0
                            ? (int)Math.Min(100, (indexedCount * 100) / Math.Max(1, vectorCount))
                            : 0;

                        collections.Add(new
                        {
                            name,
                            vectorCount,
                            dimensions,
                            storage = FormatBytes(memoryBytes),
                            health,
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to get info for collection {CollectionName}", name);
                        collections.Add(new
                        {
                            name,
                            vectorCount = 0L,
                            dimensions = 0,
                            storage = "N/A",
                            health = 0,
                        });
                    }
                }

                return Results.Ok(new { collections });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to list Qdrant collections");
                return Results.Ok(new { collections = Array.Empty<object>() });
            }
        });

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
        .WithName("EstimateAgentCost")
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

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Request model for pre-chat agent cost estimation by document selection.
/// </summary>
internal record EstimateAgentCostByDocumentsRequest(
    Guid GameId,
    List<Guid> DocumentIds,
    string? StrategyName
);
