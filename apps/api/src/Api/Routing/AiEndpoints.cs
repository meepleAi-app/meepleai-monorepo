using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Configuration;
using Api.Extensions;
using Api.Helpers;
using Api.Infrastructure.Entities;
using Api.Models;
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
    private static readonly string[] ParagraphSeparators = { "\n\n", "\n" };

    public static RouteGroupBuilder MapAiEndpoints(this RouteGroupBuilder group)
    {
        MapQaEndpoint(group);

        MapExplainEndpoint(group);

        MapExplainStreamEndpoint(group);

        MapQaStreamEndpoint(group);

        MapSetupGuideEndpoint(group);

        MapAgentFeedbackEndpoint(group);

        MapChessAgentEndpoint(group);

        MapPlayerModeSuggestionEndpoint(group);

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

    private static void MapPlayerModeSuggestionEndpoint(RouteGroupBuilder group)
    {
        // Issue #2421: Player Mode UI Controls - AI move suggestion endpoint
        // Uses SuggestPlayerMoveCommand via MediatR
        group.MapPost("/agents/player-mode/suggest", HandlePlayerModeSuggestion)
        .WithName("PlayerModeSuggestion")
        .WithDescription("Suggest optimal player move based on current game state using AI analysis")
        .WithTags("AI Agents")
        .Produces<PlayerModeSuggestionResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
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
        ILogger<Program> logger,
        bool generateFollowUps = true, // CHAT-02: opt-in parameter (Issue #1188)
        CancellationToken ct = default)
    {
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        // CONFIG-05: Check if streaming responses feature is enabled
        if (!await mediator.Send(new IsFeatureEnabledQuery("Features.StreamingResponses"), ct).ConfigureAwait(false))
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
#pragma warning disable S125 // Sections of code should not be commented out
        // STREAMING BOUNDARY: Streaming generator boundary - must handle all errors gracefully without throwing
        // All expected exceptions are caught above; this ensures cleanup and error event on unexpected errors
#pragma warning restore S125
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
#pragma warning disable S125 // Sections of code should not be commented out
            // CLEANUP PATTERN: Cleanup operation - must not throw during disposal/cleanup
            // Error event sending failure is logged but suppressed to ensure graceful stream termination
#pragma warning restore S125
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
        var resp = MapToLegacyQaResponse(qaResponse);

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
        var qualityScores = BuildQualityScores(qaResponse, resp.snippets.Count, resp.answer);

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

    private static async Task<IResult> HandleSetupGuide(
        SetupGuideRequest req,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        // CONFIG-05: Check if setup guide generation feature is enabled
        if (!await mediator.Send(new IsFeatureEnabledQuery("Features.SetupGuideGeneration"), ct).ConfigureAwait(false))
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
#pragma warning disable S125 // Sections of code should not be commented out
        // STREAMING BOUNDARY: Streaming generator boundary - must handle all errors gracefully without throwing
        // All expected exceptions are caught above; this ensures cleanup and error event on unexpected errors
#pragma warning restore S125
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
        // Issue #3352: Added comment support for detailed feedback
        await mediator.Send(new ProvideAgentFeedbackCommand
        {
            MessageId = req.messageId,
            Endpoint = req.endpoint,
            UserId = session.User!.Id.ToString(),
            Outcome = string.IsNullOrWhiteSpace(req.outcome) ? null : req.outcome,
            GameId = req.gameId,
            Comment = req.comment
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

    private static async Task<IResult> HandlePlayerModeSuggestion(
        PlayerModeSuggestionRequest req,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Session validated by RequireSessionFilter
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        var startTime = DateTime.UtcNow;
        logger.LogInformation(
            "Player mode suggestion request from user {UserId} for game {GameId}",
            session.User!.Id,
            req.gameId);

        // Send command via MediatR
        var resp = await mediator.Send(new SuggestPlayerMoveCommand
        {
            GameId = req.gameId,
            GameState = req.gameState,
            Query = req.query,
            ChatThreadId = req.chatThreadId
        }, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Player move suggestion delivered in {Latency}ms: confidence {Confidence}, alternatives {AlternativeCount}",
            (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
            resp.overallConfidence,
            resp.alternativeMoves?.Count ?? 0);

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
#pragma warning disable S125 // Sections of code should not be commented out
        // STREAMING BOUNDARY: Streaming generator boundary - must handle all errors gracefully without throwing
        // All expected exceptions are caught above; this ensures cleanup and error event on unexpected errors
#pragma warning restore S125
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
#pragma warning disable S125 // Sections of code should not be commented out
            // CLEANUP PATTERN: Cleanup operation - must not throw during disposal/cleanup
            // Error event sending failure is logged but suppressed to ensure graceful stream termination
#pragma warning restore S125
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
        if (!Guid.TryParse(gameId, out var gameGuid))
            return null;
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
    /// Map QaResponseDto to legacy QaResponse format for backward compatibility.
    /// </summary>
    private static QaResponse MapToLegacyQaResponse(QaResponseDto qaResponse)
    {
        var snippets = qaResponse.Sources.Select(src => new Snippet(
            text: src.TextContent,
            source: $"PDF:{src.VectorDocumentId}",
            page: src.PageNumber,
            line: 0,
            score: (float)src.RelevanceScore
        )).ToList();

        return new QaResponse(
            answer: qaResponse.Answer,
            snippets: snippets,
            confidence: qaResponse.OverallConfidence
        );
    }

    /// <summary>
    /// Build QualityScores from handler-calculated metrics.
    /// </summary>
    private static QualityScores BuildQualityScores(QaResponseDto qaResponse, int citationCount, string? answer)
    {
        var citationQuality = CalculateCitationQuality(citationCount, answer);

        return new QualityScores
        {
            RagConfidence = qaResponse.SearchConfidence,
            LlmConfidence = qaResponse.LlmConfidence,
            CitationQuality = citationQuality,
            OverallConfidence = qaResponse.OverallConfidence,
            IsLowQuality = qaResponse.IsLowQuality
        };
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
            .Split(ParagraphSeparators, StringSplitOptions.RemoveEmptyEntries)
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
    private static async Task<IReadOnlyList<string>> StartFollowUpGenerationAsync(
        string gameId,
        string query,
        string answer,
        List<Snippet> snippets,
        IMediator mediator,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            if (!Guid.TryParse(gameId, out var gameGuid))
                return new List<string>().AsReadOnly();
            var gameDto = await mediator.Send(new GetGameByIdQuery(gameGuid), ct).ConfigureAwait(false);

            if (gameDto == null || string.IsNullOrEmpty(gameDto.Title))
            {
                logger.LogWarning("Game {GameId} not found for follow-up generation", gameId);
                return new List<string>().AsReadOnly();
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

#pragma warning disable S1172 // Unused method parameters - CancellationToken intentionally ignored, using CancellationToken.None for logging
    private static async Task LogSetupGuideRequestAsync(
        string gameId,
        string userId,
        HttpContext context,
        IMediator mediator,
        SetupGuideStreamContext streamContext,
        int latencyMs,
        CancellationToken _)
#pragma warning restore S1172
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