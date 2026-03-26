using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using Api.Middleware;
using Api.Services;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Cache management endpoints (Admin only).
/// Handles cache statistics retrieval and cache invalidation operations.
/// </summary>
internal static class CacheEndpoints
{
    public static RouteGroupBuilder MapCacheEndpoints(this RouteGroupBuilder group)
    {
        // PERF-03: Cache management endpoints - DDD Migration Phase 3.2
        group.MapGet("/admin/cache/stats", async (HttpContext context, IMediator mediator, string? gameId = null, CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetCacheStatsQuery(GameId: gameId);
            var stats = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(stats);
        })
        .WithName("GetCacheStats")
        .WithDescription("Get cache statistics with optional game filter (Admin only)")
        .WithTags("Admin", "Cache")
        .Produces<CacheStats>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status500InternalServerError);

        group.MapDelete("/admin/cache/games/{gameId:guid}", async (Guid gameId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Validate game exists using CQRS Query (but proceed with cache invalidation even if not - idempotent)
            var game = await mediator.Send(new GetGameByIdQuery(gameId), ct).ConfigureAwait(false);
            if (game == null)
            {
                logger.LogWarning("Admin {AdminId} invalidating cache for non-existent game {GameId} (idempotent)", session!.User!.Id, gameId);
            }

            logger.LogInformation("Admin {AdminId} invalidating cache for game {GameId}", session!.User!.Id, gameId);

            // DDD Migration Phase 3.2: Use InvalidateGameCacheCommand via IMediator
            var command = new InvalidateGameCacheCommand(GameId: gameId);
            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Successfully invalidated cache for game {GameId}", gameId);
            return Results.Json(new { ok = true, message = $"Cache invalidated for game '{gameId}'" });
        })
        .WithName("InvalidateGameCache")
        .WithDescription("Invalidate all cached responses for a specific game (Admin only)")
        .WithTags("Admin", "Cache")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status500InternalServerError);

        group.MapDelete("/admin/cache/tags/{tag}", async (string tag, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            if (string.IsNullOrWhiteSpace(tag))
            {
                return Results.BadRequest(new { error = "tag is required" });
            }

            logger.LogInformation("Admin {AdminId} invalidating cache by tag {Tag}", session!.User!.Id, LogValueSanitizer.Sanitize(tag));

            // DDD Migration Phase 3.2: Use InvalidateCacheByTagCommand via IMediator
            var command = new InvalidateCacheByTagCommand(Tag: tag);
            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Successfully invalidated cache by tag {Tag}", LogValueSanitizer.Sanitize(tag));
            return Results.Json(new { ok = true, message = $"Cache invalidated for tag '{tag}'" });
        })
        .WithName("InvalidateCacheByTag")
        .WithDescription("Invalidate cache entries by tag (e.g., game:chess, pdf:abc123) (Admin only)")
        .WithTags("Admin", "Cache")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status500InternalServerError);

        return group;
    }
}
