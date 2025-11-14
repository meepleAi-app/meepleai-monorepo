using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using Api.Infrastructure.Entities;
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

        // CHAT-THREAD-01: Create chat thread
        group.MapPost("/chat-threads", async (
            CreateChatThreadRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (!Guid.TryParse(session.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "Invalid user ID" });
            }

            logger.LogInformation("Creating chat thread for user {UserId}, game {GameId}", userId, req.GameId);

            try
            {
                var command = new CreateChatThreadCommand(
                    UserId: userId,
                    GameId: req.GameId,
                    Title: req.Title,
                    InitialMessage: req.InitialMessage
                );

                var result = await mediator.Send(command, ct);
                return Results.Created($"/api/v1/chat-threads/{result.Id}", result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create chat thread");
                return Results.Problem(ex.Message, statusCode: 500);
            }
        })
        .WithName("CreateChatThread")
        .WithTags("ChatThreads");

        // CHAT-THREAD-02: Get chat thread by ID
        group.MapGet("/chat-threads/{threadId:guid}", async (
            Guid threadId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            try
            {
                var query = new GetChatThreadByIdQuery(threadId);
                var result = await mediator.Send(query, ct);

                if (result == null)
                {
                    return Results.NotFound(new { error = "Thread not found" });
                }

                // Authorization: User can only access their own threads unless admin
                if (!Guid.TryParse(session.User.Id, out var userId))
                {
                    return Results.BadRequest(new { error = "Invalid user ID" });
                }

                if (result.UserId != userId &&
                    !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Forbid();
                }

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get chat thread {ThreadId}", threadId);
                return Results.Problem(ex.Message, statusCode: 500);
            }
        })
        .WithName("GetChatThreadById")
        .WithTags("ChatThreads");

        // CHAT-THREAD-03: Get threads by game
        group.MapGet("/chat-threads", async (
            [Microsoft.AspNetCore.Mvc.FromQuery] Guid? gameId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            try
            {
                if (gameId.HasValue)
                {
                    var query = new GetChatThreadsByGameQuery(gameId.Value);
                    var results = await mediator.Send(query, ct);

                    // Filter by user (non-admin can only see own threads)
                    if (!Guid.TryParse(session.User.Id, out var userId))
                    {
                        return Results.BadRequest(new { error = "Invalid user ID" });
                    }

                    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        results = results.Where(t => t.UserId == userId).ToList();
                    }

                    return Results.Ok(new { threads = results, count = results.Count });
                }
                else
                {
                    return Results.BadRequest(new { error = "gameId query parameter required" });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get chat threads for game {GameId}", gameId);
                return Results.Problem(ex.Message, statusCode: 500);
            }
        })
        .WithName("GetChatThreadsByGame")
        .WithTags("ChatThreads");

        // CHAT-THREAD-04: Add message to thread
        group.MapPost("/chat-threads/{threadId:guid}/messages", async (
            Guid threadId,
            AddMessageRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (string.IsNullOrWhiteSpace(req.Content))
            {
                return Results.BadRequest(new { error = "Content is required" });
            }

            try
            {
                var command = new AddMessageCommand(
                    ThreadId: threadId,
                    Content: req.Content,
                    Role: req.Role
                );

                var result = await mediator.Send(command, ct);

                // Authorization: User can only add to their own threads
                if (!Guid.TryParse(session.User.Id, out var userId))
                {
                    return Results.BadRequest(new { error = "Invalid user ID" });
                }

                if (result.UserId != userId &&
                    !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Forbid();
                }

                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Failed to add message to thread {ThreadId}", threadId);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error adding message to thread {ThreadId}", threadId);
                return Results.Problem(ex.Message, statusCode: 500);
            }
        })
        .WithName("AddMessageToThread")
        .WithTags("ChatThreads");

        // CHAT-THREAD-05: Close chat thread
        group.MapPost("/chat-threads/{threadId:guid}/close", async (
            Guid threadId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            try
            {
                var command = new CloseThreadCommand(threadId);
                var result = await mediator.Send(command, ct);

                // Authorization: User can only close their own threads
                if (!Guid.TryParse(session.User.Id, out var userId))
                {
                    return Results.BadRequest(new { error = "Invalid user ID" });
                }

                if (result.UserId != userId &&
                    !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Forbid();
                }

                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Failed to close thread {ThreadId}", threadId);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error closing thread {ThreadId}", threadId);
                return Results.Problem(ex.Message, statusCode: 500);
            }
        })
        .WithName("CloseThread")
        .WithTags("ChatThreads");

        // CHAT-THREAD-06: Reopen chat thread
        group.MapPost("/chat-threads/{threadId:guid}/reopen", async (
            Guid threadId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            try
            {
                var command = new ReopenThreadCommand(threadId);
                var result = await mediator.Send(command, ct);

                // Authorization: User can only reopen their own threads
                if (!Guid.TryParse(session.User.Id, out var userId))
                {
                    return Results.BadRequest(new { error = "Invalid user ID" });
                }

                if (result.UserId != userId &&
                    !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Forbid();
                }

                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Failed to reopen thread {ThreadId}", threadId);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error reopening thread {ThreadId}", threadId);
                return Results.Problem(ex.Message, statusCode: 500);
            }
        })
        .WithName("ReopenThread")
        .WithTags("ChatThreads");

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
