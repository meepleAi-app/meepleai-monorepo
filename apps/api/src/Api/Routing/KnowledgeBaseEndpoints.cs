using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.ContextEngineering.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.ContextEngineering.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;

using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGameDocuments;
using Api.Extensions;
using Api.Helpers;
using Api.Infrastructure.Entities;
using Api.Middleware;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Routing;

/// <summary>
/// DDD-PHASE3: KnowledgeBase bounded context endpoints.
/// Provides vector search and RAG Q&amp;A via CQRS handlers.
/// </summary>
internal static class KnowledgeBaseEndpoints
{
    public static RouteGroupBuilder MapKnowledgeBaseEndpoints(this RouteGroupBuilder group)
    {
        MapStatusEndpoint(group);
        MapSearchEndpoint(group);
        MapAskEndpoint(group);
        MapChatLookupEndpoints(group);
        MapChatHistoryEndpoints(group);
        MapChatLifecycleEndpoints(group);
        MapChatStateEndpoints(group);
        MapChatUpdateEndpoints(group);
        MapChatMessageEndpoints(group);
        MapChatExportEndpoints(group);
        MapContextEngineeringEndpoints(group);
        MapGameDocumentsEndpoint(group);
        MapLinkKbEndpoint(group);

        return group;
    }

    private static void MapStatusEndpoint(RouteGroupBuilder group)
    {
        // Issue #4065: RAG readiness polling endpoint
        group.MapGet("/knowledge-base/{gameId:guid}/status", HandleGetKnowledgeBaseStatus)
            .WithName("GetKnowledgeBaseStatus")
            .RequireSession()
            .WithTags("KnowledgeBase")
            .WithSummary("Get embedding status for a game")
            .WithDescription("Returns the embedding pipeline status (Pending, Extracting, Chunking, Embedding, Completed, Failed) for the most recent PDF of a game.")
            .Produces<KnowledgeBaseStatusDto>()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static void MapSearchEndpoint(RouteGroupBuilder group)
    {
        // DDD-PHASE3: Vector/hybrid search endpoint using MediatR
        group.MapPost("/knowledge-base/search", HandleSearch)
        .WithName("KnowledgeBaseSearch")
        .RequireSession()
        .WithTags("KnowledgeBase");
    }

    private static void MapAskEndpoint(RouteGroupBuilder group)
    {
        // DDD-PHASE3: RAG Q&amp;A endpoint using MediatR
        group.MapPost("/knowledge-base/ask", HandleAsk)
        .WithName("KnowledgeBaseAsk")
        .RequireSession()
        .WithTags("KnowledgeBase");
    }

    private static void MapChatLookupEndpoints(RouteGroupBuilder group)
    {
        // CHAT-THREAD-02: Get chat thread by ID
        group.MapGet("/chat-threads/{threadId:guid}", HandleGetChatThreadById)
        .WithName("GetChatThreadById")
        .RequireSession()
        .WithTags("ChatThreads");

        // CHAT-THREAD-03: Get threads by game
        group.MapGet("/chat-threads", HandleGetChatThreadsByGame)
        .WithName("GetChatThreadsByGame")
        .RequireSession()
        .WithTags("ChatThreads");
    }

    private static void MapChatHistoryEndpoints(RouteGroupBuilder group)
    {
        // CHAT-HISTORY-01: Get user's chat history for dashboard (Issue #2026)
        group.MapGet("/knowledge-base/my-chats", HandleGetMyChatHistory)
        .WithName("GetMyChatHistory")
        .RequireSession()
        .WithTags("ChatThreads")
        .WithDescription("Retrieve user's chat history for dashboard display (optimized, lightweight)");

        // Issue #4362: Get user's chat threads with filtering and pagination
        group.MapGet("/chat-threads/my", HandleGetMyFilteredThreads)
        .WithName("GetMyFilteredThreads")
        .RequireSession()
        .WithTags("ChatThreads")
        .WithDescription("Retrieve user's chat threads with filtering by agentType, gameId, status, and search");
    }

    private static void MapChatLifecycleEndpoints(RouteGroupBuilder group)
    {
        // CHAT-THREAD-01: Create chat thread
        group.MapPost("/chat-threads", HandleCreateChatThread)
        .WithName("CreateChatThread")
        .RequireSession()
        .WithTags("ChatThreads");

        // ISSUE-1184: Delete chat thread
        group.MapDelete("/chat-threads/{threadId:guid}", HandleDeleteChatThread)
        .WithName("DeleteChatThread")
        .RequireSession()
        .WithTags("ChatThreads");
    }

    private static void MapChatStateEndpoints(RouteGroupBuilder group)
    {
        // CHAT-THREAD-05: Close chat thread
        group.MapPost("/chat-threads/{threadId:guid}/close", HandleCloseThread)
        .WithName("CloseThread")
        .RequireSession()
        .WithTags("ChatThreads");

        // CHAT-THREAD-06: Reopen chat thread
        group.MapPost("/chat-threads/{threadId:guid}/reopen", HandleReopenThread)
        .WithName("ReopenThread")
        .RequireSession()
        .WithTags("ChatThreads");
    }

    private static void MapChatUpdateEndpoints(RouteGroupBuilder group)
    {
        // ISSUE-2257: Update chat thread title
        group.MapPatch("/chat-threads/{threadId:guid}", HandleUpdateChatThreadTitle)
        .WithName("UpdateChatThreadTitle")
        .RequireSession()
        .WithTags("ChatThreads");

        // ISSUE-4465: Switch agent type mid-conversation
        group.MapPatch("/chat-threads/{threadId:guid}/agent", HandleSwitchThreadAgent)
        .WithName("SwitchThreadAgent")
        .RequireSession()
        .WithTags("ChatThreads");
    }

    private static void MapChatMessageEndpoints(RouteGroupBuilder group)
    {
        // CHAT-THREAD-04: Add message to thread
        group.MapPost("/chat-threads/{threadId:guid}/messages", HandleAddMessageToThread)
        .WithName("AddMessageToThread")
        .RequireSession()
        .WithTags("ChatThreads");

        // ISSUE-1184: Update message in thread
        group.MapPut("/chat-threads/{threadId:guid}/messages/{messageId:guid}", HandleUpdateChatMessage)
        .WithName("UpdateChatMessage")
        .RequireSession()
        .WithTags("ChatThreads");

        // ISSUE-1184: Delete message from thread
        group.MapDelete("/chat-threads/{threadId:guid}/messages/{messageId:guid}", HandleDeleteChatMessage)
        .WithName("DeleteChatMessage")
        .RequireSession()
        .WithTags("ChatThreads");
    }

    private static void MapChatExportEndpoints(RouteGroupBuilder group)
    {
        // ISSUE-860: Export chat thread (DDD implementation)
        group.MapGet("/chat-threads/{threadId:guid}/export", HandleExportChatThread)
        .WithName("ExportChatThread")
        .RequireSession()
        .WithTags("ChatThreads")
        .Produces<string>(200, "application/json", "text/markdown")
        .ProducesProblem(400)
        .ProducesProblem(404)
        .ProducesProblem(403)
        .ProducesProblem(500);
    }

    private static async Task<IResult> HandleGetKnowledgeBaseStatus(
        Guid gameId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        logger.LogDebug("GetKnowledgeBaseStatus for game {GameId} by user {UserId}",
            gameId, session!.User!.Id);

        var query = new GetKnowledgeBaseStatusQuery(gameId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        if (result is null)
        {
            return Results.NotFound(new { error = "Knowledge base status not found" });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleSearch(
        KnowledgeBaseSearchRequest req,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        if (!Guid.TryParse(req.gameId, out var gameId))
        {
            return Results.BadRequest(new { error = "Invalid gameId format" });
        }

        var queryError = QueryValidator.ValidateQuery(req.query);
        if (queryError != null)
        {
            return Results.BadRequest(new { error = queryError });
        }

        logger.LogInformation(
            "KnowledgeBase search request from user {UserId} for game {GameId}: {Query}",
            session!.User!.Id, gameId, req.query);

        var query = new SearchQuery(
            GameId: gameId,
            Query: req.query,
            TopK: req.topK ?? 5,
            MinScore: req.minScore ?? 0.55,
            SearchMode: req.searchMode ?? "hybrid",
            Language: req.language ?? "en",
            UserId: session!.User!.Id,
            UserRole: session.User!.Role
        );

        var results = await mediator.Send(query, ct).ConfigureAwait(false);

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

    private static async Task<IResult> HandleAsk(
        KnowledgeBaseAskRequest req,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        logger.LogDebug("[KnowledgeBase.Ask] HandleAsk ENTRY - gameId: {GameId}, query: {Query}",
            req.gameId, req.query?.Substring(0, Math.Min(50, req.query?.Length ?? 0)));

        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        logger.LogDebug("[KnowledgeBase.Ask] Session retrieved - UserId: {UserId}", session?.User?.Id);

        if (!Guid.TryParse(req.gameId, out var gameId))
        {
            logger.LogWarning("[KnowledgeBase.Ask] Invalid gameId format: {GameId}", req.gameId);
            return Results.BadRequest(new { error = "Invalid gameId format" });
        }

        var queryError = QueryValidator.ValidateQuery(req.query);
        if (queryError != null)
        {
            logger.LogWarning("[KnowledgeBase.Ask] Query validation failed: {Error}", queryError);
            return Results.BadRequest(new { error = queryError });
        }

        logger.LogInformation(
            "[KnowledgeBase.Ask] Q&A request from user {UserId} for game {GameId}: {Query}",
            session!.User!.Id, gameId, req.query);

        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: req.query!,  // Already validated by QueryValidator above
            Language: req.language ?? "en",
            BypassCache: req.bypassCache ?? false,
            UserId: session!.User!.Id,
            UserRole: session.User!.Role
        );

        logger.LogDebug("[KnowledgeBase.Ask] Sending AskQuestionQuery to mediator...");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await mediator.Send(query, ct).ConfigureAwait(false);
        stopwatch.Stop();

        logger.LogInformation(
            "[KnowledgeBase.Ask] Q&A completed in {ElapsedMs}ms: Confidence={Confidence}, IsLowQuality={IsLowQuality}",
            stopwatch.ElapsedMilliseconds, response.OverallConfidence, response.IsLowQuality);

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

    private static async Task<IResult> HandleGetMyChatHistory(
        [FromQuery] int skip,
        [FromQuery] int take,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        const int DEFAULT_PAGE_SIZE = 50;
        const int MAX_PAGE_SIZE = 100;
        const int MAX_SKIP = 10000;

        var session = context.Items[nameof(SessionStatusDto)] as SessionStatusDto;
        if (session?.User?.Id is not Guid userId)
        {
            logger.LogWarning("GetMyChatHistory called without valid session");
            return Results.Unauthorized();
        }

        var safeSkip = skip < 0 ? 0 : Math.Min(skip, MAX_SKIP);
        var safeTake = take <= 0 ? DEFAULT_PAGE_SIZE : Math.Min(take, MAX_PAGE_SIZE);

        if (skip > MAX_SKIP)
        {
            logger.LogWarning("User {UserId} attempted excessive skip ({Skip}), capped at {Max}", userId, skip, MAX_SKIP);
        }

        logger.LogInformation("Fetching chat history for user {UserId} (skip: {Skip}, take: {Take})", userId, safeSkip, safeTake);

        try
        {
            var query = new GetMyChatHistoryQuery(userId, safeSkip, safeTake);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                threads = result.Chats,
                count = result.TotalCount,
                page = new
                {
                    skip = safeSkip,
                    take = safeTake,
                    hasMore = safeSkip + result.Chats.Count < result.TotalCount
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch chat history for user {UserId}", userId);
            return Results.Problem("Failed to load chat history");
        }
    }

    /// <summary>
    /// Issue #4362: Get user's chat threads with filtering and pagination.
    /// Supports agentType, gameId, status, and search filters.
    /// </summary>
    private static async Task<IResult> HandleGetMyFilteredThreads(
        [FromQuery] Guid? gameId,
        [FromQuery] string? agentType,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        HttpContext context = null!,
        IMediator mediator = null!,
        CancellationToken ct = default)
    {
        var session = context.Items[nameof(SessionStatusDto)] as SessionStatusDto;
        if (session?.User?.Id is not Guid userId)
        {
            return Results.Unauthorized();
        }

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = new GetUserChatThreadsQuery(
            UserId: userId,
            GameId: gameId,
            AgentType: agentType,
            Status: status,
            Search: search,
            Page: safePage,
            PageSize: safePageSize
        );

        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetChatThreadById(
        Guid threadId,
        HttpContext context,
        IMediator mediator,
                CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        var query = new GetChatThreadByIdQuery(threadId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        if (result == null)
        {
            return Results.NotFound(new { error = "Thread not found" });
        }

        var userId = session!.User!.Id;

        if (result.UserId != userId &&
            !string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Results.Forbid();
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetChatThreadsByGame(
        [FromQuery] Guid? gameId,
        HttpContext context,
        IMediator mediator,
                CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        if (gameId.HasValue)
        {
            var userId = session!.User!.Id;
            var query = new GetChatThreadsByGameQuery(gameId.Value, userId);
            var results = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(new { threads = results, count = results.Count });
        }
        else
        {
            return Results.BadRequest(new { error = "gameId query parameter required" });
        }
    }

    private static async Task<IResult> HandleCreateChatThread(
        CreateChatThreadRequest req,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        logger.LogInformation("Creating chat thread for user {UserId}, game {GameId}", userId, req.GameId);

        var command = new CreateChatThreadCommand(
            UserId: userId,
            GameId: req.GameId,
            Title: req.Title,
            InitialMessage: req.InitialMessage,
            AgentId: req.AgentId,
            AgentType: req.AgentType, // Issue #4362
            UserRole: session.User!.Role,
            SelectedKnowledgeBaseIds: req.SelectedKnowledgeBaseIds
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Created($"/api/v1/chat-threads/{result.Id}", result);
    }

    private static async Task<IResult> HandleDeleteChatThread(
        Guid threadId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        var command = new DeleteChatThreadCommand(threadId, userId);
        await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("User {UserId} deleted thread {ThreadId}", userId, threadId);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleCloseThread(
        Guid threadId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        // Verify thread ownership before mutation
        var threadQuery = new GetChatThreadByIdQuery(threadId);
        var existingThread = await mediator.Send(threadQuery, ct).ConfigureAwait(false);

        if (existingThread == null)
        {
            return Results.NotFound(new { error = "Thread not found" });
        }

        if (existingThread.UserId != userId &&
            !string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("User {UserId} denied access to close thread {ThreadId} (owner: {OwnerId})",
                userId, threadId, existingThread.UserId);
            return Results.Forbid();
        }

        var command = new CloseThreadCommand(threadId);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleReopenThread(
        Guid threadId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        // Verify thread ownership before mutation
        var threadQuery = new GetChatThreadByIdQuery(threadId);
        var existingThread = await mediator.Send(threadQuery, ct).ConfigureAwait(false);

        if (existingThread == null)
        {
            return Results.NotFound(new { error = "Thread not found" });
        }

        if (existingThread.UserId != userId &&
            !string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("User {UserId} denied access to reopen thread {ThreadId} (owner: {OwnerId})",
                userId, threadId, existingThread.UserId);
            return Results.Forbid();
        }

        var command = new ReopenThreadCommand(threadId);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUpdateChatThreadTitle(
        Guid threadId,
        UpdateChatThreadTitleRequest req,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        if (string.IsNullOrWhiteSpace(req.Title))
        {
            return Results.BadRequest(new { error = "Title is required" });
        }

        // Verify thread ownership before mutation
        var threadQuery = new GetChatThreadByIdQuery(threadId);
        var existingThread = await mediator.Send(threadQuery, ct).ConfigureAwait(false);

        if (existingThread == null)
        {
            return Results.NotFound(new { error = "Thread not found" });
        }

        if (existingThread.UserId != userId &&
            !string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("User {UserId} denied access to update thread {ThreadId} (owner: {OwnerId})",
                userId, threadId, existingThread.UserId);
            return Results.Forbid();
        }

        var command = new UpdateChatThreadTitleCommand(threadId, req.Title);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("User {UserId} updated title for thread {ThreadId}", userId, threadId);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleSwitchThreadAgent(
        Guid threadId,
        SwitchThreadAgentRequest req,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        if (string.IsNullOrWhiteSpace(req.AgentType))
        {
            return Results.BadRequest(new { error = "AgentType is required" });
        }

        var threadQuery = new GetChatThreadByIdQuery(threadId);
        var existingThread = await mediator.Send(threadQuery, ct).ConfigureAwait(false);

        if (existingThread == null)
        {
            return Results.NotFound(new { error = "Thread not found" });
        }

        if (existingThread.UserId != userId &&
            !string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("User {UserId} denied access to switch agent on thread {ThreadId} (owner: {OwnerId})",
                userId, threadId, existingThread.UserId);
            return Results.Forbid();
        }

        var command = new SwitchThreadAgentCommand(threadId, req.AgentType);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("User {UserId} switched agent to {AgentType} on thread {ThreadId}",
            userId, req.AgentType, threadId);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleAddMessageToThread(
        Guid threadId,
        AddMessageRequest req,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        if (string.IsNullOrWhiteSpace(req.Content))
        {
            return Results.BadRequest(new { error = "Content is required" });
        }

        var userId = session!.User!.Id;

        // Verify thread ownership before mutation
        var threadQuery = new GetChatThreadByIdQuery(threadId);
        var existingThread = await mediator.Send(threadQuery, ct).ConfigureAwait(false);

        if (existingThread == null)
        {
            return Results.NotFound(new { error = "Thread not found" });
        }

        if (existingThread.UserId != userId &&
            !string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
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

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUpdateChatMessage(
        Guid threadId,
        Guid messageId,
        UpdateMessageRequest req,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        if (string.IsNullOrWhiteSpace(req.Content))
        {
            return Results.BadRequest(new { error = "Content is required" });
        }

        var command = new UpdateMessageCommand(threadId, messageId, req.Content, userId);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("User {UserId} updated message {MessageId} in thread {ThreadId}",
            userId, messageId, threadId);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleDeleteChatMessage(
        Guid threadId,
        Guid messageId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;
        var isAdmin = string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

        var command = new DeleteMessageCommand(threadId, messageId, userId, isAdmin);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("User {UserId} deleted message {MessageId} from thread {ThreadId} (admin: {IsAdmin})",
            userId, messageId, threadId, isAdmin);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleExportChatThread(
        Guid threadId,
        [FromQuery] string? format,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        // Verify thread ownership before export
        var threadQuery = new GetChatThreadByIdQuery(threadId);
        var existingThread = await mediator.Send(threadQuery, ct).ConfigureAwait(false);

        if (existingThread == null)
        {
            return Results.NotFound(new { error = "Thread not found" });
        }

        if (existingThread.UserId != userId &&
            !string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("User {UserId} denied access to export thread {ThreadId} (owner: {OwnerId})",
                userId, threadId, existingThread.UserId);
            return Results.Forbid();
        }

        // Default to JSON if no format specified
        var exportFormat = format?.ToLowerInvariant() ?? "json";

        var command = new ExportChatCommand(threadId, exportFormat);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        // Generate filename for logging purposes
        var threadTitle = existingThread.Title?.Replace(" ", "_") ?? "chat";
        var sanitizedTitle = string.Concat(threadTitle.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'));
        var filename = $"{sanitizedTitle}_{threadId.ToString()[..8]}.{result.FileExtension}";

        logger.LogInformation("User {UserId} exported thread {ThreadId} in {Format} format as {Filename}",
            userId, threadId, result.Format, filename);

        return Results.Content(
            result.Content,
            result.ContentType,
            null,
            statusCode: 200);
    }

    private static void MapContextEngineeringEndpoints(RouteGroupBuilder group)
    {
        // ISSUE-3491: Context Engineering Framework - Assemble context from multiple sources
        group.MapPost("/context-engineering/assemble", HandleAssembleContext)
            .WithName("AssembleContext")
            .RequireSession()
            .WithTags("ContextEngineering")
            .WithDescription("Assemble context from multiple sources for AI agent prompts");

        // ISSUE-3491: Context Engineering Framework - Get available context sources
        group.MapGet("/context-engineering/sources", HandleGetContextSources)
            .WithName("GetContextSources")
            .RequireSession()
            .WithTags("ContextEngineering")
            .WithDescription("Retrieve available context sources and their metadata");
    }

    private static async Task<IResult> HandleAssembleContext(
        AssembleContextRequest req,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        var command = new AssembleContextCommand(
            Query: req.Query,
            GameId: req.GameId,
            UserId: userId,
            SessionId: req.SessionId,
            MaxTotalTokens: req.MaxTotalTokens ?? 8000,
            MinRelevance: req.MinRelevance ?? 0.5,
            SourcePriorities: req.SourcePriorities,
            MinTokensPerSource: req.MinTokensPerSource,
            MaxTokensPerSource: req.MaxTokensPerSource,
            IncludeEmbedding: req.IncludeEmbedding ?? true
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation(
            "User {UserId} assembled context: {ItemCount} items, {TokenCount} tokens, {DurationMs}ms",
            userId, result.Items.Count, result.TotalTokens, result.Metrics.TotalDurationMs);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetContextSources(
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var query = new GetContextSourcesQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        logger.LogInformation("Retrieved {SourceCount} context sources", result.Count);

        return Results.Ok(result);
    }

    private static void MapGameDocumentsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/knowledge-base/{gameId:guid}/documents", HandleGetGameDocuments)
            .WithName("GetGameDocuments")
            .RequireSession()
            .WithTags("KnowledgeBase")
            .WithSummary("Get KB documents for a game")
            .WithDescription("Returns the list of KB documents linked to a game, ordered by creation date descending.")
            .Produces<IReadOnlyList<GameDocumentDto>>()
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static void MapLinkKbEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/games/{gameId:guid}/knowledge-base/link", HandleLinkKb)
            .WithName("LinkExistingKbToGame")
            .RequireSession()
            .WithTags("KnowledgeBase")
            .WithSummary("Link an existing KB to a game")
            .WithDescription("Creates a VectorDocument clone linking an existing processed PDF to a different game. The pgvector embeddings are shared.");
    }

    private static async Task<IResult> HandleLinkKb(
        Guid gameId,
        LinkKbRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        var result = await mediator.Send(new LinkExistingKbToGameCommand(
            UserId: session.User!.Id,
            TargetGameId: gameId,
            SourcePdfDocumentId: request.PdfDocumentId), ct).ConfigureAwait(false);

        return string.Equals(result.Status, "linked", StringComparison.Ordinal)
            ? Results.Ok(result)
            : Results.Accepted(value: result);
    }

    private static async Task<IResult> HandleGetGameDocuments(
        Guid gameId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = context.Items[nameof(SessionStatusDto)] as SessionStatusDto;
        if (session?.User?.Id is not Guid userId)
        {
            return Results.Unauthorized();
        }

        logger.LogDebug("GetGameDocuments for game {GameId} by user {UserId}", gameId, userId);

        var query = new GetGameDocumentsQuery(gameId, userId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }
}

/// <summary>
/// Request model for knowledge base search.
/// </summary>
internal record KnowledgeBaseSearchRequest(
    string gameId,
    string query,
    int? topK = null,
    double? minScore = null,
    string? searchMode = null,
    string? language = null
);

/// <summary>
/// Request model for updating chat thread title (Issue #2257).
/// </summary>
internal record UpdateChatThreadTitleRequest(
    string Title
);

/// <summary>
/// Request model for switching agent type on a chat thread (Issue #4465).
/// </summary>
internal record SwitchThreadAgentRequest(
    string AgentType
);

/// <summary>
/// Request model for context assembly (Issue #3491).
/// </summary>
internal record AssembleContextRequest(
    string Query,
    Guid? GameId = null,
    Guid? SessionId = null,
    int? MaxTotalTokens = null,
    double? MinRelevance = null,
    IDictionary<string, int>? SourcePriorities = null,
    IDictionary<string, int>? MinTokensPerSource = null,
    IDictionary<string, int>? MaxTokensPerSource = null,
    bool? IncludeEmbedding = null
);

/// <summary>
/// Request model for linking an existing KB (processed PDF) to a different game.
/// </summary>
internal record LinkKbRequest(Guid PdfDocumentId);