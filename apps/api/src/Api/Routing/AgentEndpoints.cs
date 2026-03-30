using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Api.Extensions;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Serialization;
using Api.Middleware;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using KbAgentDto = Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDto;
using Api.Models;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Routing;

/// <summary>
/// DDD-PHASE3: Agent management endpoints for KnowledgeBase bounded context.
/// Issue 866: AI Agents Entity and Configuration.
/// </summary>
internal static class AgentEndpoints
{
    private static readonly string[] AvailableAgentNames = { "TutorAgent", "ArbitroAgent", "DecisoreAgent" };

    public static RouteGroupBuilder MapAgentEndpoints(this RouteGroupBuilder group)
    {
        MapCreateAgentEndpoint(group);
        MapGetAgentByIdEndpoint(group);
        MapGetAllAgentsEndpoint(group);
        MapGetAgentStatusEndpoint(group); // Agent chat readiness validation
        MapConfigureAgentEndpoint(group);
        MapInvokeAgentEndpoint(group);
        MapUpdateAgentDocumentsEndpoint(group);
        MapGetAgentDocumentsEndpoint(group);
        MapAskAgentQuestionEndpoint(group); // POC: Agent search strategy testing
        MapTutorQueryEndpoint(group); // ISSUE-3499
        MapChatWithAgentEndpoint(group); // Issue #4126: SSE chat streaming
        MapGetRecentAgentsEndpoint(group); // Issue #4126: Dashboard widget
        MapUnifiedAgentQueryEndpoint(group); // Issue #4338: Unified API Gateway
        MapRoutingMetricsEndpoint(group); // Issue #4338: Routing metrics observability

        // Issue #4683: User-facing agent CRUD
        MapCreateUserAgentEndpoint(group);
        MapUpdateUserAgentEndpoint(group);
        MapDeleteUserAgentEndpoint(group);

        // Issue #4771: Agent slots quota
        MapGetUserAgentSlotsEndpoint(group);

        // Issue #4772: Orchestrated agent creation
        MapCreateAgentWithSetupEndpoint(group);

        // Agent LLM configuration (user-facing)
        MapGetAgentConfigurationEndpoint(group);
        MapUpdateAgentConfigurationEndpoint(group);

        // Issue #5585: Arbiter Mode — dispute arbitration
        MapAskArbiterEndpoint(group);

        // Ownership/RAG access: Quick agent creation
        MapQuickCreateAgentEndpoint(group);

        return group;
    }

    private static void MapCreateAgentEndpoint(RouteGroupBuilder group)
    {
        // Create agent
        group.MapPost("/agents", async (
            CreateAgentRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated AND Admin role checked by RequireAdminSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new CreateAgentCommand(
                Name: req.Name,
                AgentType: req.Type,
                StrategyName: req.StrategyName,
                StrategyParameters: req.StrategyParameters ?? new Dictionary<string, object>(StringComparer.Ordinal),
                IsActive: req.IsActive ?? true
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Created agent {AgentId} by user {UserId}",
                result.Id, session!.User!.Id);

            return Results.Created($"/api/v1/agents/{result.Id}", result);
        })
        .RequireAdminSession() // Issue #1446: Automatic admin session validation
        .WithName("CreateAgent")
        .Produces(201)
        .Produces(400)
        .Produces(500);
    }

    private static void MapGetAgentByIdEndpoint(RouteGroupBuilder group)
    {
        // Get agent by ID
        group.MapGet("/agents/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            _ = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new GetAgentByIdQuery(id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result == null)
            {
                return Results.NotFound(new { error = $"Agent {id} not found" });
            }

            return Results.Ok(result);
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .WithName("GetAgentById")
        .Produces(200)
        .Produces(404)
        .Produces(500);
    }

    private static void MapGetAllAgentsEndpoint(RouteGroupBuilder group)
    {
        // Get all agents
        group.MapGet("/agents", async (
            [FromQuery] bool? activeOnly,
            [FromQuery] string? type,
            [FromQuery] Guid? gameId,
            [FromQuery] bool? userOwned,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            // Issue #4914: when userOwned=true, filter by game + current user
            var ownedByUserId = userOwned.GetValueOrDefault(false) ? session!.User!.Id : (Guid?)null;
            var query = new GetAllAgentsQuery(activeOnly, type, gameId, ownedByUserId);
            var results = await mediator.Send(query, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Retrieved {Count} agents for user {UserId}",
                results.Count, session!.User!.Id);

            return Results.Ok(new
            {
                success = true,
                agents = results,
                count = results.Count
            });
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .WithName("GetAllAgents")
        .Produces(200)
        .Produces(500);
    }

    private static void MapGetAgentStatusEndpoint(RouteGroupBuilder group)
    {
        // Get agent readiness status for chat
        group.MapGet("/agents/{id:guid}/status", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            _ = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new GetAgentStatusQuery(id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result == null)
            {
                return Results.NotFound(new { error = $"Agent {id} not found" });
            }

            logger.LogInformation(
                "Agent status check: {AgentId} - Ready={IsReady}, Documents={DocumentCount}",
                id, result.IsReady, result.DocumentCount);

            return Results.Ok(result);
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .WithName("GetAgentStatus")
        .WithDescription("Check if agent is ready for chat (has KB documents and RAG initialized)")
        .Produces<AgentStatusDto>(200)
        .Produces(404)
        .Produces(500);
    }

    private static void MapConfigureAgentEndpoint(RouteGroupBuilder group)
    {
        // Configure agent
        group.MapPut("/agents/{id:guid}/configure", async (
            Guid id,
            ConfigureAgentRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated AND Admin role checked by RequireAdminSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new ConfigureAgentCommand(
                AgentId: id,
                StrategyName: req.StrategyName,
                StrategyParameters: req.StrategyParameters ?? new Dictionary<string, object>(StringComparer.Ordinal)
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return result.ErrorCode switch
                {
                    "AGENT_NOT_FOUND" => Results.NotFound(new { error = result.Message }),
                    "INVALID_CONFIGURATION" => Results.BadRequest(new { error = result.Message }),
                    _ => Results.Problem(
                        detail: result.Message,
                        statusCode: 500,
                        title: "Configuration failed")
                };
            }

            logger.LogInformation(
                "Configured agent {AgentId} by user {UserId}",
                id, session!.User!.Id);

            return Results.Ok(result);
        })
        .RequireAdminSession() // Issue #1446: Automatic admin session validation
        .WithName("ConfigureAgent")
        .Produces(200)
        .Produces(400)
        .Produces(404)
        .Produces(500);
    }

    private static void MapInvokeAgentEndpoint(RouteGroupBuilder group)
    {
        // Invoke agent - Issue #867: Game Master Agent Integration
        group.MapPost("/agents/{id:guid}/invoke", async (
            Guid id,
            InvokeAgentRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new InvokeAgentCommand(
                AgentId: id,
                Query: req.Query,
                GameId: req.GameId,
                ChatThreadId: req.ChatThreadId,
                UserId: session!.User!.Id,
                UserRole: session.User!.Role
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Agent {AgentId} invoked by user {UserId}: InvocationId={InvocationId}, Confidence={Confidence:F3}, Results={ResultCount}",
                id, session.User!.Id, result.InvocationId, result.Confidence, result.ResultCount);

            return Results.Ok(result);
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .WithName("InvokeAgent")
        .Produces(200)
        .Produces(400)
        .Produces(404)
        .Produces(500);
    }

    private static void MapUpdateAgentDocumentsEndpoint(RouteGroupBuilder group)
    {
        // Update agent document selection - Issue #2399: Knowledge Base Document Selection
        group.MapPut("/agents/{id:guid}/documents", async (
            Guid id,
            UpdateAgentDocumentsRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated AND Admin role checked by RequireAdminSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new UpdateAgentDocumentsCommand(
                AgentId: id,
                DocumentIds: req.DocumentIds ?? Array.Empty<Guid>()
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return result.ErrorCode switch
                {
                    "AGENT_NOT_FOUND" => Results.NotFound(new { error = result.Message }),
                    "NO_CONFIGURATION" => Results.BadRequest(new { error = result.Message }),
                    "DOCUMENTS_NOT_FOUND" => Results.BadRequest(new { error = result.Message }),
                    "DOCUMENTS_REQUIRED" => Results.BadRequest(new { error = result.Message }),
                    _ => Results.Problem(
                        detail: result.Message,
                        statusCode: 500,
                        title: "Update documents failed")
                };
            }

            logger.LogInformation(
                "Updated documents for agent {AgentId} by user {UserId}: {DocumentCount} documents",
                id, session!.User!.Id, result.DocumentCount);

            return Results.Ok(result);
        })
        .RequireAdminSession() // Issue #1446: Automatic admin session validation
        .WithName("UpdateAgentDocuments")
        .Produces(200)
        .Produces(400)
        .Produces(404)
        .Produces(500);
    }

    private static void MapGetAgentDocumentsEndpoint(RouteGroupBuilder group)
    {
        // Get agent document selection - Issue #2399: Knowledge Base Document Selection
        group.MapGet("/agents/{id:guid}/documents", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            _ = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new GetAgentDocumentsQuery(id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result == null)
            {
                return Results.NotFound(new { error = $"Agent {id} not found or has no configuration" });
            }

            return Results.Ok(result);
        })
        .RequireSession() // Issue #1446: Automatic session validation
        .WithName("GetAgentDocuments")
        .Produces(200)
        .Produces(404)
        .Produces(500);
    }

    /// <summary>
    /// POC: Ask agent a question with selectable search strategy and token tracking
    /// </summary>
    private static void MapAskAgentQuestionEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/chat/ask", async (
            AskAgentQuestionRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new AskAgentQuestionCommand
            {
                Question = req.Question,
                Strategy = req.Strategy,
                SessionId = req.SessionId,
                GameId = req.GameId,
                Language = req.Language,
                TopK = req.TopK ?? 5,
                MinScore = req.MinScore ?? 0.6,
                GameSessionId = req.GameSessionId,
                UserId = session.User!.Id,
                UserRole = session.User!.Role
            };

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Agent question answered: strategy={Strategy}, tokens={Tokens}, cost=${Cost}, latency={Latency}ms",
                result.Strategy, result.TokenUsage.TotalTokens, result.CostBreakdown.TotalCost, result.LatencyMs);

            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("AskAgentQuestion")
        .WithTags("POC")
        .Produces<AgentChatResponse>(200)
        .Produces(400)
        .Produces(500);
    }

    private static void MapTutorQueryEndpoint(RouteGroupBuilder group)
    {
        // ISSUE-3499: Query Tutor agent
        group.MapPost("/agents/tutor/query", async (
            TutorQueryRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new TutorQueryCommand(
                GameId: req.GameId,
                SessionId: req.SessionId,
                Query: req.Query,
                UserId: session.User!.Id,
                UserRole: session.User!.Role
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Tutor query completed for session {SessionId}: confidence={Confidence}, time={Time}ms",
                req.SessionId, result.Confidence, result.ExecutionTimeMs);

            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("TutorQuery")
        .WithTags("Agents", "Tutor")
        .Produces<TutorQueryResponse>(200)
        .Produces(400)
        .Produces(401)
        .Produces(500);
    }

    /// <summary>
    /// Get recent agents endpoint for dashboard widget.
    /// Issue #4126: API Integration.
    /// </summary>
    private static void MapGetRecentAgentsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents/recent", async (
            [FromQuery] int? limit,
            [FromServices] IMediator mediator,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;

            var query = new GetRecentAgentsQuery(limit ?? 10, session.User!.Id);
            var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("GetRecentAgents")
        .WithTags("Agents")
        .WithDescription("Get recently used agents")
        .Produces<IReadOnlyList<KbAgentDto>>(200)
        .Produces(500);
    }

    /// <summary>
    /// Chat with agent endpoint with SSE streaming.
    /// Issue #4126: API Integration for Agent Chat.
    /// </summary>
    private static void MapChatWithAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/{id:guid}/chat", async (
            [FromRoute] Guid id,
            [FromBody] ChatWithAgentRequest request,
            [FromServices] IMediator mediator,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;

            var command = new SendAgentMessageCommand(
                AgentId: id,
                UserQuestion: request.Message,
                UserId: session.User!.Id,
                ChatThreadId: request.ChatThreadId,
                UserRole: session.User!.Role,
                GameSessionId: request.GameSessionId
            );

            // Set SSE headers
            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.Append("Cache-Control", "no-cache");
            httpContext.Response.Headers.Append("Connection", "keep-alive");

            await foreach (var @event in mediator.CreateStream(command, cancellationToken).ConfigureAwait(false))
            {
                await httpContext.Response.WriteAsync(
                    $"data: {System.Text.Json.JsonSerializer.Serialize(@event, SseJsonOptions.Default)}\n\n",
                    cancellationToken).ConfigureAwait(false);

                await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        })
        .RequireSession()
        .WithName("ChatWithAgent")
        .WithTags("Agents", "Chat")
        .WithDescription("Chat with agent (SSE streaming)")
        .Produces(200)
        .Produces(400)
        .Produces(404)
        .Produces(500);
    }
    /// <summary>
    /// Unified API Gateway endpoint - routes queries to the appropriate agent automatically.
    /// Issue #4338: Unified API Gateway - /api/v1/agents/query
    /// Features: SSE streaming, intent-based routing, session authentication, rate limiting.
    /// </summary>
    private static void MapUnifiedAgentQueryEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/query", async (
            [FromBody] UnifiedAgentQueryRequest request,
            [FromServices] IMediator mediator,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.Query))
                return Results.BadRequest(new { Error = "Query cannot be empty" });

            if (request.Query.Length > 2000)
                return Results.BadRequest(new { Error = "Query exceeds maximum length of 2000 characters" });

            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;

            var command = new UnifiedAgentQueryCommand(
                Query: request.Query,
                UserId: session.User!.Id,
                GameId: request.GameId,
                ChatThreadId: request.ChatThreadId,
                PreferredAgentId: request.PreferredAgentId,
                UserRole: session.User!.Role
            );

            // Set SSE headers
            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.Append("Cache-Control", "no-cache");
            httpContext.Response.Headers.Append("Connection", "keep-alive");
            httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

            await foreach (var @event in mediator.CreateStream(command, cancellationToken).ConfigureAwait(false))
            {
                await httpContext.Response.WriteAsync(
                    $"data: {System.Text.Json.JsonSerializer.Serialize(@event, SseJsonOptions.Default)}\n\n",
                    cancellationToken).ConfigureAwait(false);

                await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            return Results.Empty;
        })
        .RequireSession()
        .RequireRateLimiting("AgentQuery")
        .WithName("UnifiedAgentQuery")
        .WithTags("Agents", "Gateway")
        .WithSummary("Unified Agent Query Gateway (SSE)")
        .WithDescription(
            "Single entry point for all agent interactions. Automatically classifies query intent " +
            "(Tutorial, RulesQuestion, MoveValidation, StrategicAnalysis) and routes to the appropriate agent " +
            "(TutorAgent, ArbitroAgent, DecisoreAgent). Returns SSE stream with real-time progress updates, " +
            "search results, and LLM-generated responses. Requires authenticated session.")
        .Produces(200, contentType: "text/event-stream")
        .Produces(400)
        .Produces(401)
        .Produces(429)
        .Produces(500);
    }

    /// <summary>
    /// Routing metrics endpoint for observability.
    /// Issue #4338: Unified API Gateway - Metrics and monitoring.
    /// </summary>
    private static void MapRoutingMetricsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents/routing-metrics", (
            [FromServices] AgentRouterService router,
            [FromServices] IntentClassifier classifier) =>
        {
            // Return basic routing capabilities and health status
            return Results.Ok(new AgentRoutingMetricsResponse(
                AvailableAgents: AvailableAgentNames,
                SupportedIntents: Enum.GetNames<AgentIntent>(),
                ConfidenceThresholds: new ConfidenceThresholdInfo(
                    HighConfidence: 0.90,
                    MediumConfidence: 0.70,
                    MinimumForRouting: 0.40
                ),
                Status: "healthy"
            ));
        })
        .RequireSession()
        .WithName("GetRoutingMetrics")
        .WithTags("Agents", "Gateway", "Metrics")
        .WithSummary("Agent routing metrics and health")
        .WithDescription(
            "Returns routing system health, available agents, supported intents, " +
            "and confidence thresholds for the multi-agent gateway.")
        .Produces<AgentRoutingMetricsResponse>(200)
        .Produces(401);
    }

    /// <summary>
    /// Create a user-owned agent with tier-aware configuration.
    /// Issue #4683: User Agent CRUD Endpoints.
    /// </summary>
    private static void MapCreateUserAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/user", async (
            CreateUserAgentRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new CreateUserAgentCommand(
                UserId: session.User!.Id,
                UserTier: session.User.Tier,
                UserRole: session.User.Role,
                GameId: req.GameId,
                AgentType: req.AgentType,
                Name: req.Name,
                StrategyName: req.StrategyName,
                StrategyParameters: req.StrategyParameters,
                DocumentIds: req.DocumentIds
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "User {UserId} created agent {AgentId} for game {GameId}",
                session.User.Id, result.Id, req.GameId);

            return Results.Created($"/api/v1/agents/{result.Id}", result);
        })
        .RequireSession()
        .RequireRateLimiting("AgentCreation")
        .WithName("CreateUserAgent")
        .WithTags("Agents", "User")
        .WithSummary("Create a user-owned agent (tier-aware)")
        .WithDescription(
            "Creates an agent associated to a game. Configuration depth depends on user tier: " +
            "Free=type only, Normal=type+strategy, Premium/Admin=full config.")
        .Produces<KbAgentDto>(201)
        .Produces(400)
        .Produces(401)
        .Produces(409)
        .Produces(429);
    }

    /// <summary>
    /// Update a user-owned agent configuration.
    /// Issue #4683: User Agent CRUD Endpoints.
    /// </summary>
    private static void MapUpdateUserAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/agents/{id:guid}/user", async (
            Guid id,
            UpdateUserAgentRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new UpdateUserAgentCommand(
                AgentId: id,
                UserId: session.User!.Id,
                UserTier: session.User.Tier,
                UserRole: session.User.Role,
                Name: req.Name,
                StrategyName: req.StrategyName,
                StrategyParameters: req.StrategyParameters
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "User {UserId} updated agent {AgentId}",
                session.User.Id, id);

            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("UpdateUserAgent")
        .WithTags("Agents", "User")
        .WithSummary("Update a user-owned agent (owner/admin only)")
        .Produces<KbAgentDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    /// <summary>
    /// Delete a user-owned agent.
    /// Issue #4683: User Agent CRUD Endpoints.
    /// </summary>
    private static void MapDeleteUserAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/agents/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new DeleteUserAgentCommand(
                AgentId: id,
                UserId: session.User!.Id,
                UserRole: session.User.Role
            );

            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "User {UserId} deleted agent {AgentId}",
                session.User.Id, id);

            return Results.NoContent();
        })
        .RequireSession()
        .WithName("DeleteUserAgent")
        .WithTags("Agents", "User")
        .WithSummary("Delete a user-owned agent (owner/admin only)")
        .Produces(204)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    /// <summary>
    /// Get user's agent slot allocation and usage.
    /// Issue #4771: Agent Slots Endpoint + Quota System.
    /// </summary>
    private static void MapGetUserAgentSlotsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/user/agent-slots", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new GetUserAgentSlotsQuery(
                UserId: session.User!.Id,
                UserTier: session.User.Tier,
                UserRole: session.User.Role
            );

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("GetUserAgentSlots")
        .WithTags("Agents", "User")
        .WithSummary("Get agent slot allocation for the current user")
        .WithDescription(
            "Returns total, used, and available agent slots based on the user's tier. " +
            "Each slot shows the associated agent details or availability status.")
        .Produces<UserAgentSlotsDto>(200)
        .Produces(401);
    }

    /// <summary>
    /// Orchestrated agent creation: validates slots, optionally adds game to library,
    /// creates agent, creates initial chat thread.
    /// Issue #4772: Agent Creation Orchestration Flow.
    /// </summary>
    private static void MapCreateAgentWithSetupEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/create-with-setup", async (
            CreateAgentWithSetupRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new CreateAgentWithSetupCommand(
                UserId: session.User!.Id,
                UserTier: session.User.Tier,
                UserRole: session.User.Role,
                GameId: req.GameId,
                AddToCollection: req.AddToCollection,
                AgentType: req.AgentType,
                AgentName: req.AgentName,
                StrategyName: req.StrategyName,
                StrategyParameters: req.StrategyParameters
            )
            {
                SharedGameId = req.SharedGameId,
                DocumentIds = req.DocumentIds
            };

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "User {UserId} created agent {AgentId} with setup for game {GameId}",
                session.User.Id, result.AgentId, req.GameId);

            return Results.Created($"/api/v1/agents/{result.AgentId}", result);
        })
        .RequireSession()
        .RequireRateLimiting("AgentCreation")
        .WithName("CreateAgentWithSetup")
        .WithTags("Agents", "User")
        .WithSummary("Orchestrated agent creation with auto-setup")
        .WithDescription(
            "Creates an agent with full setup: validates slot availability, " +
            "optionally adds game to library, creates the agent, and creates an initial chat thread.")
        .Produces<AgentCreationResultDto>(201)
        .Produces(400)
        .Produces(401)
        .Produces(409)
        .Produces(429);
    }

    /// <summary>
    /// Get the current LLM configuration for a user-owned agent.
    /// </summary>
    private static void MapGetAgentConfigurationEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents/{id:guid}/configuration", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new GetAgentConfigurationQuery(
                AgentId: id,
                UserId: session.User!.Id,
                UserRole: session.User.Role
            );

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("GetAgentConfiguration")
        .WithTags("Agents", "User")
        .WithSummary("Get current LLM configuration for a user-owned agent")
        .Produces<AgentConfigurationDto>(200)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    /// <summary>
    /// Ask the arbiter agent to resolve a dispute between players.
    /// Issue #5585: Arbiter Mode — dispute arbitration with citations and verdict.
    /// </summary>
    private static void MapAskArbiterEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/{id:guid}/arbiter", async (
            [FromRoute] Guid id,
            [FromBody] AskArbiterRequest request,
            [FromServices] IMediator mediator,
            HttpContext httpContext,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;

            var command = new AskArbiterCommand
            {
                AgentDefinitionId = id,
                SessionId = request.SessionId,
                Situation = request.Situation,
                PositionA = request.PositionA,
                PositionB = request.PositionB,
                UserId = session.User!.Id
            };

            var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Arbiter verdict for Agent {AgentId}: confidence={Confidence:F2}, conclusive={IsConclusive}, citations={CitationCount}",
                id, result.Confidence, result.IsConclusive, result.Citations.Count);

            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("AskArbiter")
        .WithTags("Agents", "Arbiter")
        .WithSummary("Request arbiter verdict on a dispute between players")
        .WithDescription(
            "Submits a dispute between two player positions for arbitration. " +
            "The arbiter searches the rulebook, cites exact passages, and provides " +
            "a structured verdict with confidence level.")
        .Produces<ArbiterVerdictDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .Produces(500);
    }

    /// <summary>
    /// Patch the LLM configuration for a user-owned agent.
    /// Only non-null fields are updated. Enforces tier-based model restrictions.
    /// </summary>
    private static void MapUpdateAgentConfigurationEndpoint(RouteGroupBuilder group)
    {
        group.MapPatch("/agents/{id:guid}/configuration", async (
            Guid id,
            UpdateAgentConfigurationRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new UpdateAgentLlmConfigurationCommand(
                AgentId: id,
                UserId: session.User!.Id,
                UserTier: session.User.Tier,
                UserRole: session.User.Role,
                ModelId: req.ModelId,
                Temperature: req.Temperature,
                MaxTokens: req.MaxTokens,
                SelectedDocumentIds: req.SelectedDocumentIds
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "User {UserId} updated LLM config for agent {AgentId}",
                session.User.Id, id);

            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("UpdateAgentConfiguration")
        .WithTags("Agents", "User")
        .WithSummary("Update LLM configuration for a user-owned agent (partial update)")
        .Produces<AgentConfigurationDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    /// <summary>
    /// Quick-create an agent + chat thread for a game after ownership is declared.
    /// Automatically selects all indexed KB cards.
    /// </summary>
    private static void MapQuickCreateAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/quick-create", async (
            QuickCreateAgentRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new QuickCreateAgentCommand(
                UserId: session.User!.Id,
                GameId: req.GameId,
                SharedGameId: req.SharedGameId,
                UserRole: session.User.Role,
                UserTier: session.User.Tier
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "User {UserId} quick-created agent {AgentId} for game {GameId}",
                session.User.Id, result.AgentId, req.GameId);

            return Results.Created($"/api/v1/agents/{result.AgentId}", result);
        })
        .RequireSession()
        .RequireRateLimiting("AgentCreation")
        .WithName("QuickCreateAgent")
        .WithTags("Agents", "User", "Ownership")
        .WithSummary("Quick-create agent for an owned game")
        .WithDescription(
            "Creates a Tutor agent with all indexed KB cards + an initial chat thread. " +
            "Requires RAG access (ownership declared or game is RAG-public).")
        .Produces<QuickCreateAgentResult>(201)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(429);
    }
}

/// <summary>
/// Request for tutor agent query.
/// </summary>
internal record TutorQueryRequest(
    Guid GameId,
    Guid SessionId,
    string Query
);

/// <summary>
/// Request for chat with agent (SSE streaming).
/// Issue #4126, Issue #4386: SSE Stream, Issue #5580: GameSessionId for session-aware RAG.
/// </summary>
internal record ChatWithAgentRequest(
    string Message,
    Guid? ChatThreadId = null,
    Guid? GameSessionId = null
);

// Request DTOs
internal record CreateAgentRequest(
    string Name,
    string Type,
    string StrategyName,
    IDictionary<string, object>? StrategyParameters,
    bool? IsActive
);

internal record ConfigureAgentRequest(
    string StrategyName,
    IDictionary<string, object>? StrategyParameters
);

internal record InvokeAgentRequest(
    string Query,
    Guid? GameId = null,
    Guid? ChatThreadId = null
);

/// <summary>
/// Request to update agent document selection.
/// Issue #2399: Knowledge Base Document Selection.
/// </summary>
internal record UpdateAgentDocumentsRequest(
    IReadOnlyList<Guid>? DocumentIds
);

/// <summary>
/// Request for the Unified API Gateway.
/// Issue #4338: Unified API Gateway - /api/v1/agents/query
/// </summary>
internal record UnifiedAgentQueryRequest(
    string Query,
    Guid? GameId = null,
    Guid? ChatThreadId = null,
    Guid? PreferredAgentId = null
);

/// <summary>
/// POC: Request to ask agent a question with search strategy selection
/// </summary>
/// <summary>
/// POC: Request to ask agent a question with search strategy selection.
/// Issue #5580: GameSessionId for session-aware RAG filtering.
/// </summary>
internal record AskAgentQuestionRequest(
    string Question,
    Api.BoundedContexts.KnowledgeBase.Domain.Enums.AgentSearchStrategy Strategy,
    string? SessionId = null,
    Guid? GameId = null,
    string? Language = null,
    int? TopK = null,
    double? MinScore = null,
    Guid? GameSessionId = null
);

/// <summary>
/// Response for routing metrics endpoint.
/// Issue #4338: Unified API Gateway - Observability.
/// </summary>
internal record AgentRoutingMetricsResponse(
    string[] AvailableAgents,
    string[] SupportedIntents,
    ConfidenceThresholdInfo ConfidenceThresholds,
    string Status
);

internal record ConfidenceThresholdInfo(
    double HighConfidence,
    double MediumConfidence,
    double MinimumForRouting
);

/// <summary>
/// Request for user-facing agent creation.
/// Issue #4683: User Agent CRUD Endpoints.
/// </summary>
internal record CreateUserAgentRequest(
    Guid GameId,
    string AgentType,
    string? Name = null,
    string? StrategyName = null,
    IDictionary<string, object>? StrategyParameters = null,
    IReadOnlyList<Guid>? DocumentIds = null
);

/// <summary>
/// Request for user-facing agent update.
/// Issue #4683: User Agent CRUD Endpoints.
/// </summary>
internal record UpdateUserAgentRequest(
    string? Name = null,
    string? StrategyName = null,
    IDictionary<string, object>? StrategyParameters = null
);

/// <summary>
/// Request for orchestrated agent creation with setup.
/// Issue #4772: Agent Creation Orchestration Flow.
/// </summary>
internal record CreateAgentWithSetupRequest(
    Guid GameId,
    bool AddToCollection,
    string AgentType,
    string? AgentName = null,
    string? StrategyName = null,
    IDictionary<string, object>? StrategyParameters = null,
    Guid? SharedGameId = null,
    List<Guid>? DocumentIds = null
);

/// <summary>
/// Request for updating agent LLM configuration (partial update).
/// </summary>
internal record UpdateAgentConfigurationRequest(
    string? ModelId = null,
    decimal? Temperature = null,
    int? MaxTokens = null,
    IReadOnlyList<Guid>? SelectedDocumentIds = null
);

/// <summary>
/// Request for arbiter dispute resolution.
/// Issue #5585: Arbiter Mode — dispute arbitration with citations and verdict.
/// </summary>
internal record AskArbiterRequest(
    Guid SessionId,
    string Situation,
    string PositionA,
    string PositionB
);

/// <summary>
/// Request for quick agent creation after ownership declaration.
/// </summary>
internal record QuickCreateAgentRequest(
    Guid GameId,
    Guid? SharedGameId = null
);
