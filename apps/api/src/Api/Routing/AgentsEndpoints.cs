using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// User-facing Agents endpoints (read-only listing + by-id lookup + recent widget + readiness status).
/// Issue #641 (Wave B.2 hotfix): expose existing GetAllAgentsQuery handler over HTTP
/// so the frontend <c>useAgents</c> hook can resolve agents at <c>GET /api/v1/agents</c>.
/// Issue #647 (Phase γ.1): expose <c>GET /api/v1/agents/{id}</c> for the single-agent
/// detail surface consumed by the frontend <c>agentsClient.getById</c> helper.
/// Issue #650 (Phase γ.3): expose <c>GET /api/v1/agents/recent</c> for the dashboard
/// recent-agents widget consumed by the frontend <c>useRecentAgents</c> hook.
/// Issue #648 (Phase γ.2): expose <c>GET /api/v1/agents/{id}/status</c> for the
/// readiness widget consumed by the frontend <c>useAgentStatus</c> hook.
/// Issue #654 (Phase β.2): expose <c>POST /api/v1/agents/user</c> for the
/// frontend <c>agentsClient.createUserAgent</c> helper. MVP defers documentIds
/// linking and tier/quota validation.
/// Issue #655 (Phase β.3): expose <c>POST /api/v1/agents/create-with-setup</c>
/// orchestration route consumed by the AgentCreationSheet wizard. Optional
/// addToCollection step + agent creation (β.2 reuse). Placeholder threadId
/// (chat-thread BC integration deferred) + slotUsed=0 (Issue #4771 deferred).
/// </summary>
internal static class AgentsEndpoints
{
    public static RouteGroupBuilder MapAgentsEndpoints(this RouteGroupBuilder group)
    {
        MapGetAgentsEndpoint(group);
        // Recent must be registered before the {id:guid} route so that the literal "recent"
        // segment never reaches the GUID-constrained handler. The :guid constraint already
        // disambiguates at the matcher level, but explicit ordering keeps intent obvious.
        MapGetRecentAgentsEndpoint(group);
        MapGetAgentByIdEndpoint(group);
        MapGetAgentStatusEndpoint(group);
        MapCreateUserAgentEndpoint(group);
        MapCreateAgentWithSetupEndpoint(group);
        return group;
    }

    /// <summary>
    /// Frontend <c>agentsClient.createUserAgent</c> request body shape.
    /// Mirrors <c>CreateUserAgentRequest</c> in <c>apps/web/src/lib/api/clients/agentsClient.ts</c>.
    /// </summary>
    private sealed record CreateUserAgentRequest(
        Guid GameId,
        string AgentType,
        string? Name = null,
        string? StrategyName = null,
        Dictionary<string, object>? StrategyParameters = null,
        List<Guid>? DocumentIds = null
    );

    /// <summary>
    /// Frontend <c>agentsClient.createWithSetup</c> request body shape.
    /// Mirrors <c>CreateAgentWithSetupRequest</c> in
    /// <c>apps/web/src/lib/api/clients/agentsClient.ts</c>. Issue #655 (Phase β.3).
    /// </summary>
    private sealed record CreateAgentWithSetupRequest(
        Guid GameId,
        bool AddToCollection,
        string AgentType,
        string? AgentName = null,
        string? StrategyName = null,
        Dictionary<string, object>? StrategyParameters = null,
        List<Guid>? DocumentIds = null
    );

    private static void MapGetAgentsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents", async (
            [FromQuery] bool? activeOnly,
            [FromQuery] string? type,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            var query = new GetAllAgentsQuery(
                ActiveOnly: activeOnly,
                Type: type
            );
            var agents = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                success = true,
                agents,
                count = agents.Count
            });
        })
        .RequireAuthenticatedUser()
        .Produces(200)
        .Produces(401)
        .WithTags("Agents")
        .WithSummary("List all agents")
        .WithDescription("Returns all agents with optional activeOnly and type filters. Issue #641 Wave B.2 hotfix.")
        .WithOpenApi();
    }

    private static void MapGetAgentByIdEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents/{id:guid}", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            var query = new GetAgentByIdQuery(id);
            var dto = await mediator.Send(query, ct).ConfigureAwait(false);

            return dto is null ? Results.NotFound() : Results.Ok(dto);
        })
        .RequireAuthenticatedUser()
        .Produces<AgentDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Agents")
        .WithSummary("Get agent by ID")
        .WithDescription("Returns a single agent definition by id, mapped to AgentDto with GameName resolved via SharedGame catalog (PR #662 drift-fix). Issue #647 (Phase γ.1).")
        .WithOpenApi();
    }

    private static void MapGetRecentAgentsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents/recent", async (
            [FromQuery] int? limit,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            var query = new GetRecentAgentsQuery(Limit: limit ?? 10);
            var agents = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(agents);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<AgentDto>>(200)
        .Produces(401)
        .WithTags("Agents")
        .WithSummary("Get recently used agents")
        .WithDescription("Returns active agents ordered by LastInvokedAt desc (limit clamped 1..50, default 10). Powers the dashboard recent-agents widget (frontend useRecentAgents hook). Issue #650 (Phase γ.3).")
        .WithOpenApi();
    }

    private static void MapGetAgentStatusEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents/{id:guid}/status", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            var query = new GetAgentStatusQuery(id);
            var dto = await mediator.Send(query, ct).ConfigureAwait(false);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        })
        .RequireAuthenticatedUser()
        .Produces<AgentStatusDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Agents")
        .WithSummary("Get agent readiness status")
        .WithDescription("Returns readiness derived from AgentDefinition flags (IsActive AND Strategy.Name presence). HasDocuments precise count is deferred to a follow-up if needed. Issue #648 — useAgentStatus widget.")
        .WithOpenApi();
    }

    private static void MapCreateUserAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/user", async (
            [FromBody] CreateUserAgentRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            try
            {
                var command = new CreateUserAgentCommand(
                    UserId: userId,
                    GameId: request.GameId,
                    AgentType: request.AgentType,
                    Name: request.Name,
                    StrategyName: request.StrategyName,
                    StrategyParameters: request.StrategyParameters,
                    DocumentIds: request.DocumentIds
                );

                var dto = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/agents/{dto.Id}", dto);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<AgentDto>(201)
        .Produces(400)
        .Produces(401)
        .WithTags("Agents")
        .WithSummary("Create a user-owned agent")
        .WithDescription("Creates a custom agent linked to a SharedGame, returning AgentDto with GameName resolved. MVP: documentIds parameter accepted but not linked (deferred); tier/quota validation deferred (Issue #4771). Issue #654 (Phase β.2).")
        .WithOpenApi();
    }

    private static void MapCreateAgentWithSetupEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/create-with-setup", async (
            [FromBody] CreateAgentWithSetupRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            try
            {
                var command = new CreateAgentWithSetupCommand(
                    UserId: userId,
                    GameId: request.GameId,
                    AddToCollection: request.AddToCollection,
                    AgentType: request.AgentType,
                    AgentName: request.AgentName,
                    StrategyName: request.StrategyName,
                    StrategyParameters: request.StrategyParameters,
                    DocumentIds: request.DocumentIds);

                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/agents/{result.AgentId}", result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<CreateAgentWithSetupResult>(201)
        .Produces(400)
        .Produces(401)
        .WithTags("Agents")
        .WithSummary("Create agent with optional library add + thread")
        .WithDescription("Orchestrates: optional addToCollection → AddGameToLibraryCommand (idempotent: 'already in' counts as success) → CreateUserAgentCommand → result with placeholder threadId (chat-thread BC integration deferred) and slotUsed=0 (tier/quota Issue #4771 deferred). Issue #655 (Phase β.3).")
        .WithOpenApi();
    }
}
