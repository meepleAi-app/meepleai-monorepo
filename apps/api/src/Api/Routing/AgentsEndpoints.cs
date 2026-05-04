using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// User-facing Agents endpoints (read-only listing + by-id lookup).
/// Issue #641 (Wave B.2 hotfix): expose existing GetAllAgentsQuery handler over HTTP
/// so the frontend <c>useAgents</c> hook can resolve agents at <c>GET /api/v1/agents</c>.
/// Issue #647 (Phase γ.1): expose <c>GET /api/v1/agents/{id}</c> for the single-agent
/// detail surface consumed by the frontend <c>agentsClient.getById</c> helper.
/// </summary>
internal static class AgentsEndpoints
{
    public static RouteGroupBuilder MapAgentsEndpoints(this RouteGroupBuilder group)
    {
        MapGetAgentsEndpoint(group);
        MapGetAgentByIdEndpoint(group);
        return group;
    }

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
}
