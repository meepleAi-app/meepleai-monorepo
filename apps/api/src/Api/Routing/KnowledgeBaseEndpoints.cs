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

            var command = new CreateChatThreadCommand(
                UserId: userId,
                GameId: req.GameId,
                Title: req.Title,
                InitialMessage: req.InitialMessage
            );

            var result = await mediator.Send(command, ct);
            return Results.Created($"/api/v1/chat-threads/{result.Id}", result);
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

            if (gameId.HasValue)
            {
                if (!Guid.TryParse(session.User.Id, out var userId))
                {
                    return Results.BadRequest(new { error = "Invalid user ID" });
                }

                var query = new GetChatThreadsByGameQuery(gameId.Value, userId);
                var results = await mediator.Send(query, ct);

                return Results.Ok(new { threads = results, count = results.Count });
            }
            else
            {
                return Results.BadRequest(new { error = "gameId query parameter required" });
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

            // SEC: Authorize BEFORE executing command to prevent unauthorized mutations
            if (!Guid.TryParse(session.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "Invalid user ID" });
            }

            // Verify thread ownership before mutation
            var threadQuery = new GetChatThreadByIdQuery(threadId);
            var existingThread = await mediator.Send(threadQuery, ct);

            if (existingThread == null)
            {
                return Results.NotFound(new { error = "Thread not found" });
            }

            if (existingThread.UserId != userId &&
                !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("User {UserId} denied access to add message to thread {ThreadId} (owner: {OwnerId})",
                    userId, threadId, existingThread.UserId);
                return Results.Forbid();
            }

            var command = new AddMessageCommand(
                ThreadId: threadId,
                Content: req.Content,
                Role: req.Role
            );

            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
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

            // SEC: Authorize BEFORE executing command
            if (!Guid.TryParse(session.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "Invalid user ID" });
            }

            // Verify thread ownership before mutation
            var threadQuery = new GetChatThreadByIdQuery(threadId);
            var existingThread = await mediator.Send(threadQuery, ct);

            if (existingThread == null)
            {
                return Results.NotFound(new { error = "Thread not found" });
            }

            if (existingThread.UserId != userId &&
                !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("User {UserId} denied access to close thread {ThreadId} (owner: {OwnerId})",
                    userId, threadId, existingThread.UserId);
                return Results.Forbid();
            }

            var command = new CloseThreadCommand(threadId);
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
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

            // SEC: Authorize BEFORE executing command
            if (!Guid.TryParse(session.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "Invalid user ID" });
            }

            // Verify thread ownership before mutation
            var threadQuery = new GetChatThreadByIdQuery(threadId);
            var existingThread = await mediator.Send(threadQuery, ct);

            if (existingThread == null)
            {
                return Results.NotFound(new { error = "Thread not found" });
            }

            if (existingThread.UserId != userId &&
                !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("User {UserId} denied access to reopen thread {ThreadId} (owner: {OwnerId})",
                    userId, threadId, existingThread.UserId);
                return Results.Forbid();
            }

            var command = new ReopenThreadCommand(threadId);
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        })
        .WithName("ReopenThread")
        .WithTags("ChatThreads");

        // ISSUE-860: Export chat thread (DDD implementation)
        group.MapGet("/chat-threads/{threadId:guid}/export", async (
            Guid threadId,
            [Microsoft.AspNetCore.Mvc.FromQuery] string? format,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // SEC: Authorize BEFORE executing command
            if (!Guid.TryParse(session.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "Invalid user ID" });
            }

            // Verify thread ownership before export
            var threadQuery = new GetChatThreadByIdQuery(threadId);
            var existingThread = await mediator.Send(threadQuery, ct);

            if (existingThread == null)
            {
                return Results.NotFound(new { error = "Thread not found" });
            }

            if (existingThread.UserId != userId &&
                !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("User {UserId} denied access to export thread {ThreadId} (owner: {OwnerId})",
                    userId, threadId, existingThread.UserId);
                return Results.Forbid();
            }

            // Default to JSON if no format specified
            var exportFormat = format?.ToLowerInvariant() ?? "json";

            var command = new ExportChatCommand(threadId, exportFormat);
            var result = await mediator.Send(command, ct);

            // Generate filename
            var threadTitle = existingThread.Title?.Replace(" ", "_") ?? "chat";
            var sanitizedTitle = new string(threadTitle.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            var filename = $"{sanitizedTitle}_{threadId.ToString()[..8]}.{result.FileExtension}";

            logger.LogInformation("User {UserId} exported thread {ThreadId} in {Format} format",
                userId, threadId, result.Format);

            // Return file with appropriate content type
            return Results.Content(
                result.Content,
                result.ContentType,
                null,
                statusCode: 200);
        })
        .WithName("ExportChatThread")
        .WithTags("ChatThreads")
        .Produces<string>(200, "application/json", "text/markdown")
        .ProducesProblem(400)
        .ProducesProblem(404)
        .ProducesProblem(403)
        .ProducesProblem(500);

        // ISSUE-1184: Update message in thread
        group.MapPut("/chat-threads/{threadId:guid}/messages/{messageId:guid}", async (
            Guid threadId,
            Guid messageId,
            UpdateMessageRequest req,
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

            if (string.IsNullOrWhiteSpace(req.Content))
            {
                return Results.BadRequest(new { error = "Content is required" });
            }

            var command = new UpdateMessageCommand(threadId, messageId, req.Content, userId);
            var result = await mediator.Send(command, ct);

            logger.LogInformation("User {UserId} updated message {MessageId} in thread {ThreadId}",
                userId, messageId, threadId);

            return Results.Ok(result);
        })
        .WithName("UpdateChatMessage")
        .WithTags("ChatThreads");

        // ISSUE-1184: Delete message from thread
        group.MapDelete("/chat-threads/{threadId:guid}/messages/{messageId:guid}", async (
            Guid threadId,
            Guid messageId,
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

            var isAdmin = string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

            var command = new DeleteMessageCommand(threadId, messageId, userId, isAdmin);
            var result = await mediator.Send(command, ct);

            logger.LogInformation("User {UserId} deleted message {MessageId} from thread {ThreadId} (admin: {IsAdmin})",
                userId, messageId, threadId, isAdmin);

            return Results.Ok(result);
        })
        .WithName("DeleteChatMessage")
        .WithTags("ChatThreads");

        // ISSUE-1184: Delete chat thread
        group.MapDelete("/chat-threads/{threadId:guid}", async (
            Guid threadId,
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

            var command = new DeleteChatThreadCommand(threadId, userId);
            await mediator.Send(command, ct);

            logger.LogInformation("User {UserId} deleted thread {ThreadId}", userId, threadId);

            return Results.NoContent();
        })
        .WithName("DeleteChatThread")
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
