using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Configuration;
using Api.Extensions;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
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
            IResponseQualityService qualityService, // AI-11: Quality scoring
            IMediator mediator, // CHAT-02: for GenerateFollowUpQuestionsQuery (Issue #1188) + AI logging
            IOptions<FollowUpQuestionsConfiguration> followUpConfig, // CHAT-02
            ILogger<Program> logger,
            bool bypassCache = false,
            bool generateFollowUps = true, // CHAT-02: opt-in parameter
            CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

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
                session.User.Id, req.gameId, req.query, bypassCache, generateFollowUps);

            // ISSUE-1194: Error handling now centralized in middleware + pipeline behavior
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
            // CHAT-02: Generate follow-up questions using CQRS query (Issue #1188)
            if (generateFollowUps)
            {
                var gameGuid = Guid.Parse(req.gameId);
                // Use CQRS Query to get game name
                var gameDto = await mediator.Send(new GetGameByIdQuery(gameGuid), ct);

                if (gameDto != null && !string.IsNullOrEmpty(gameDto.Title))
                {
                    followUpQuestions = await mediator.Send(new GenerateFollowUpQuestionsQuery
                    {
                        OriginalQuestion = req.query,
                        GeneratedAnswer = resp.answer,
                        RagContext = resp.snippets,
                        GameName = gameDto.Title,
                        MaxQuestions = 5
                    }, ct);

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
        })
        .WithName("QaAgent")
        .WithDescription("Ask a question about game rules using RAG (Retrieval-Augmented Generation)")
        .WithTags("AI Agents")
        .Produces<QaResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/agents/explain", async (ExplainRequest req, HttpContext context, IRagService rag, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (string.IsNullOrWhiteSpace(req.gameId))
            {
                return Results.BadRequest(new { error = "gameId is required" });
            }

            var startTime = DateTime.UtcNow;
            logger.LogInformation("Explain request from user {UserId} for game {GameId}: {Topic}",
                session.User.Id, req.gameId, req.topic);

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            // AI-09: Language parameter defaults to null (uses "en")
            var resp = await rag.ExplainAsync(req.gameId, req.topic, language: null, ct);
            var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            logger.LogInformation("Explain response delivered for game {GameId}, estimated {Minutes} min read",
                req.gameId, resp.estimatedReadingTimeMinutes);

            // ADM-01: Log AI request using CQRS
            var logCommand = new Api.BoundedContexts.Administration.Application.Commands.LogAiRequestCommand(
                UserId: session.User.Id,
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
            await mediator.Send(logCommand, ct);

            return Results.Json(resp);
        });

        // API-02: Streaming RAG Explain endpoint (SSE)
        // Migrated to CQRS: Uses StreamExplainQuery via MediatR (Issue #1186)
        group.MapPost("/agents/explain/stream", async (ExplainRequest req, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (string.IsNullOrWhiteSpace(req.gameId))
            {
                return Results.BadRequest(new { error = "gameId is required" });
            }

            logger.LogInformation("Streaming explain request from user {UserId} for game {GameId}: {Topic}",
                session.User.Id, req.gameId, req.topic);

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            // Set SSE headers
            context.Response.Headers["Content-Type"] = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";

            var query = new StreamExplainQuery(req.gameId, req.topic);
            await foreach (var evt in mediator.CreateStream(query, ct))
            {
                // Serialize event as JSON
                var json = System.Text.Json.JsonSerializer.Serialize(evt);

                // Write SSE format: "data: {json}\n\n"
                await context.Response.WriteAsync($"data: {json}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }

            logger.LogInformation("Streaming explain completed for game {GameId}, topic: {Topic}", req.gameId, req.topic);

            return Results.Empty;
        });

        // CHAT-01: Streaming QA endpoint (SSE)
        // Migrated to CQRS: Uses StreamQaQuery via MediatR (Issue #1186)
        group.MapPost("/agents/qa/stream", async (
            QaRequest req,
            HttpContext context,
            IMediator mediator,
            IOptions<FollowUpQuestionsConfiguration> followUpConfig, // CHAT-02
            IFeatureFlagService featureFlags, // CONFIG-05
            ILogger<Program> logger,
            bool generateFollowUps = true, // CHAT-02: opt-in parameter (Issue #1188)
            CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

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
                session.User.Id, req.gameId, req.query);

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            // Set SSE headers
            context.Response.Headers["Content-Type"] = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";

            var answerBuilder = new System.Text.StringBuilder();
            var totalTokens = 0;
            double? confidence = null;
            var snippets = new List<Snippet>();

            // CHAT-02: Follow-up question generation (fire-and-forget after Complete event)
            Task<IReadOnlyList<string>>? followUpTask = null;
            IReadOnlyList<string>? followUpQuestions = null;
            string? gameName = null;

            var query = new StreamQaQuery(req.gameId, req.query, req.chatId);
            await foreach (var evt in mediator.CreateStream(query, ct))
            {
                // Serialize event as JSON
                var json = System.Text.Json.JsonSerializer.Serialize(evt);

                // Write SSE format: "data: {json}\n\n"
                await context.Response.WriteAsync($"data: {json}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);

                // Track response data for logging and chat persistence
                // Issue #1186: Handlers now emit strongly-typed objects, not JsonElements
                if (evt.Type == StreamingEventType.Token && evt.Data is StreamingToken tokenData)
                {
                    answerBuilder.Append(tokenData.token);
                }
                else if (evt.Type == StreamingEventType.Citations && evt.Data is StreamingCitations citationsData)
                {
                    snippets = citationsData.citations.ToList();
                }
                else if (evt.Type == StreamingEventType.Complete && evt.Data is StreamingComplete completeData)
                {
                    totalTokens = completeData.totalTokens;
                    confidence = completeData.confidence;

                    // CHAT-02: Start follow-up generation in parallel (fire-and-forget)
                    if (generateFollowUps && followUpTask == null)
                    {
                        followUpTask = Task.Run(async () =>
                        {
                            // Use CQRS Query to fetch game name
                            var gameGuid = Guid.Parse(req.gameId);
                            var gameDto = await mediator.Send(new GetGameByIdQuery(gameGuid), ct);

                            if (gameDto == null || string.IsNullOrEmpty(gameDto.Title))
                            {
                                logger.LogWarning("Game {GameId} not found for follow-up generation", req.gameId);
                                return new List<string>().AsReadOnly();
                            }

                            gameName = gameDto.Title;
                            var answer = answerBuilder.ToString();
                            // CHAT-02: Use CQRS query for follow-up generation (Issue #1188)
                            return await mediator.Send(new GenerateFollowUpQuestionsQuery
                            {
                                OriginalQuestion = req.query,
                                GeneratedAnswer = answer,
                                RagContext = snippets,
                                GameName = gameName,
                                MaxQuestions = 5
                            }, ct);
                        });
                    }
                }
            }

            var answer = answerBuilder.ToString();
            var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            // CHAT-02: Wait for follow-up questions and send event
            if (followUpTask != null)
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

            logger.LogInformation("Streaming QA completed for game {GameId}, query: {Query}", req.gameId, req.query);

            // Log AI request using CQRS
            var logCommand = new Api.BoundedContexts.Administration.Application.Commands.LogAiRequestCommand(
                UserId: session.User.Id,
                GameId: req.gameId,
                Endpoint: "qa-stream",
                Query: req.query,
                ResponseSnippet: answer?.Length > 500 ? answer.Substring(0, 500) : answer,
                LatencyMs: latencyMs,
                TokenCount: totalTokens,
                Confidence: confidence,
                Status: "Success",
                ErrorMessage: null,
                IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString(),
                CompletionTokens: totalTokens
            );
            await mediator.Send(logCommand, ct);

            return Results.Empty;
        });

        // AI-03: RAG Setup Guide endpoint (Streaming)
        // Migrated to CQRS: Uses StreamSetupGuideQuery via MediatR with SSE streaming (Issue #1186)
        group.MapPost("/agents/setup", async (SetupGuideRequest req, HttpContext context, IMediator mediator, IFeatureFlagService featureFlags, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

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

            var startTime = DateTime.UtcNow;
            logger.LogInformation("Setup guide streaming request from user {UserId} for game {GameId}",
                session.User.Id, req.gameId);

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            // Set SSE headers for streaming
            context.Response.Headers["Content-Type"] = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";

            var steps = new List<SetupGuideStep>();
            string? gameTitle = null;
            int totalTokens = 0;
            double? confidence = null;
            int estimatedTime = 0;

            var query = new StreamSetupGuideQuery(req.gameId);
            await foreach (var evt in mediator.CreateStream(query, ct))
            {
                // Serialize event as JSON
                var json = System.Text.Json.JsonSerializer.Serialize(evt);

                // Write SSE format: "data: {json}\n\n"
                await context.Response.WriteAsync($"data: {json}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);

                // Track data for chat persistence and logging
                if (evt.Type == StreamingEventType.SetupStep && evt.Data is System.Text.Json.JsonElement stepElement)
                {
                    var stepData = System.Text.Json.JsonSerializer.Deserialize<StreamingSetupStep>(stepElement.GetRawText());
                    if (stepData?.step != null)
                    {
                        steps.Add(stepData.step);
                    }
                }
                else if (evt.Type == StreamingEventType.Complete && evt.Data is System.Text.Json.JsonElement completeElement)
                {
                    var completeData = System.Text.Json.JsonSerializer.Deserialize<StreamingComplete>(completeElement.GetRawText());
                    if (completeData != null)
                    {
                        estimatedTime = completeData.estimatedReadingTimeMinutes;
                        totalTokens = completeData.totalTokens;
                        confidence = completeData.confidence;
                    }
                }
            }

            var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            logger.LogInformation("Setup guide streaming completed for game {GameId}, {StepCount} steps, estimated {Minutes} min",
                req.gameId, steps.Count, estimatedTime);

            // ADM-01: Log AI request
            var responseSnippet = steps.Count > 0
                ? string.Join("; ", steps.Take(3).Select(s => s.instruction))
                : "No steps generated";
            if (responseSnippet.Length > 500)
            {
                responseSnippet = responseSnippet.Substring(0, 500);
            }

            // Log AI request using CQRS
            var logCommand = new Api.BoundedContexts.Administration.Application.Commands.LogAiRequestCommand(
                UserId: session.User.Id,
                GameId: req.gameId,
                Endpoint: "setup-stream",
                Query: "setup_guide",
                ResponseSnippet: responseSnippet,
                LatencyMs: latencyMs,
                TokenCount: totalTokens,
                Confidence: confidence,
                Status: "Success",
                ErrorMessage: null,
                IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString(),
                PromptTokens: 0, // Not tracked in streaming
                CompletionTokens: totalTokens
            );
            await mediator.Send(logCommand, ct);

            return Results.Empty;
        });

        // Migrated to CQRS: Uses ProvideAgentFeedbackCommand via MediatR (Issue #1188)
        group.MapPost("/agents/feedback", async (AgentFeedbackRequest req, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (!string.Equals(req.userId, session.User.Id, StringComparison.Ordinal))
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
                UserId = session.User.Id,
                Outcome = string.IsNullOrWhiteSpace(req.outcome) ? null : req.outcome,
                GameId = req.gameId
            }, ct);

            logger.LogInformation(
                "Recorded feedback {Outcome} for message {MessageId} on endpoint {Endpoint} by user {UserId}",
                req.outcome ?? "cleared",
                req.messageId,
                req.endpoint,
                session.User.Id);

            return Results.Json(new { ok = true });
        });

        // CHESS-04: Chess conversational agent endpoint
        // Migrated to CQRS: Uses InvokeChessAgentCommand via MediatR (Issue #1188)
        group.MapPost("/agents/chess", async (ChessAgentRequest req, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (string.IsNullOrWhiteSpace(req.question))
            {
                return Results.BadRequest(new { error = "question is required" });
            }

            var startTime = DateTime.UtcNow;
            logger.LogInformation("Chess agent request from user {UserId}: {Question}, FEN: {FEN}",
                session.User.Id, req.question, req.fenPosition ?? "none");

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            var resp = await mediator.Send(new InvokeChessAgentCommand
            {
                Question = req.question,
                FenPosition = req.fenPosition,
                ChatId = req.chatId
            }, ct);
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

            // ADM-01: Log AI request using CQRS
            var logCommand = new Api.BoundedContexts.Administration.Application.Commands.LogAiRequestCommand(
                UserId: session.User.Id,
                GameId: "chess",
                Endpoint: "chess",
                Query: req.question,
                ResponseSnippet: resp.answer?.Length > 500 ? resp.answer.Substring(0, 500) : resp.answer,
                LatencyMs: latencyMs,
                TokenCount: resp.totalTokens,
                Confidence: resp.confidence,
                Status: "Success",
                ErrorMessage: null,
                IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString(),
                PromptTokens: resp.promptTokens,
                CompletionTokens: resp.completionTokens,
                Model: model,
                FinishReason: finishReason
            );
            await mediator.Send(logCommand, ct);

            return Results.Json(resp);
        });

        group.MapGet("/bgg/search", async (
            HttpContext context,
            [FromQuery] string? q,
            [FromQuery] bool exact,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Authentication required
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Validate query parameter
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { error = "Query parameter 'q' is required" });
            }

            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            var query = new Api.BoundedContexts.GameManagement.Application.Queries.BggApi.SearchBggGamesQuery
            {
                Query = q,
                Exact = exact
            };
            var results = await mediator.Send(query, ct);
            logger.LogInformation("BGG search returned {Count} results for query: {Query}", results.Count, q);
            return Results.Json(new { results });
        });

        group.MapGet("/bgg/games/{bggId:int}", async (
            int bggId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Authentication required
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

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
            var details = await mediator.Send(query, ct);

            if (details == null)
            {
                logger.LogWarning("BGG game not found: {BggId}", bggId);
                return Results.NotFound(new { error = $"Game with BGG ID {bggId} not found" });
            }

            logger.LogInformation("BGG game details retrieved: {BggId}, {Name}", bggId, details.Name);
            return Results.Json(details);
        });


        // Migrated to CQRS: Uses IndexChessKnowledgeCommand via MediatR (Issue #1188)
        group.MapPost("/chess/index", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("User {UserId} with role {Role} attempted to index chess knowledge without permission",
                    session.User.Id, session.User.Role);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            logger.LogInformation("Admin {UserId} starting chess knowledge indexing", session.User.Id);

            var result = await mediator.Send(new IndexChessKnowledgeCommand(), ct);

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

        // Migrated to CQRS: Uses SearchChessKnowledgeQuery via MediatR (Issue #1188)
        group.MapGet("/chess/search", async (string? q, int? limit, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { error = "Query parameter 'q' is required" });
            }

            logger.LogInformation("User {UserId} searching chess knowledge: {Query}", session.User.Id, q);

            var searchResult = await mediator.Send(new SearchChessKnowledgeQuery
            {
                Query = q,
                Limit = limit ?? 5
            }, ct);

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

        // Migrated to CQRS: Uses DeleteChessKnowledgeCommand via MediatR (Issue #1188)
        group.MapDelete("/chess/index", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("User {UserId} with role {Role} attempted to delete chess knowledge without permission",
                    session.User.Id, session.User.Role);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            logger.LogInformation("Admin {UserId} deleting all chess knowledge", session.User.Id);

            var success = await mediator.Send(new DeleteChessKnowledgeCommand(), ct);

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
