using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Services.Chat;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Routing;

/// <summary>
/// AI and Agent endpoints.
/// Handles RAG QA, explain, setup guide, chess agent, BGG API, and chess knowledge indexing.
/// </summary>
public static class AiEndpoints
{
    public static RouteGroupBuilder MapAiEndpoints(this RouteGroupBuilder group)
    {
group.MapPost("/agents/qa", async (
    QaRequest req,
    HttpContext context,
    IRagService rag,
    ChatService chatService,
    AiRequestLogService aiLog,
    IResponseQualityService qualityService, // AI-11: Quality scoring
    IFollowUpQuestionService followUpService, // CHAT-02
    IOptions<FollowUpQuestionsConfiguration> followUpConfig, // CHAT-02
    MeepleAiDbContext dbContext, // CHAT-02: for game name lookup
    ILogger<Program> logger,
    bool bypassCache = false,
    bool generateFollowUps = true, // CHAT-02: opt-in parameter
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    // TEST-650: Validate query parameter to prevent empty/null queries
    if (string.IsNullOrWhiteSpace(req.query))
    {
        return Results.BadRequest(new { error = "query is required" });
    }

    // TEST-650: Validate game existence
    var gameExists = await dbContext.Games.AnyAsync(g => g.Id == req.gameId, ct);
    if (!gameExists)
    {
        return Results.NotFound(new { error = "Game not found", gameId = req.gameId });
    }

    var startTime = DateTime.UtcNow;
    var config = followUpConfig.Value;
    generateFollowUps = generateFollowUps && config.Enabled; // Apply global feature flag

    logger.LogInformation("QA request from user {UserId} for game {GameId}: {Query} (bypassCache: {BypassCache}, generateFollowUps: {GenerateFollowUps})",
        session.User.Id, req.gameId, req.query, bypassCache, generateFollowUps);

    try
    {
        // Persist user query to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "user",
                req.query,
                new { endpoint = "qa", gameId = req.gameId, bypassCache, generateFollowUps },
                ct);
        }

        // AI-14: Use hybrid search with configurable search mode (default: Hybrid)
        // PERF-03: Support cache bypass via query parameter
        // AI-09: Language parameter defaults to null (uses "en")
        var resp = await rag.AskWithHybridSearchAsync(
            req.gameId,
            req.query,
            searchMode: req.searchMode,
            language: null,
            bypassCache,
            ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // CHAT-02: Generate follow-up questions if enabled
        IReadOnlyList<string>? followUpQuestions = null;
        if (generateFollowUps)
        {
            var game = await dbContext.Games
                .Where(g => g.Id == req.gameId)
                .Select(g => g.Name)
                .FirstOrDefaultAsync(ct);

            if (game != null)
            {
                followUpQuestions = await followUpService.GenerateQuestionsAsync(
                    originalQuestion: req.query,
                    generatedAnswer: resp.answer,
                    ragContext: resp.snippets,
                    gameName: game,
                    ct: ct);

                logger.LogInformation("Generated {Count} follow-up questions for game {GameId}",
                    followUpQuestions.Count, req.gameId);
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

        // AI-11: Calculate quality scores from response data using actual RAG scores
        var ragSearchResults = resp.snippets.Select(s => new RagSearchResult { Score = s.score }).ToList();
        var citations = resp.snippets.Select(s => new Citation
        {
            DocumentId = Guid.NewGuid(), // Placeholder - snippets don't have document IDs
            PageNumber = s.page,
            SnippetText = s.text
        }).ToList();
        var qualityScores = qualityService.CalculateQualityScores(
            ragSearchResults,
            citations,
            resp.answer,
            null); // Let service calculate LLM confidence from response text (resp.confidence is RAG max, not LLM confidence)

        // AI-11: Debug logging for quality scores
        logger.LogInformation(
            "Quality scores calculated - RAG: {RagConf:F3}, LLM: {LlmConf:F3}, Citation: {CitConf:F3}, Overall: {OverallConf:F3}, IsLowQuality: {IsLowQuality}, SnippetScores: [{Scores}]",
            qualityScores.RagConfidence,
            qualityScores.LlmConfidence,
            qualityScores.CitationQuality,
            qualityScores.OverallConfidence,
            qualityScores.IsLowQuality,
            string.Join(", ", ragSearchResults.Select(r => r.Score.ToString("F3"))));

        string? model = null;
        string? finishReason = null;
        if (resp.metadata != null)
        {
            if (resp.metadata.TryGetValue("model", out var metadataModel))
            {
                model = metadataModel;
            }

            if (resp.metadata.TryGetValue("finish_reason", out var metadataFinish))
            {
                finishReason = metadataFinish;
            }
        }

        // Persist agent response to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "assistant",
                resp.answer,
                new
                {
                    endpoint = "qa",
                    gameId = req.gameId,
                    promptTokens = resp.promptTokens,
                    completionTokens = resp.completionTokens,
                    totalTokens = resp.totalTokens,
                    confidence = resp.confidence,
                    model,
                    finishReason,
                    snippetCount = resp.snippets.Count,
                    followUpQuestionsCount = followUpQuestions?.Count ?? 0 // CHAT-02
                },
                ct);
        }

        // ADM-01: Log AI request with AI-11 quality scores
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "qa",
            req.query,
            resp.answer?.Length > 500 ? resp.answer.Substring(0, 500) : resp.answer,
            latencyMs,
            resp.totalTokens,
            resp.confidence,
            "Success",
            null,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            promptTokens: resp.promptTokens,
            completionTokens: resp.completionTokens,
            model: model,
            finishReason: finishReason,
            qualityScores: qualityScores, // AI-11: Include quality scores
            ct: ct);

        return Results.Json(finalResponse); // CHAT-02: Return response with follow-up questions
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (RagService, LlmService, etc.)
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Persist error to chat if chatId provided
        if (req.chatId.HasValue)
        {
            try
            {
                await chatService.AddMessageAsync(
                    req.chatId.Value,
                    session.User.Id,
                    "error",
                    $"Failed to process QA request: {ex.Message}",
                    new { endpoint = "qa", gameId = req.gameId, error = ex.GetType().Name },
                    ct);
            }
            catch (Exception chatEx)
            {
                // Resilience pattern: Chat logging failure shouldn't break error response flow
                // Fail-open to ensure client receives error message even if chat persistence fails
                logger.LogWarning(chatEx, "Failed to log error message to chat {ChatId}", req.chatId.Value);
            }
        }

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "qa",
            req.query,
            null,
            latencyMs,
            status: "Error",
            errorMessage: ex.Message,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString(),
            ct: ct);

        throw;
    }
})
.WithName("QaAgent")
.WithDescription("Ask a question about game rules using RAG (Retrieval-Augmented Generation)")
.WithTags("AI Agents")
.Produces<QaResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status500InternalServerError);

group.MapPost("/agents/explain", async (ExplainRequest req, HttpContext context, IRagService rag, ChatService chatService, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    // TEST-650: Validate query parameter to prevent empty/null queries
    if (string.IsNullOrWhiteSpace(req.query))
    {
        return Results.BadRequest(new { error = "query is required" });
    }

    // TEST-650: Validate game existence
    var gameExists = await dbContext.Games.AnyAsync(g => g.Id == req.gameId, ct);
    if (!gameExists)
    {
        return Results.NotFound(new { error = "Game not found", gameId = req.gameId });
    }

    var startTime = DateTime.UtcNow;
    logger.LogInformation("Explain request from user {UserId} for game {GameId}: {Topic}",
        session.User.Id, req.gameId, req.topic);

    try
    {
        // Persist user query to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "user",
                $"Explain: {req.topic}",
                new { endpoint = "explain", gameId = req.gameId, topic = req.topic },
                ct);
        }

        // AI-09: Language parameter defaults to null (uses "en")
        var resp = await rag.ExplainAsync(req.gameId, req.topic, language: null, ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("Explain response delivered for game {GameId}, estimated {Minutes} min read",
            req.gameId, resp.estimatedReadingTimeMinutes);

        // Persist agent response to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "assistant",
                resp.script,
                new
                {
                    endpoint = "explain",
                    gameId = req.gameId,
                    topic = req.topic,
                    promptTokens = resp.promptTokens,
                    completionTokens = resp.completionTokens,
                    totalTokens = resp.totalTokens,
                    confidence = resp.confidence,
                    estimatedReadingTimeMinutes = resp.estimatedReadingTimeMinutes,
                    outline = resp.outline,
                    citationCount = resp.citations.Count
                },
                ct);
        }

        // ADM-01: Log AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "explain",
            req.topic,
            resp.script?.Length > 500 ? resp.script.Substring(0, 500) : resp.script,
            latencyMs,
            resp.totalTokens,
            resp.confidence,
            "Success",
            null,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            promptTokens: resp.promptTokens,
            completionTokens: resp.completionTokens,
            model: null,
            finishReason: null,
            ct: ct);

        return Results.Json(resp);
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (RagService, LlmService, etc.)
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Persist error to chat if chatId provided
        if (req.chatId.HasValue)
        {
            try
            {
                await chatService.AddMessageAsync(
                    req.chatId.Value,
                    session.User.Id,
                    "error",
                    $"Failed to process Explain request: {ex.Message}",
                    new { endpoint = "explain", gameId = req.gameId, topic = req.topic, error = ex.GetType().Name },
                    ct);
            }
            catch (Exception chatEx)
            {
                // Resilience pattern: Chat logging failure shouldn't break error response flow
                // Fail-open to ensure client receives error message even if chat persistence fails
                logger.LogWarning(chatEx, "Failed to log error message to chat {ChatId}", req.chatId.Value);
            }
        }

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "explain",
            req.topic,
            null,
            latencyMs,
            status: "Error",
            errorMessage: ex.Message,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString(),
            ct: ct);

        throw;
    }
});

// API-02: Streaming RAG Explain endpoint (SSE)
group.MapPost("/agents/explain/stream", async (ExplainRequest req, HttpContext context, IStreamingRagService streamingRag, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    logger.LogInformation("Streaming explain request from user {UserId} for game {GameId}: {Topic}",
        session.User.Id, req.gameId, req.topic);

    // Set SSE headers
    context.Response.Headers["Content-Type"] = "text/event-stream";
    context.Response.Headers["Cache-Control"] = "no-cache";
    context.Response.Headers["Connection"] = "keep-alive";

    try
    {
        await foreach (var evt in streamingRag.ExplainStreamAsync(req.gameId, req.topic, ct))
        {
            // Serialize event as JSON
            var json = System.Text.Json.JsonSerializer.Serialize(evt);

            // Write SSE format: "data: {json}\n\n"
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }

        logger.LogInformation("Streaming explain completed for game {GameId}, topic: {Topic}", req.gameId, req.topic);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Streaming explain cancelled by client for game {GameId}, topic: {Topic}", req.gameId, req.topic);
    }
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
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }
        catch
        {
            // If we can't send error event, client connection is likely broken
        }
    }

    return Results.Empty;
});

// CHAT-01: Streaming QA endpoint (SSE)
group.MapPost("/agents/qa/stream", async (
    QaRequest req,
    HttpContext context,
    IStreamingQaService streamingQa,
    ChatService chatService,
    AiRequestLogService aiLog,
    IFollowUpQuestionService followUpService, // CHAT-02
    IOptions<FollowUpQuestionsConfiguration> followUpConfig, // CHAT-02
    MeepleAiDbContext dbContext, // CHAT-02
    IFeatureFlagService featureFlags, // CONFIG-05
    ILogger<Program> logger,
    bool generateFollowUps = true, // CHAT-02: opt-in parameter
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // CONFIG-05: Check if streaming responses feature is enabled
    if (!await featureFlags.IsEnabledAsync("Features.StreamingResponses"))
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
    generateFollowUps = generateFollowUps && config.Enabled;

    var startTime = DateTime.UtcNow;
    logger.LogInformation("Streaming QA request from user {UserId} for game {GameId}: {Query}",
        session.User.Id, req.gameId, req.query);

    // Set SSE headers
    context.Response.Headers["Content-Type"] = "text/event-stream";
    context.Response.Headers["Cache-Control"] = "no-cache";
    context.Response.Headers["Connection"] = "keep-alive";

    try
    {
        // Persist user query to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "user",
                req.query,
                new { endpoint = "qa-stream", gameId = req.gameId },
                ct);
        }

        var answerBuilder = new System.Text.StringBuilder();
        var totalTokens = 0;
        double? confidence = null;
        var snippets = new List<Snippet>();

        // CHAT-02: Follow-up question generation (fire-and-forget after Complete event)
        Task<IReadOnlyList<string>>? followUpTask = null;
        IReadOnlyList<string>? followUpQuestions = null;
        string? gameName = null;

        await foreach (var evt in streamingQa.AskStreamAsync(req.gameId, req.query, req.chatId, ct))
        {
            // Serialize event as JSON
            var json = System.Text.Json.JsonSerializer.Serialize(evt);

            // Write SSE format: "data: {json}\n\n"
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);

            // Track response data for logging and chat persistence
            if (evt.Type == StreamingEventType.Token && evt.Data is System.Text.Json.JsonElement tokenElement)
            {
                var tokenData = System.Text.Json.JsonSerializer.Deserialize<StreamingToken>(tokenElement.GetRawText());
                if (tokenData != null)
                {
                    answerBuilder.Append(tokenData.token);
                }
            }
            else if (evt.Type == StreamingEventType.Citations && evt.Data is System.Text.Json.JsonElement citationsElement)
            {
                var citationsData = System.Text.Json.JsonSerializer.Deserialize<StreamingCitations>(citationsElement.GetRawText());
                if (citationsData != null)
                {
                    snippets = citationsData.citations.ToList();
                }
            }
            else if (evt.Type == StreamingEventType.Complete && evt.Data is System.Text.Json.JsonElement completeElement)
            {
                var completeData = System.Text.Json.JsonSerializer.Deserialize<StreamingComplete>(completeElement.GetRawText());
                if (completeData != null)
                {
                    totalTokens = completeData.totalTokens;
                    confidence = completeData.confidence;
                }

                // CHAT-02: Start follow-up generation in parallel (fire-and-forget)
                if (generateFollowUps && followUpTask == null)
                {
                    followUpTask = Task.Run(async () =>
                    {
                        try
                        {
                            // Fetch game name
                            gameName = await dbContext.Games
                                .Where(g => g.Id.ToString() == req.gameId)
                                .Select(g => g.Name)
                                .FirstOrDefaultAsync(ct);

                            if (gameName == null)
                            {
                                logger.LogWarning("Game {GameId} not found for follow-up generation", req.gameId);
                                return new List<string>().AsReadOnly();
                            }

                            var answer = answerBuilder.ToString();
                            return await followUpService.GenerateQuestionsAsync(
                                originalQuestion: req.query,
                                generatedAnswer: answer,
                                ragContext: snippets,
                                gameName: gameName,
                                ct: ct);
                        }
                        catch (Exception ex)
                        {
                            // Resilience pattern: Follow-up question generation failure shouldn't break main QA response
                            // Fail-open to ensure user gets answer even if follow-up suggestions unavailable
                            logger.LogWarning(ex, "Failed to generate follow-up questions for game {GameId}", req.gameId);
                            return new List<string>().AsReadOnly();
                        }
                    });
                }
            }
        }

        var answer = answerBuilder.ToString();
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // CHAT-02: Wait for follow-up questions and send event
        if (followUpTask != null)
        {
            try
            {
                followUpQuestions = await followUpTask;
                if (followUpQuestions != null && followUpQuestions.Count > 0)
                {
                    var followUpEvent = new RagStreamingEvent(
                        StreamingEventType.FollowUpQuestions,
                        new StreamingFollowUpQuestions(followUpQuestions),
                        DateTime.UtcNow);
                    var followUpJson = System.Text.Json.JsonSerializer.Serialize(followUpEvent);
                    await context.Response.WriteAsync($"data: {followUpJson}\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);

                    logger.LogInformation("Sent {Count} follow-up questions for game {GameId}",
                        followUpQuestions.Count, req.gameId);
                }
            }
            catch (Exception ex)
            {
                // Resilience pattern: SSE event sending failure for follow-up questions shouldn't break response
                // Fail-open to ensure client receives main answer even if follow-up streaming fails
                logger.LogWarning(ex, "Failed to send follow-up questions event for game {GameId}", req.gameId);
            }
        }

        logger.LogInformation("Streaming QA completed for game {GameId}, query: {Query}", req.gameId, req.query);

        // Persist agent response to chat if chatId provided
        if (req.chatId.HasValue && !string.IsNullOrWhiteSpace(answer))
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "assistant",
                answer,
                new
                {
                    endpoint = "qa-stream",
                    gameId = req.gameId,
                    totalTokens,
                    confidence,
                    snippetCount = snippets.Count,
                    followUpQuestionsCount = followUpQuestions?.Count ?? 0 // CHAT-02
                },
                ct);
        }

        // Log AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "qa-stream",
            req.query,
            answer?.Length > 500 ? answer.Substring(0, 500) : answer,
            latencyMs,
            totalTokens,
            confidence,
            "Success",
            null,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            completionTokens: totalTokens,
            ct: ct);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Streaming QA cancelled by client for game {GameId}, query: {Query}", req.gameId, req.query);
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions for SSE streaming endpoint
        // Sends error event to client stream, specific exception handling in service layer
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        logger.LogError(ex, "Error during streaming QA for game {GameId}, query: {Query}", req.gameId, req.query);

        // Persist error to chat if chatId provided
        if (req.chatId.HasValue)
        {
            try
            {
                await chatService.AddMessageAsync(
                    req.chatId.Value,
                    session.User.Id,
                    "error",
                    $"Failed to process streaming QA request: {ex.Message}",
                    new { endpoint = "qa-stream", gameId = req.gameId, error = ex.GetType().Name },
                    ct);
            }
            catch (Exception chatEx)
            {
                // Resilience pattern: Chat logging failure shouldn't break error response flow
                // Fail-open to ensure client receives error message even if chat persistence fails
                logger.LogWarning(chatEx, "Failed to log error message to chat {ChatId}", req.chatId.Value);
            }
        }

        // Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "qa-stream",
            req.query,
            null,
            latencyMs,
            status: "Error",
            errorMessage: ex.Message,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString(),
            ct: ct);

        // Send error event if possible
        try
        {
            var errorEvent = new RagStreamingEvent(
                StreamingEventType.Error,
                new StreamingError($"An error occurred: {ex.Message}", "INTERNAL_ERROR"),
                DateTime.UtcNow);
            var json = System.Text.Json.JsonSerializer.Serialize(errorEvent);
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }
        catch
        {
            // If we can't send error event, client connection is likely broken
        }
    }

    return Results.Empty;
});

// AI-03: RAG Setup Guide endpoint
group.MapPost("/agents/setup", async (SetupGuideRequest req, HttpContext context, SetupGuideService setupGuide, ChatService chatService, AiRequestLogService aiLog, IFeatureFlagService featureFlags, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // CONFIG-05: Check if setup guide generation feature is enabled
    if (!await featureFlags.IsEnabledAsync("Features.SetupGuideGeneration"))
    {
        return Results.Json(
            new { error = "feature_disabled", message = "Setup guide generation is currently unavailable", featureName = "Features.SetupGuideGeneration" },
            statusCode: 403);
    }

    if (string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    // TEST-650: Validate query parameter to prevent empty/null queries
    if (string.IsNullOrWhiteSpace(req.query))
    {
        return Results.BadRequest(new { error = "query is required" });
    }

    // TEST-650: Validate game existence
    var gameExists = await dbContext.Games.AnyAsync(g => g.Id == req.gameId, ct);
    if (!gameExists)
    {
        return Results.NotFound(new { error = "Game not found", gameId = req.gameId });
    }

    var startTime = DateTime.UtcNow;
    logger.LogInformation("Setup guide request from user {UserId} for game {GameId}",
        session.User.Id, req.gameId);

    try
    {
        // Persist user query to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "user",
                "Generate setup guide",
                new { endpoint = "setup", gameId = req.gameId },
                ct);
        }

        var resp = await setupGuide.GenerateSetupGuideAsync(req.gameId, ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("Setup guide delivered for game {GameId}, {StepCount} steps, estimated {Minutes} min",
            req.gameId, resp.steps.Count, resp.estimatedSetupTimeMinutes);

        // Persist agent response to chat if chatId provided
        if (req.chatId.HasValue)
        {
            var setupSummary = resp.steps.Count > 0
                ? string.Join("; ", resp.steps.Take(3).Select(s => $"{s.stepNumber}. {s.title}")) + (resp.steps.Count > 3 ? "..." : "")
                : "No steps generated";

            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "assistant",
                $"Setup guide for {resp.gameTitle}: {setupSummary}",
                new
                {
                    endpoint = "setup",
                    gameId = req.gameId,
                    gameTitle = resp.gameTitle,
                    promptTokens = resp.promptTokens,
                    completionTokens = resp.completionTokens,
                    totalTokens = resp.totalTokens,
                    confidence = resp.confidence,
                    estimatedSetupTimeMinutes = resp.estimatedSetupTimeMinutes,
                    stepCount = resp.steps.Count,
                    steps = resp.steps
                },
                ct);
        }

        // ADM-01: Log AI request
        var responseSnippet = resp.steps.Count > 0
            ? string.Join("; ", resp.steps.Take(3).Select(s => s.instruction))
            : "No steps generated";
        if (responseSnippet.Length > 500)
        {
            responseSnippet = responseSnippet.Substring(0, 500);
        }

        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "setup",
            "setup_guide",
            responseSnippet,
            latencyMs,
            resp.totalTokens,
            resp.confidence,
            "Success",
            null,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            promptTokens: resp.promptTokens,
            completionTokens: resp.completionTokens,
            model: null,
            finishReason: null,
            ct: ct);

        return Results.Json(resp);
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (RagService, LlmService, etc.)
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Persist error to chat if chatId provided
        if (req.chatId.HasValue)
        {
            try
            {
                await chatService.AddMessageAsync(
                    req.chatId.Value,
                    session.User.Id,
                    "error",
                    $"Failed to generate setup guide: {ex.Message}",
                    new { endpoint = "setup", gameId = req.gameId, error = ex.GetType().Name },
                    ct);
            }
            catch (Exception chatEx)
            {
                // Resilience pattern: Chat logging failure shouldn't break error response flow
                // Fail-open to ensure client receives error message even if chat persistence fails
                logger.LogWarning(chatEx, "Failed to log error message to chat {ChatId}", req.chatId.Value);
            }
        }

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "setup",
            "setup_guide",
            null,
            latencyMs,
            status: "Error",
            errorMessage: ex.Message,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString(),
            ct: ct);

        throw;
    }
});

group.MapPost("/agents/feedback", async (AgentFeedbackRequest req, HttpContext context, AgentFeedbackService feedbackService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(req.userId, session.User.Id, StringComparison.Ordinal))
    {
        return Results.BadRequest(new { error = "Invalid user" });
    }

    if (string.IsNullOrWhiteSpace(req.messageId) || string.IsNullOrWhiteSpace(req.endpoint))
    {
        return Results.BadRequest(new { error = "messageId and endpoint are required" });
    }

    try
    {
        await feedbackService.RecordFeedbackAsync(
            req.messageId,
            req.endpoint,
            session.User.Id,
            string.IsNullOrWhiteSpace(req.outcome) ? null : req.outcome,
            req.gameId,
            ct);

        logger.LogInformation(
            "Recorded feedback {Outcome} for message {MessageId} on endpoint {Endpoint} by user {UserId}",
            req.outcome ?? "cleared",
            req.messageId,
            req.endpoint,
            session.User.Id);

        return Results.Json(new { ok = true });
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (AgentFeedbackService)
        logger.LogError(ex, "Failed to record feedback for message {MessageId}", req.messageId);
        return Results.Problem(detail: "Unable to record feedback", statusCode: 500);
    }
});

// CHESS-04: Chess conversational agent endpoint
group.MapPost("/agents/chess", async (ChessAgentRequest req, HttpContext context, IChessAgentService chessAgent, ChatService chatService, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.question))
    {
        return Results.BadRequest(new { error = "question is required" });
    }

    var startTime = DateTime.UtcNow;
    logger.LogInformation("Chess agent request from user {UserId}: {Question}, FEN: {FEN}",
        session.User.Id, req.question, req.fenPosition ?? "none");

    try
    {
        // Persist user query to chat if chatId provided
        if (req.chatId.HasValue)
        {
            var queryText = !string.IsNullOrWhiteSpace(req.fenPosition)
                ? $"{req.question} [Position: {req.fenPosition}]"
                : req.question;

            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "user",
                queryText,
                new { endpoint = "chess", question = req.question, fenPosition = req.fenPosition },
                ct);
        }

        var resp = await chessAgent.AskAsync(req, ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("Chess agent response delivered: {MoveCount} moves suggested",
            resp.suggestedMoves.Count);

        string? model = null;
        string? finishReason = null;
        if (resp.metadata != null)
        {
            if (resp.metadata.TryGetValue("model", out var metadataModel))
            {
                model = metadataModel;
            }

            if (resp.metadata.TryGetValue("finish_reason", out var metadataFinish))
            {
                finishReason = metadataFinish;
            }
        }

        // Persist agent response to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "assistant",
                resp.answer,
                new
                {
                    endpoint = "chess",
                    question = req.question,
                    fenPosition = req.fenPosition,
                    promptTokens = resp.promptTokens,
                    completionTokens = resp.completionTokens,
                    totalTokens = resp.totalTokens,
                    confidence = resp.confidence,
                    model,
                    finishReason,
                    sourceCount = resp.sources.Count,
                    suggestedMoves = resp.suggestedMoves,
                    analysis = resp.analysis
                },
                ct);
        }

        // ADM-01: Log AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            "chess",
            "chess",
            req.question,
            resp.answer?.Length > 500 ? resp.answer.Substring(0, 500) : resp.answer,
            latencyMs,
            resp.totalTokens,
            resp.confidence,
            "Success",
            null,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            promptTokens: resp.promptTokens,
            completionTokens: resp.completionTokens,
            model: model,
            finishReason: finishReason,
            ct: ct);

        return Results.Json(resp);
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (RagService, LlmService, etc.)
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Persist error to chat if chatId provided
        if (req.chatId.HasValue)
        {
            try
            {
                await chatService.AddMessageAsync(
                    req.chatId.Value,
                    session.User.Id,
                    "error",
                    $"Failed to process chess question: {ex.Message}",
                    new { endpoint = "chess", question = req.question, fenPosition = req.fenPosition, error = ex.GetType().Name },
                    ct);
            }
            catch (Exception chatEx)
            {
                // Resilience pattern: Chat logging failure shouldn't break error response flow
                // Fail-open to ensure client receives error message even if chat persistence fails
                logger.LogWarning(chatEx, "Failed to log error message to chat {ChatId}", req.chatId.Value);
            }
        }

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            "chess",
            "chess",
            req.question,
            null,
            latencyMs,
            status: "Error",
            errorMessage: ex.Message,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString(),
            ct: ct);

        throw;
    }
});

group.MapGet("/bgg/search", async (
    HttpContext context,
    [FromQuery] string? q,
    [FromQuery] bool exact,
    IBggApiService bggService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    // Authentication required
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // Validate query parameter
    if (string.IsNullOrWhiteSpace(q))
    {
        return Results.BadRequest(new { error = "Query parameter 'q' is required" });
    }

    try
    {
        var results = await bggService.SearchGamesAsync(q, exact, ct);
        logger.LogInformation("BGG search returned {Count} results for query: {Query}", results.Count, q);
        return Results.Json(new { results });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogError(ex, "BGG API unavailable for search query: {Query}", q);
        return Results.Json(new
        {
            error = "BoardGameGeek API is currently unavailable. Please try again later.",
            details = ex.Message
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (BggApiService)
        logger.LogError(ex, "Unexpected error during BGG search: {Query}", q);
        return Results.Json(new
        {
            error = "An unexpected error occurred while searching BoardGameGeek."
        }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

group.MapGet("/bgg/games/{bggId:int}", async (
    int bggId,
    HttpContext context,
    IBggApiService bggService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    // Authentication required
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // Validate BGG ID
    if (bggId <= 0)
    {
        return Results.BadRequest(new { error = "Invalid BGG ID. Must be a positive integer." });
    }

    try
    {
        var details = await bggService.GetGameDetailsAsync(bggId, ct);

        if (details == null)
        {
            logger.LogWarning("BGG game not found: {BggId}", bggId);
            return Results.NotFound(new { error = $"Game with BGG ID {bggId} not found" });
        }

        logger.LogInformation("BGG game details retrieved: {BggId}, {Name}", bggId, details.Name);
        return Results.Json(details);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogError(ex, "BGG API unavailable for game ID: {BggId}", bggId);
        return Results.Json(new
        {
            error = "BoardGameGeek API is currently unavailable. Please try again later.",
            details = ex.Message
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (BggApiService)
        logger.LogError(ex, "Unexpected error retrieving BGG game details: {BggId}", bggId);
        return Results.Json(new
        {
            error = "An unexpected error occurred while retrieving game details."
        }, statusCode: StatusCodes.Status500InternalServerError);
    }
});


group.MapPost("/chess/index", async (HttpContext context, IChessKnowledgeService chessService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to index chess knowledge without permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {UserId} starting chess knowledge indexing", session.User.Id);

    var result = await chessService.IndexChessKnowledgeAsync(ct);

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
});

group.MapGet("/chess/search", async (string? q, int? limit, HttpContext context, IChessKnowledgeService chessService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(q))
    {
        return Results.BadRequest(new { error = "Query parameter 'q' is required" });
    }

    logger.LogInformation("User {UserId} searching chess knowledge: {Query}", session.User.Id, q);

    var searchResult = await chessService.SearchChessKnowledgeAsync(q, limit ?? 5, ct);

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
});

group.MapDelete("/chess/index", async (HttpContext context, IChessKnowledgeService chessService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to delete chess knowledge without permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {UserId} deleting all chess knowledge", session.User.Id);

    var success = await chessService.DeleteChessKnowledgeAsync(ct);

    if (!success)
    {
        logger.LogError("Chess knowledge deletion failed");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    logger.LogInformation("Chess knowledge deletion completed successfully");
    return Results.Json(new { success = true });
});

// UI-01: Chat management endpoints
        return group;
    }
}
