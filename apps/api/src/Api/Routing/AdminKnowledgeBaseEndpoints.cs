using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.EstimateAgentCost;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.ExportDocumentChunks;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGamesWithoutKb;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbNavCounts;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchDocumentChunks;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for Knowledge Base management.
/// Issues #4654, #4655, #4785: KB endpoints with real data from pgvector, DB, and processing queue.
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

        // GET /api/v1/admin/kb/nav-counts — Issue #1655 F3-FU-6
        kbGroup.MapGet("/nav-counts", async (IMediator mediator, CancellationToken ct) =>
        {
            var counts = await mediator.Send(new GetKbNavCountsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(counts);
        })
        .WithName("GetKbNavCounts")
        .WithSummary("Counts for KbSubNav badges (active queue + feedback last 7d).")
        .Produces<KbNavCountsDto>(200);

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

        // GET /api/v1/admin/kb/docs/{docId}/ingestion-log — Issue #1650
        kbGroup.MapGet("/docs/{docId:guid}/ingestion-log", async (
            Guid docId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var query = new GetLatestIngestionLogByDocumentIdQuery(docId);
            var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetKbDocIngestionLog")
        .WithSummary("Get the latest ProcessingJob (with Steps and LogEntries) for a PdfDocumentId.");

        // GET /api/v1/admin/kb/docs/{docId}/chunks/export — Issue #1653 F3-FU-4
        kbGroup.MapGet("/docs/{docId:guid}/chunks/export", async (
            Guid docId,
            IMediator m,
            CancellationToken ct) =>
        {
            var chunks = await m.Send(new ExportDocumentChunksQuery(docId), ct).ConfigureAwait(false);
            return Results.Ok(chunks);
        })
        .WithName("ExportKbDocChunks")
        .WithSummary("Export all chunks (full content) for a document as JSON.");

        // POST /api/v1/admin/kb/docs/{docId}/chunks/search — Issue #1653 F3-FU-4 (scored similarity)
        kbGroup.MapPost("/docs/{docId:guid}/chunks/search", async (
            Guid docId,
            [FromBody] DocChunkSearchRequest req,
            IMediator m,
            CancellationToken ct) =>
        {
            var r = await m.Send(
                new SearchDocumentChunksByVectorQuery(docId, req.Query, req.TopK ?? 10, req.MinScore ?? 0.0),
                ct).ConfigureAwait(false);
            return Results.Ok(r);
        })
        .WithName("SearchKbDocChunks")
        .WithSummary("Per-document semantic chunk search (scored).");

        // GET /api/v1/admin/kb/docs/{docId}/agents — Issue #1651 F3-FU-2
        kbGroup.MapGet("/docs/{docId:guid}/agents", async (
            Guid docId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var query = new GetConsumingAgentsByDocumentIdQuery(docId);
            var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetKbDocConsumingAgents")
        .WithSummary("List agent definitions that consume a given PDF document (KbCardIds containment).");

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

        // GET /api/v1/admin/kb/games/without-kb - Admin RAG onboarding: games missing a KB
        kbGroup.MapGet("/games/without-kb", async (
            IMediator mediator,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            CancellationToken cancellationToken) =>
        {
            var query = new GetGamesWithoutKbQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                Search: search);

            var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetGamesWithoutKb")
        .WithSummary("List shared games with no active Knowledge Base (admin RAG onboarding)")
        .WithDescription("Returns SharedGames where HasKnowledgeBase = false. Supports pagination and search on Title.")
        .Produces<GamesWithoutKbPagedResponse>();

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

/// <summary>
/// Request model for per-document semantic chunk search.
/// Issue #1653: F3-FU-4 — per-document scored similarity-search.
/// </summary>
internal record DocChunkSearchRequest(
    string Query,
    int? TopK,
    double? MinScore
);
