using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// User-facing agent typologies endpoint (Issue #649).
/// "Typologies" was collapsed into AgentDefinition during system simplification —
/// this route exposes Published agent definitions as the user-facing typology dropdown source.
/// </summary>
internal static class AgentTypologiesEndpoints
{
    public static RouteGroupBuilder MapAgentTypologiesEndpoints(this RouteGroupBuilder group)
    {
        MapGetTypologiesEndpoint(group);
        return group;
    }

    private static void MapGetTypologiesEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agent-typologies", async (
            [FromQuery] string? status,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            // Frontend sends ?status=Approved (legacy term) → map to Published.
            // Default behavior: only return Published typologies (user-facing).
            var publishedOnly = string.IsNullOrEmpty(status) ||
                                string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase);

            var query = new GetAllAgentDefinitionsQuery(PublishedOnly: publishedOnly);
            var typologies = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                success = true,
                typologies,
                total = typologies.Count
            });
        })
        .RequireAuthenticatedUser()
        .Produces(200)
        .Produces(401)
        .WithTags("AgentTypologies")
        .WithSummary("List agent typologies")
        .WithDescription("Returns Published agent definitions used as user-facing typology dropdown source. Issue #649.")
        .WithOpenApi();
    }
}
