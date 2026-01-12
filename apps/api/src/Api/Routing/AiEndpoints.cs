using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Configuration;
using Api.Extensions;
using Api.Helpers;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using UserDto = Api.BoundedContexts.Authentication.Application.DTOs.UserDto;

namespace Api.Routing;

/// <summary>
/// AI and Agent endpoints.
/// Handles RAG QA, explain, setup guide, chess agent, BGG API, and chess knowledge indexing.
/// </summary>
internal static class AiEndpoints
{
    public static RouteGroupBuilder MapAiEndpoints(this RouteGroupBuilder group)
    {
        MapQaEndpoint(group);

        MapExplainEndpoint(group);

        MapExplainStreamEndpoint(group);

        MapQaStreamEndpoint(group);

        MapSetupGuideEndpoint(group);

        MapAgentFeedbackEndpoint(group);

        MapChessAgentEndpoint(group);

        MapBggEndpoints(group);

        MapChessKnowledgeEndpoints(group);

        // UI-01: Chat management endpoints
        return group;
    }

    private static void MapQaEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/qa", HandleQaRequest)
        .WithName("QaAgent")
        .WithDescription("Ask a question about game rules using RAG (Retrieval-Augmented Generation)")
        .WithTags("AI Agents")
        .Produces<QaResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status500InternalServerError)
        .RequireSession(); // Issue #1446: Automatic session validation
    }

    private static void MapExplainEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/explain", HandleExplainRequest)
        .RequireSession(); // Issue #1446: Automatic session validation
    }

    private static void MapBggEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/bgg/search", HandleBggSearch)
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapGet("/bgg/games/{bggId:int}", HandleGetBggGameDetails)
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapPost("/bgg/thumbnails", HandleBatchThumbnails)
        .RequireSession(); // Batch thumbnail loading for search results
    }

    private static void MapChessKnowledgeEndpoints(RouteGroupBuilder group)
    {
        // Migrated to CQRS: Uses IndexChessKnowledgeCommand via MediatR (Issue #1188)
        // Migrated to CQRS: Uses IndexChessKnowledgeCommand via MediatR (Issue #1188)
        group.MapPost("/chess/index", HandleIndexChessKnowledge)
        .RequireAdminSession(); // Issue #1446: Admin-only endpoint

        // Migrated to CQRS: Uses SearchChessKnowledgeQuery via MediatR (Issue #1188)
        // Migrated to CQRS: Uses SearchChessKnowledgeQuery via MediatR (Issue #1188)
        group.MapGet("/chess/search", HandleSearchChessKnowledge)
        .RequireSession(); // Issue #1446: Automatic session validation

        // Migrated to CQRS: Uses DeleteChessKnowledgeCommand via MediatR (Issue #1188)
        // Migrated to CQRS: Uses DeleteChessKnowledgeCommand via MediatR (Issue #1188)
        group.MapDelete("/chess/index", HandleDeleteChessKnowledge)
        .RequireAdminSession(); // Issue #1446: Admin-only endpoint
    }

    private static void MapSetupGuideEndpoint(RouteGroupBuilder group)
    {
        // AI-03: RAG Setup Guide endpoint (Streaming)
        // Migrated to CQRS: Uses StreamSetupGuideQuery via MediatR with SSE streaming (Issue #1186)
        group.MapPost("/agents/setup", HandleSetupGuide);
    }

    private static void MapAgentFeedbackEndpoint(RouteGroupBuilder group)
    {
        // Migrated to CQRS: Uses ProvideAgentFeedbackCommand via MediatR (Issue #1188)
        group.MapPost("/agents/feedback", HandleAgentFeedback)
        .RequireSession(); // Issue #1446: Automatic session validation
    }

    private static void MapChessAgentEndpoint(RouteGroupBuilder group)
    {
        // CHESS-04: Chess conversational agent endpoint
        // Migrated to CQRS: Uses InvokeChessAgentCommand via MediatR (Issue #1188)
        group.MapPost("/agents/chess", HandleChessAgent)
        .RequireSession(); // Issue #1446: Automatic session validation
    }

    private static void MapExplainStreamEndpoint(RouteGroupBuilder group)
    {
        // API-02: Streaming RAG Explain endpoint (SSE)
        // Migrated to CQRS: Uses StreamExplainQuery via MediatR (Issue #1186)
        group.MapPost("/agents/explain/stream", HandleExplainStream);
    }

    private static void MapQaStreamEndpoint(RouteGroupBuilder group)
    {
        // CHAT-01: Streaming QA endpoint (SSE)
        // Migrated to CQRS: Uses StreamQaQuery via MediatR (Issue #1186)
        group.MapPost("/agents/qa/stream", HandleQaStreamAsync);
    }

    private static async Task<IResult> HandleQaStreamAsync(
        QaRequest req,
        HttpContext context,
        IMediator mediator,
        IOptions<FollowUpQuestionsConfiguration> followUpConfig, // CHAT-02
        IFeatureFlagService featureFlags, // CONFIG-05
        ILogger<Program> logger,
        bool generateFollowUps = true, // CHAT-02: opt-in parameter (Issue #1188)
        CancellationToken ct = default)
    {
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        // CONFIG-05: Check if streaming responses feature is enabled
        if (!await featureFlags.IsEnabledAsync("Features.StreamingResponses").ConfigureAwait(false))
        {
            return Results.Json(
                new { error = "feature_disabled", message = "Streaming responses are currently disabled", featureName = "Features.StreamingResponses" },
                statusCode: 403);
        }

        if (string.IsNullOrWhiteSpace(req.gameId))
        {
            return Results.BadRequest(new { error = "gameId is required" });
        }

        // CHAT-02: Apply global feature flag for follow-up questions
        var config = followUpConfig.Value;
        if (config == null)
        {
            logger.LogWarning("FollowUpQuestionsConfiguration is null, disabling follow-up generation");
            generateFollowUps = false;
        }
        else
        {
            generateFollowUps = generateFollowUps && config.Enabled;
        }

        var startTime = DateTime.UtcNow;
        logger.LogInformation("Streaming QA request from user {UserId} for game {GameId}: {Query}",
            session.User!.Id, req.gameId, req.query);

        // Set SSE headers
        context.Response.Headers["Content-Type"] = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["Connection"] = "keep-alive";

        var streamContext = new QaStreamContext();

        try
        {
            await ExecuteQaStreamingAsync(req, context, mediator, logger, generateFollowUps, streamContext, ct).ConfigureAwait(false);
            logger.LogInformation("Streaming QA completed for game {GameId}, query: {Query}", req.gameId, req.query);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogInformation(ex, "Streaming QA cancelled by client for game {GameId}, query: {Query}", req.gameId, req.query);
            streamContext.StreamStatus = "Cancelled";
            streamContext.StreamErrorMessage = "Client disconnected";
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Streaming generator boundary - must handle all errors gracefully without throwing
        // All expected exceptions are caught above; this ensures cleanup and error event on unexpected errors
        catch (Exception ex)
        {
            // Top-level API endpoint handler: Catches all exceptions for SSE streaming endpoint
            // Sends error event to client stream, specific exception handling in service layer
            logger.LogError(ex, "Error during streaming QA for game {GameId}, query: {Query}", req.gameId, req.query);
            streamContext.StreamStatus = "Error";
            streamContext.StreamErrorMessage = ex.Message;

            // Send error event if possible
            try
            {
                var errorEvent = new RagStreamingEvent(
                    StreamingEventType.Error,
                    new StreamingError($"An error occurred: {ex.Message}", "INTERNAL_ERROR"),
                    DateTime.UtcNow);
                var json = System.Text.Json.JsonSerializer.Serialize(errorEvent);
                await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: Cleanup operation - must not throw during disposal/cleanup
            // Error event sending failure is logged but suppressed to ensure graceful stream termination
            catch
            {
                // If we can't send error event, client connection is likely broken
            }
#pragma warning restore CA1031
        }
#pragma warning restore CA1031

        await LogQaRequestAsync(req, context, mediator, session, startTime, streamContext).ConfigureAwait(false);

        return Results.Empty;
    }

    private static async Task ExecuteQaStreamingAsync(
        QaRequest req,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        bool generateFollowUps,
        QaStreamContext streamContext,
        CancellationToken ct)
    {
        // CHAT-02: Follow-up question generation (fire-and-forget after Complete event)
        Task<IReadOnlyList<string>>? followUpTask = null;

        var query = new StreamQaQuery(req.gameId, req.query, req.chatId, req.documentIds); // Issue #2051
        await foreach (var evt in mediator.CreateStream(query, ct).ConfigureAwait(false))
        {
            // Serialize event as JSON
            var json = System.Text.Json.JsonSerializer.Serialize(evt);

            // Write SSE format: "data: {json}\n\n"
            await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
            await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);

            // Track response data for logging and chat persistence
            // Issue #1186: Handlers now emit strongly-typed objects, not JsonElements
            if (evt.Type == StreamingEventType.Token && evt.Data is StreamingToken tokenData)
            {
                streamContext.AnswerBuilder.Append(tokenData.token);
            }
            else if (evt.Type == StreamingEventType.Citations && evt.Data is StreamingCitations citationsData)
            {
                streamContext.Snippets = citationsData.citations.ToList();
            }
            else if (evt.Type == StreamingEventType.Complete && evt.Data is StreamingComplete completeData)
            {
                streamContext.TotalTokens = completeData.totalTokens;
                streamContext.Confidence = completeData.confidence;

                // CHAT-02: Start follow-up generation in parallel (fire-and-forget)
                if (generateFollowUps && followUpTask == null)
                {
                    var answer = streamContext.AnswerBuilder.ToString();
                    followUpTask = StartFollowUpGenerationAsync(req.gameId, req.query, answer, streamContext.Snippets, mediator, logger, ct);
                }
            }
        }

        // CHAT-02: Wait for follow-up questions and send event
        if (followUpTask != null)
        {
            await SendFollowUpQuestionsEventAsync(followUpTask, context, req.gameId, logger, ct).ConfigureAwait(false);
        }
    }

    private static async Task LogQaRequestAsync(
        QaRequest req,
        HttpContext context,
        IMediator mediator,
        SessionStatusDto session,
        DateTime startTime,
        QaStreamContext streamContext)
    {
        var answer = streamContext.AnswerBuilder.ToString();
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Log AI request using CQRS - Use CancellationToken.None to ensure logging completes even if request was cancelled
        var logCommand = new Api.BoundedContexts.Administration.Application.Commands.LogAiRequestCommand(
            UserId: session.User!.Id.ToString(),
            GameId: req.gameId,
            Endpoint: "qa-stream",
            Query: req.query,
            ResponseSnippet: answer?.Length > 500 ? answer.Substring(0, 500) : answer,
            LatencyMs: latencyMs,
            TokenCount: streamContext.TotalTokens,
            Confidence: streamContext.Confidence,
            Status: streamContext.StreamStatus,
            ErrorMessage: streamContext.StreamErrorMessage,
            IpAddress: context.Connection.RemoteIpAddress?.ToString(),
            UserAgent: context.Request.Headers.UserAgent.ToString(),
            CompletionTokens: streamContext.TotalTokens
        );
        await mediator.Send(logCommand, CancellationToken.None).ConfigureAwait(false);
    }


    private static async Task<IResult> HandleQaRequest(
        QaRequest req,
        HttpContext context,
        IMediator mediator, // DDD/CQRS: Use IMediator only
        IOptions<FollowUpQuestionsConfiguration> followUpConfig, // CHAT-02
        ILogger<Program> logger,
        bool bypassCache = false,
        bool generateFollowUps = true, // CHAT-02: opt-in parameter
        CancellationToken ct = default)
    {
        // Session validated by RequireSessionFilter
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        if (string.IsNullOrWhiteSpace(req.gameId))
        {
            return Results.BadRequest(new { error = "gameId is required" });
        }

        var startTime = DateTime.UtcNow;
        var config = followUpConfig.Value;
        if (config == null)
        {
            logger.LogWarning("FollowUpQuestionsConfiguration is null, disabling follow-up generation");
            generateFollowUps = false;
        }
        else
        {
            generateFollowUps = generateFollowUps && config.Enabled; // Apply global feature flag
        }

        logger.LogInformation("QA request from user {UserId} for game {GameId}: {Query} (bypassCache: {BypassCache}, generateFollowUps: {GenerateFollowUps})",
            session.User!.Id, req.gameId, req.query, bypassCache, generateFollowUps);

        // DDD/CQRS Migration: Use AskQuestionQuery via IMediator instead of IRagService
        if (!Guid.TryParse(req.gameId, out var gameGuid))
        {
            return Results.BadRequest(new { error = "Invalid game ID format" });
        }

        var askQuery = new BoundedContexts.KnowledgeBase.Application.Queries.AskQuestionQuery(
            GameId: gameGuid,
            Question: req.query,
            ThreadId: null, // No thread context for legacy endpoint
            SearchMode: req.searchMode.ToString(),
            Language: "en",
            BypassCache: bypassCache
        );

        var qaResponse = await mediator.Send(askQuery, ct).ConfigureAwait(false);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Map QaResponseDto to legacy QaResponse format (for backward compatibility)
        var snippets = qaResponse.Sources.Select(src => new Snippet(
            text: src.TextContent,
            source: $"PDF:{src.VectorDocumentId}",
            page: src.PageNumber,
            line: 0,
            score: (float)src.RelevanceScore
        )).ToList();

        var resp = new QaResponse(
            answer: qaResponse.Answer,
            snippets: snippets,
            confidence: qaResponse.OverallConfidence
        );

        // CHAT-02: Generate follow-up questions if enabled
        IReadOnlyList<string>? followUpQuestions = null;
        if (generateFollowUps)
        {
            followUpQuestions = await GenerateFollowUpQuestionsAsync(req.gameId, req.query, resp.answer, resp.snippets, mediator, ct).ConfigureAwait(false);
            if (followUpQuestions != null)
            {
                logger.LogInformation("Generated {Count} follow-up questions for game {GameId}", followUpQuestions.Count, req.gameId);
            }
        }

        // Create response with follow-up questions
        var finalResponse = new QaResponse(
            resp.answer,
            resp.snippets,
            resp.promptTokens,
            resp.completionTokens,
            resp.totalTokens,
            resp.confidence,
            resp.metadata,
            followUpQuestions); // CHAT-02

        logger.LogInformation("QA response delivered for game {GameId}", req.gameId);

        // AI-11: Build quality scores from handler-calculated metrics
        // Handler already calculated SearchConfidence, LlmConfidence, OverallConfidence via QualityTrackingDomainService
        // Only need to calculate CitationQuality here (simple ratio calculation)
        var citationQuality = CalculateCitationQuality(resp.snippets.Count, resp.answer);

        var qualityScores = new QualityScores
        {
            RagConfidence = qaResponse.SearchConfidence, // From handler
            LlmConfidence = qaResponse.LlmConfidence, // From handler
            CitationQuality = citationQuality, // Calculated locally
            OverallConfidence = qaResponse.OverallConfidence, // From handler
            IsLowQuality = qaResponse.IsLowQuality // From handler
        };

        // AI-11: Debug logging for quality scores
        logger.LogInformation(
            "Quality scores - RAG: {RagConf:F3}, LLM: {LlmConf:F3}, Citation: {CitConf:F3}, Overall: {OverallConf:F3}, IsLowQuality: {IsLowQuality}",
            qualityScores.RagConfidence,
            qualityScores.LlmConfidence,
            qualityScores.CitationQuality,
            qualityScores.OverallConfidence,
            qualityScores.IsLowQuality);

        // ADM-01: Log AI request with AI-11 quality scores (using CQRS)
        await LogQaRequestAsync(session.User!.Id.ToString(), req.gameId, req.query, resp, latencyMs,
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            context.Request.Headers.UserAgent.ToString(),
            qualityScores, mediator, ct).ConfigureAwait(false);

        return Results.Json(finalResponse); // CHAT-02: Return response with follow-up questions
    }

    private static async Task<IResult> HandleExplainRequest(ExplainRequest req, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        // Session validated by RequireSessionFilter
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        if (string.IsNullOrWhiteSpace(req.gameId))
        {
            return Results.BadRequest(new { error = "gameId is required" });
        }

        var startTime = DateTime.UtcNow;
        logger.LogInformation("Explain request from user {UserId} for game {GameId}: {Topic}",
            session.User!.Id, req.gameId, req.topic);

        // DDD/CQRS Migration: Use ExplainQuery via IMediator instead of IRagService
        if (!Guid.TryParse(req.gameId, out var gameGuid))
        {
            return Results.BadRequest(new { error = "Invalid game ID format" });
        }

        var explainQuery = new BoundedContexts.KnowledgeBase.Application.Queries.ExplainQuery(
            GameId: gameGuid,
            Topic: req.topic,
            Language: "en"
        );

        var explainResponse = await mediator.Send(explainQuery, ct).ConfigureAwait(false);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Map ExplainResponseDto to legacy ExplainResponse format (for backward compatibility)
        var outlineParts = explainResponse.Outline.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var mainTopic = outlineParts.Length > 0 ? outlineParts[0] : req.topic;
        var sections = outlineParts.Skip(1)
            .Select(s => s.TrimStart('•', ' ', '\t'))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var outline = new ExplainOutline(mainTopic, sections);

        var snippets = explainResponse.Citations.Select(c => new Snippet(
            text: c.Snippet,
            source: $"PDF:{c.DocumentId}",
            page: c.PageNumber,
            line: 0,
            score: (float)c.RelevanceScore
        )).ToList();

        var resp = new ExplainResponse(
            outline: outline,
            script: explainResponse.Script,
            citations: snippets,
            estimatedReadingTimeMinutes: (int)Math.Ceiling(explainResponse.EstimatedReadingTimeSeconds / 60.0),
            promptTokens: 0, // Not tracked in new handler
            completionTokens: 0,
            totalTokens: 0,
            confidence: explainResponse.Confidence
        );

        logger.LogInformation("Explain response delivered for game {GameId}, estimated {Minutes} min read",
            req.gameId, resp.estimatedReadingTimeMinutes);

        // ADM-01: Log AI request using CQRS
        var logCommand = new Api.BoundedContexts.Administration.Application.Commands.LogAiRequestCommand(
            UserId: session.User!.Id.ToString(),
            GameId: req.gameId,
            Endpoint: "explain",
            Query: req.topic,
            ResponseSnippet: resp.script?.Length > 500 ? resp.script.Substring(0, 500) : resp.script,
            LatencyMs: latencyMs,
            TokenCount: resp.totalTokens,
            Confidence: resp.confidence,
            Status: "Success",
            ErrorMessage: null,
            IpAddress: context.Connection.RemoteIpAddress?.ToString(),
            UserAgent: context.Request.Headers.UserAgent.ToString(),
            PromptTokens: resp.promptTokens,
            CompletionTokens: resp.completionTokens,
            Model: null,
            FinishReason: null
        );
        await mediator.Send(logCommand, ct).ConfigureAwait(false);

        return Results.Json(resp);
    }

    private static async Task<IResult> HandleBggSearch(
        [FromQuery] string? q,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct,
        [FromQuery] bool exact = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Session validated by RequireSessionFilter

        // Issue #1445: Use centralized query validation
        var queryError = QueryValidator.ValidateQuery(q);
        if (queryError != null)
        {
            return Results.BadRequest(new { error = queryError });
        }

        // After validation, q is guaranteed to be non-null
        var validatedQuery = q!;

        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100; // Max 100 per page

        // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
        var query = new Api.BoundedContexts.GameManagement.Application.Queries.BggApi.SearchBggGamesQuery
        {
            Query = validatedQuery,
            Exact = exact
        };
        var allResults = await mediator.Send(query, ct).ConfigureAwait(false);

        // Apply pagination
        var total = allResults.Count;
        var totalPages = (int)Math.Ceiling((double)total / pageSize);
        var paginatedResults = allResults
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        logger.LogInformation("BGG search returned {Total} results (page {Page}/{TotalPages}) for query: {Query}",
            total, page, totalPages, validatedQuery);

        return Results.Json(new
        {
            results = paginatedResults,
            total,
            page,
            pageSize,
            totalPages
        });
    }

    private static async Task<IResult> HandleGetBggGameDetails(
        int bggId,
                IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Session validated by RequireSessionFilter

        // Validate BGG ID
        if (bggId <= 0)
        {
            return Results.BadRequest(new { error = "Invalid BGG ID. Must be a positive integer." });
        }

        // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
        var query = new Api.BoundedContexts.GameManagement.Application.Queries.BggApi.GetBggGameDetailsQuery
        {
            BggId = bggId
        };
        var details = await mediator.Send(query, ct).ConfigureAwait(false);

        if (details == null)
        {
            logger.LogWarning("BGG game not found: {BggId}", bggId);
            return Results.NotFound(new { error = $"Game with BGG ID {bggId} not found" });
        }

        logger.LogInformation("BGG game details retrieved: {BggId}, {Name}", bggId, details.Name);
        return Results.Json(details);
    }

    private static async Task<IResult> HandleBatchThumbnails(
        [FromBody] int[] bggIds,
        [FromServices] IBggApiService bggService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Session validated by RequireSessionFilter

        if (bggIds == null || bggIds.Length == 0)
        {
            return Results.BadRequest(new { error = "bggIds array is required and cannot be empty" });
        }

        if (bggIds.Length > 20)
        {
            return Results.BadRequest(new { error = "Maximum 20 IDs per batch request" });
        }

        try
        {
            // Fetch thumbnails for each ID (already cached, so fast)
            var thumbnails = new Dictionary<int, string?>();
            foreach (var id in bggIds)
            {
                var details = await bggService.GetGameDetailsAsync(id, ct).ConfigureAwait(false);
                thumbnails[id] = details?.ThumbnailUrl;
            }

            logger.LogInformation("Loaded {Count} thumbnails for BGG IDs", thumbnails.Count);
            return Results.Json(thumbnails);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load batch thumbnails");
            return Results.Problem("Failed to load thumbnails");
        }
    }

    private static async Task<IResult> HandleIndexChessKnowledge(HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        // Session validated AND Admin role checked by RequireAdminSessionFilter
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        logger.LogInformation("Admin {UserId} starting chess knowledge indexing", session.User!.Id);

        var result = await mediator.Send(new IndexChessKnowledgeCommand(), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogError("Chess knowledge indexing failed: {Error}", result.ErrorMessage);
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        logger.LogInformation("Chess knowledge indexing completed: {TotalItems} items, {TotalChunks} chunks",
            result.TotalKnowledgeItems, result.TotalChunks);

        return Results.Json(new
        {
            success = true,
            totalItems = result.TotalKnowledgeItems,
            totalChunks = result.TotalChunks,
            categoryCounts = result.CategoryCounts
        });
    }

    private static async Task<IResult> HandleSearchChessKnowledge(string? q, int? limit, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        // Session validated by RequireSessionFilter
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        // Issue #1445: Use centralized query validation
        var queryError = QueryValidator.ValidateQuery(q);
        if (queryError != null)
        {
            return Results.BadRequest(new { error = queryError });
        }
        var validatedQuery = q!;

        logger.LogInformation("User {UserId} searching chess knowledge: {Query}", session.User!.Id, validatedQuery);

        var searchResult = await mediator.Send(new SearchChessKnowledgeQuery
        {
            Query = validatedQuery,
            Limit = limit ?? 5
        }, ct).ConfigureAwait(false);

        if (!searchResult.Success)
        {
            logger.LogError("Chess knowledge search failed: {Error}", searchResult.ErrorMessage);
            return Results.BadRequest(new { error = searchResult.ErrorMessage });
        }

        logger.LogInformation("Chess knowledge search completed: {ResultCount} results", searchResult.Results.Count);

        return Results.Json(new
        {
            success = true,
            results = searchResult.Results.Select(r => new
            {
                score = r.Score,
                text = r.Text,
                page = r.Page,
                chunkIndex = r.ChunkIndex
            })
        });
    }

    private static async Task<IResult> HandleDeleteChessKnowledge(HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        // Session validated AND Admin role checked by RequireAdminSessionFilter
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        logger.LogInformation("Admin {UserId} deleting all chess knowledge", session.User!.Id);

        var success = await mediator.Send(new DeleteChessKnowledgeCommand(), ct).ConfigureAwait(false);

        if (!success)
        {
            logger.LogError("Chess knowledge deletion failed");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        logger.LogInformation("Chess knowledge deletion completed successfully");
        return Results.Json(new { success = true });
    }

    private static async Task<IResult> HandleSetupGuide(
        SetupGuideRequest req,
        HttpContext context,
        IMediator mediator,
        IFeatureFlagService featureFlags,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        // CONFIG-05: Check if setup guide generation feature is enabled
        if (!await featureFlags.IsEnabledAsync("Features.SetupGuideGeneration").ConfigureAwait(false))
        {
            return Results.Json(
                new { error = "feature_disabled", message = "Setup guide generation is currently unavailable", featureName = "Features.SetupGuideGeneration" },
                statusCode: 403);
        }

        if (string.IsNullOrWhiteSpace(req.gameId))
        {
            return Results.BadRequest(new { error = "gameId is required" });
        }

        var startTime = DateTime.UtcNow;
        logger.LogInformation("Setup guide streaming request from user {UserId} for game {GameId}",
            session.User!.Id, req.gameId);

        // Set SSE headers for streaming
        context.Response.Headers["Content-Type"] = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["Connection"] = "keep-alive";

        var streamContext = new SetupGuideStreamContext();

        try
        {
            var query = new StreamSetupGuideQuery(req.gameId);
            await ExecuteSetupGuideStreamingAsync(query, context, mediator, streamContext, ct).ConfigureAwait(false);

            logger.LogInformation("Setup guide streaming completed for game {GameId}, {StepCount} steps, estimated {Minutes} min",
                req.gameId, streamContext.Steps.Count, streamContext.EstimatedTime);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogInformation(ex, "Setup guide streaming cancelled by client for game {GameId}", req.gameId);
            streamContext.StreamStatus = "Cancelled";
            streamContext.StreamErrorMessage = "Client disconnected";
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Streaming generator boundary - must handle all errors gracefully without throwing
        // All expected exceptions are caught above; this ensures cleanup and error event on unexpected errors
        catch (Exception ex)
        {
            // Top-level API endpoint handler: Catches all exceptions for SSE streaming endpoint
            // Sends error event to client stream, specific exception handling in service layer
            logger.LogError(ex, "Error during setup guide streaming for game {GameId}", req.gameId);
            streamContext.StreamStatus = "Error";
            streamContext.StreamErrorMessage = ex.Message;

            // Send error event if possible
            try
            {
                var errorEvent = new RagStreamingEvent(
                    StreamingEventType.Error,
                    new StreamingError($"An error occurred: {ex.Message}", "INTERNAL_ERROR"),
                    DateTime.UtcNow);
                var json = System.Text.Json.JsonSerializer.Serialize(errorEvent);
                await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                // If we can't send error event, client connection is likely broken
            }
        }
#pragma warning restore CA1031

        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        await LogSetupGuideRequestAsync(req.gameId, session.User!.Id.ToString(), context, mediator, streamContext, latencyMs, ct).ConfigureAwait(false);

        return Results.Empty;
    }

    private static async Task<IResult> HandleAgentFeedback(AgentFeedbackRequest req, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        // Session validated by RequireSessionFilter
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        if (!string.Equals(req.userId, session.User!.Id.ToString(), StringComparison.Ordinal))
        {
            return Results.BadRequest(new { error = "Invalid user" });
        }

        if (string.IsNullOrWhiteSpace(req.messageId) || string.IsNullOrWhiteSpace(req.endpoint))
        {
            return Results.BadRequest(new { error = "messageId and endpoint are required" });
        }

        // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
        await mediator.Send(new ProvideAgentFeedbackCommand
        {
            MessageId = req.messageId,
            Endpoint = req.endpoint,
            UserId = session.User!.Id.ToString(),
            Outcome = string.IsNullOrWhiteSpace(req.outcome) ? null : req.outcome,
            GameId = req.gameId
        }, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Recorded feedback {Outcome} for message {MessageId} on endpoint {Endpoint} by user {UserId}",
            req.outcome ?? "cleared",
            req.messageId,
            req.endpoint,
            session.User!.Id);

        return Results.Json(new { ok = true });
    }

    private static async Task<IResult> HandleChessAgent(ChessAgentRequest req, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        // Session validated by RequireSessionFilter
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        // Issue #1445: Use centralized query validation
        var queryError = QueryValidator.ValidateQuery(req.question);
        if (queryError != null)
        {
            return Results.BadRequest(new { error = queryError });
        }

        var startTime = DateTime.UtcNow;
        logger.LogInformation("Chess agent request from user {UserId}: {Question}, FEN: {FEN}",
            session.User!.Id, req.question, req.fenPosition ?? "none");

        // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
        var resp = await mediator.Send(new InvokeChessAgentCommand
        {
            Question = req.question,
            FenPosition = req.fenPosition,
            ChatId = req.chatId
        }, ct).ConfigureAwait(false);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("Chess agent response delivered: {MoveCount} moves suggested",
            resp.suggestedMoves.Count);

        // ADM-01: Log AI request using CQRS
        await LogChessAgentRequestAsync(session.User!.Id.ToString(), req.question, resp, latencyMs,
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            context.Request.Headers.UserAgent.ToString(),
            mediator, ct).ConfigureAwait(false);

        return Results.Json(resp);
    }

    private static async Task<IResult> HandleExplainStream(ExplainRequest req, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        if (string.IsNullOrWhiteSpace(req.gameId))
        {
            return Results.BadRequest(new { error = "gameId is required" });
        }

        logger.LogInformation("Streaming explain request from user {UserId} for game {GameId}: {Topic}",
            session.User!.Id, req.gameId, req.topic);

        // Set SSE headers
        context.Response.Headers["Content-Type"] = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["Connection"] = "keep-alive";

        try
        {
            var query = new StreamExplainQuery(req.gameId, req.topic);
            await ExecuteExplainStreamingAsync(query, context, mediator, ct).ConfigureAwait(false);

            logger.LogInformation("Streaming explain completed for game {GameId}, topic: {Topic}", req.gameId, req.topic);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogInformation(ex, "Streaming explain cancelled by client for game {GameId}, topic: {Topic}", req.gameId, req.topic);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Streaming generator boundary - must handle all errors gracefully without throwing
        // All expected exceptions are caught above; this ensures cleanup and error event on unexpected errors
        catch (Exception ex)
        {
            // Top-level API endpoint handler: Catches all exceptions for SSE streaming endpoint
            // Sends error event to client stream, specific exception handling in service layer
            logger.LogError(ex, "Error during streaming explain for game {GameId}, topic: {Topic}", req.gameId, req.topic);

            // Send error event if possible
            try
            {
                var errorEvent = new RagStreamingEvent(
                    StreamingEventType.Error,
                    new StreamingError($"An error occurred: {ex.Message}", "INTERNAL_ERROR"),
                    DateTime.UtcNow);
                var json = System.Text.Json.JsonSerializer.Serialize(errorEvent);
                await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: Cleanup operation - must not throw during disposal/cleanup
            // Error event sending failure is logged but suppressed to ensure graceful stream termination
            catch
            {
                // If we can't send error event, client connection is likely broken
            }
#pragma warning restore CA1031
        }
#pragma warning restore CA1031

        return Results.Empty;
    }

    private sealed class QaStreamContext
    {
        public StringBuilder AnswerBuilder { get; } = new();
        public int TotalTokens { get; set; }
        public double? Confidence { get; set; }
        public List<Snippet> Snippets { get; set; } = new();
        public string StreamStatus { get; set; } = "Success";
        public string? StreamErrorMessage { get; set; }
    }

    private sealed class SetupGuideStreamContext
    {
        public List<SetupGuideStep> Steps { get; } = new();
        public int TotalTokens { get; set; }
        public double? Confidence { get; set; }
        public int EstimatedTime { get; set; }
        public string StreamStatus { get; set; } = "Success";
        public string? StreamErrorMessage { get; set; }
    }

    // Helpers for HandleQaRequest
    private static async Task<IReadOnlyList<string>?> GenerateFollowUpQuestionsAsync(
        string gameId,
        string query,
        string answer,
        IReadOnlyList<Snippet> snippets, // Uses Snippet directly
        IMediator mediator,
        CancellationToken ct)
    {
        var gameGuid = Guid.Parse(gameId);
        var gameDto = await mediator.Send(new GetGameByIdQuery(gameGuid), ct).ConfigureAwait(false);

        if (gameDto != null && !string.IsNullOrEmpty(gameDto.Title))
        {
            return await mediator.Send(new GenerateFollowUpQuestionsQuery
            {
                OriginalQuestion = query,
                GeneratedAnswer = answer,
                RagContext = snippets,
                GameName = gameDto.Title,
                MaxQuestions = 5
            }, ct).ConfigureAwait(false);
        }
        return null;
    }

    /// <summary>
    /// Calculate citation quality as ratio of citations to paragraphs.
    /// Simple heuristic: more citations relative to content = better quality.
    /// Capped at 1.0 (no penalty for over-citing).
    /// </summary>
    private static double CalculateCitationQuality(int citationCount, string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return 0.0;
        }

        // Count paragraphs (split by double newlines or single newlines)
        var paragraphs = responseText
            .Split(new[] { "\n\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        var paragraphCount = Math.Max(1, paragraphs.Length);

        // Calculate ratio and cap at 1.0
        return Math.Min(citationCount / (double)paragraphCount, 1.0);
    }

    private static async Task LogQaRequestAsync(
        string userId,
        string gameId,
        string query,
        QaResponse resp,
        int latencyMs,
        string ipAddress,
        string userAgent,
        QualityScores qualityScores,
        IMediator mediator,
        CancellationToken ct)
    {
        string? model = null;
        string? finishReason = null;
        if (resp.metadata != null)
        {
            resp.metadata.TryGetValue("model", out model);
            resp.metadata.TryGetValue("finish_reason", out finishReason);
        }

        var logCommand = new LogAiRequestCommand(
            UserId: userId,
            GameId: gameId,
            Endpoint: "qa",
            Query: query,
            ResponseSnippet: resp.answer?.Length > 500 ? resp.answer.Substring(0, 500) : resp.answer,
            LatencyMs: latencyMs,
            TokenCount: resp.totalTokens,
            Confidence: resp.confidence,
            Status: "Success",
            ErrorMessage: null,
            IpAddress: ipAddress,
            UserAgent: userAgent,
            PromptTokens: resp.promptTokens,
            CompletionTokens: resp.completionTokens,
            Model: model,
            FinishReason: finishReason,
            QualityScores: qualityScores
        );
        await mediator.Send(logCommand, ct).ConfigureAwait(false);
    }

    // Helpers for Streaming
    private static Task<IReadOnlyList<string>> StartFollowUpGenerationAsync(
        string gameId,
        string query,
        string answer,
        List<Snippet> snippets,
        IMediator mediator,
        ILogger logger,
        CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            try
            {
                var gameGuid = Guid.Parse(gameId);
                var gameDto = await mediator.Send(new GetGameByIdQuery(gameGuid), ct).ConfigureAwait(false);

                if (gameDto == null || string.IsNullOrEmpty(gameDto.Title))
                {
                    logger.LogWarning("Game {GameId} not found for follow-up generation", gameId);
                    return (IReadOnlyList<string>)new List<string>().AsReadOnly();
                }

                return await mediator.Send(new GenerateFollowUpQuestionsQuery
                {
                    OriginalQuestion = query,
                    GeneratedAnswer = answer,
                    RagContext = snippets,
                    GameName = gameDto.Title,
                    MaxQuestions = 5
                }, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating follow-up questions");
                return new List<string>().AsReadOnly();
            }
        });
    }

    private static async Task SendFollowUpQuestionsEventAsync(
        Task<IReadOnlyList<string>> followUpTask,
        HttpContext context,
        string gameId,
        ILogger logger,
        CancellationToken ct)
    {
        var followUpQuestions = await followUpTask.ConfigureAwait(false);
        if (followUpQuestions != null && followUpQuestions.Count > 0)
        {
            var followUpEvent = new RagStreamingEvent(
                StreamingEventType.FollowUpQuestions,
                new StreamingFollowUpQuestions(followUpQuestions),
                DateTime.UtcNow);
            var followUpJson = System.Text.Json.JsonSerializer.Serialize(followUpEvent);
            await context.Response.WriteAsync($"data: {followUpJson}\n\n", ct).ConfigureAwait(false);
            await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);

            logger.LogInformation("Sent {Count} follow-up questions for game {GameId}",
                followUpQuestions.Count, gameId);
        }
    }

    private static async Task ExecuteSetupGuideStreamingAsync(
        StreamSetupGuideQuery query,
        HttpContext context,
        IMediator mediator,
        SetupGuideStreamContext streamContext,
        CancellationToken ct)
    {
        await foreach (var evt in mediator.CreateStream(query, ct).ConfigureAwait(false))
        {
            // Serialize event as JSON
            var json = System.Text.Json.JsonSerializer.Serialize(evt);

            // Write SSE format: "data: {json}\n\n"
            await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
            await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);

            // Track data for chat persistence and logging
            if (evt.Type == StreamingEventType.SetupStep && evt.Data is System.Text.Json.JsonElement stepElement)
            {
                var stepData = System.Text.Json.JsonSerializer.Deserialize<StreamingSetupStep>(stepElement.GetRawText());
                if (stepData?.step != null)
                {
                    streamContext.Steps.Add(stepData.step);
                }
            }
            else if (evt.Type == StreamingEventType.Complete && evt.Data is System.Text.Json.JsonElement completeElement)
            {
                var completeData = System.Text.Json.JsonSerializer.Deserialize<StreamingComplete>(completeElement.GetRawText());
                if (completeData != null)
                {
                    streamContext.EstimatedTime = completeData.estimatedReadingTimeMinutes;
                    streamContext.TotalTokens = completeData.totalTokens;
                    streamContext.Confidence = completeData.confidence;
                }
            }
        }
    }

    private static async Task LogSetupGuideRequestAsync(
        string gameId,
        string userId,
        HttpContext context,
        IMediator mediator,
        SetupGuideStreamContext streamContext,
        int latencyMs,
        CancellationToken cancellationToken)
    {
        var responseSnippet = streamContext.Steps.Count > 0
            ? string.Join("; ", streamContext.Steps.Take(3).Select(s => s.instruction))
            : "No steps generated";
        if (responseSnippet.Length > 500)
        {
            responseSnippet = responseSnippet.Substring(0, 500);
        }

        // Log AI request using CQRS - Use CancellationToken.None to ensure logging completes even if request was cancelled
        var logCommand = new Api.BoundedContexts.Administration.Application.Commands.LogAiRequestCommand(
            UserId: userId,
            GameId: gameId,
            Endpoint: "setup-stream",
            Query: "setup_guide",
            ResponseSnippet: responseSnippet,
            LatencyMs: latencyMs,
            TokenCount: streamContext.TotalTokens,
            Confidence: streamContext.Confidence,
            Status: streamContext.StreamStatus,
            ErrorMessage: streamContext.StreamErrorMessage,
            IpAddress: context.Connection.RemoteIpAddress?.ToString(),
            UserAgent: context.Request.Headers.UserAgent.ToString(),
            PromptTokens: 0, // Not tracked in streaming
            CompletionTokens: streamContext.TotalTokens
        );
        await mediator.Send(logCommand, CancellationToken.None).ConfigureAwait(false);
    }
    private static async Task LogChessAgentRequestAsync(
        string userId,
        string query,
        ChessAgentResponse resp,
        int latencyMs,
        string ipAddress,
        string userAgent,
        IMediator mediator,
        CancellationToken ct)
    {
        string? model = null;
        string? finishReason = null;
        if (resp.metadata != null)
        {
            resp.metadata.TryGetValue("model", out model);
            resp.metadata.TryGetValue("finish_reason", out finishReason);
        }

        var logCommand = new Api.BoundedContexts.Administration.Application.Commands.LogAiRequestCommand(
            UserId: userId,
            GameId: "chess",
            Endpoint: "chess",
            Query: query,
            ResponseSnippet: resp.answer?.Length > 500 ? resp.answer.Substring(0, 500) : resp.answer,
            LatencyMs: latencyMs,
            TokenCount: resp.totalTokens,
            Confidence: resp.confidence,
            Status: "Success",
            ErrorMessage: null,
            IpAddress: ipAddress,
            UserAgent: userAgent,
            PromptTokens: resp.promptTokens,
            CompletionTokens: resp.completionTokens,
            Model: model,
            FinishReason: finishReason
        );
        await mediator.Send(logCommand, ct).ConfigureAwait(false);
    }

    private static async Task ExecuteExplainStreamingAsync(
        StreamExplainQuery query,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        await foreach (var evt in mediator.CreateStream(query, ct).ConfigureAwait(false))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(evt);
            await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
            await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
        }
    }
}
