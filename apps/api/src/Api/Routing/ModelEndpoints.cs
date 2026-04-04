using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Middleware;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// AI model configuration endpoints for KnowledgeBase bounded context.
/// Issue #3377: Models Tier Endpoint
/// </summary>
internal static class ModelEndpoints
{
    public static RouteGroupBuilder MapModelEndpoints(this RouteGroupBuilder group)
    {
        MapGetAllModelsEndpoint(group);

        return group;
    }

    private static void MapGetAllModelsEndpoint(RouteGroupBuilder group)
    {
        // GET /models - Get all available AI models with optional tier filtering
        // Issue #3377: Models Tier Endpoint
        group.MapGet("/models", async (
            [FromQuery] string? tier,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var query = new GetAvailableModelsQuery(tier);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Retrieved {Count} models with tier filter: {Tier}",
                result.Models.Count,
                LogValueSanitizer.Sanitize(tier ?? "all"));

            return Results.Ok(result);
        })
        .WithName("GetAvailableModels")
        .WithTags("AI Models")
        .WithDescription("Get available AI models. Optionally filter by tier (free, normal, premium, custom).")
        .WithOpenApi()
        .Produces(200)
        .Produces(500);
    }
}
