using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using Api.Infrastructure.Entities;
using Api.Middleware;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Api.Models;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Routing;

/// <summary>
/// DDD-PHASE3: Agent management endpoints for KnowledgeBase bounded context.
/// Issue 866: AI Agents Entity and Configuration.
/// </summary>
internal static class AgentEndpoints
{
    public static RouteGroupBuilder MapAgentEndpoints(this RouteGroupBuilder group)
    {
        MapCreateAgentEndpoint(group);
        MapGetAgentByIdEndpoint(group);
        MapGetAllAgentsEndpoint(group);
        MapConfigureAgentEndpoint(group);
        MapInvokeAgentEndpoint(group);
        MapUpdateAgentDocumentsEndpoint(group);
        MapGetAgentDocumentsEndpoint(group);
        MapTutorQueryEndpoint(group); // ISSUE-3499

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
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new GetAllAgentsQuery(activeOnly, type);
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
                UserId: session!.User!.Id
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
            _ = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new TutorQueryCommand(
                GameId: req.GameId,
                SessionId: req.SessionId,
                Query: req.Query
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
}

/// <summary>
/// Request for tutor agent query.
/// </summary>
internal record TutorQueryRequest(
    Guid GameId,
    Guid SessionId,
    string Query
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
