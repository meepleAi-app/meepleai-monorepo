using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using Api.Middleware;
using Api.Models;
using MediatR;

namespace Api.Routing;

/// <summary>
/// DDD-PHASE3: KnowledgeBase bounded context endpoints.
/// Provides vector search and RAG Q&A via CQRS handlers.
/// </summary>
public static class KnowledgeBaseEndpoints
{
    public static RouteGroupBuilder MapKnowledgeBaseEndpoints(this RouteGroupBuilder group)
    {
        // DDD-PHASE3: Vector/hybrid search endpoint using MediatR
        group.MapPost("/knowledge-base/search", async (
            KnowledgeBaseSearchRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Validate request
            if (!Guid.TryParse(req.gameId, out var gameId))
            {
                return Results.BadRequest(new { error = "Invalid gameId format" });
            }

            if (string.IsNullOrWhiteSpace(req.query))
            {
                return Results.BadRequest(new { error = "Query is required" });
            }

            logger.LogInformation(
                "KnowledgeBase search request from user {UserId} for game {GameId}: {Query}",
                session.User.Id, gameId, req.query);

            try
            {
                // Create query
                var query = new SearchQuery(
                    GameId: gameId,
                    Query: req.query,
                    TopK: req.topK ?? 5,
                    MinScore: req.minScore ?? 0.7,
                    SearchMode: req.searchMode ?? "hybrid",
                    Language: req.language ?? "en"
                );

                // Execute via MediatR
                var results = await mediator.Send(query, ct);

                logger.LogInformation(
                    "KnowledgeBase search completed: {ResultCount} results found",
                    results.Count);

                return Results.Ok(new
                {
                    success = true,
                    results = results,
                    count = results.Count,
                    searchMode = query.SearchMode
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "KnowledgeBase search failed for game {GameId}", gameId);
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Search failed");
            }
        })
        .WithName("KnowledgeBaseSearch")
        .WithTags("KnowledgeBase");

        // DDD-PHASE3: RAG Q&A endpoint using MediatR
        group.MapPost("/knowledge-base/ask", async (
            KnowledgeBaseAskRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Validate request
            if (!Guid.TryParse(req.gameId, out var gameId))
            {
                return Results.BadRequest(new { error = "Invalid gameId format" });
            }

            if (string.IsNullOrWhiteSpace(req.query))
            {
                return Results.BadRequest(new { error = "Query is required" });
            }

            logger.LogInformation(
                "KnowledgeBase Q&A request from user {UserId} for game {GameId}: {Query}",
                session.User.Id, gameId, req.query);

            try
            {
                // Create query
                var query = new AskQuestionQuery(
                    GameId: gameId,
                    Question: req.query,
                    Language: req.language ?? "en",
                    BypassCache: req.bypassCache ?? false
                );

                // Execute via MediatR
                var response = await mediator.Send(query, ct);

                logger.LogInformation(
                    "KnowledgeBase Q&A completed: Confidence={Confidence}, IsLowQuality={IsLowQuality}",
                    response.OverallConfidence, response.IsLowQuality);

                return Results.Ok(new
                {
                    success = true,
                    answer = response.Answer,
                    sources = response.Sources,
                    searchConfidence = response.SearchConfidence,
                    llmConfidence = response.LlmConfidence,
                    overallConfidence = response.OverallConfidence,
                    isLowQuality = response.IsLowQuality,
                    citations = response.Citations
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "KnowledgeBase Q&A failed for game {GameId}", gameId);
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Q&A failed");
            }
        })
        .WithName("KnowledgeBaseAsk")
        .WithTags("KnowledgeBase");

        return group;
    }
}

/// <summary>
/// Request model for knowledge base search.
/// </summary>
public record KnowledgeBaseSearchRequest(
    string gameId,
    string query,
    int? topK = null,
    double? minScore = null,
    string? searchMode = null,
    string? language = null
);
