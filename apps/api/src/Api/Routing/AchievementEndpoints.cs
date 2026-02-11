using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Gamification.Application.DTOs;
using Api.BoundedContexts.Gamification.Application.Queries.GetAchievements;
using Api.BoundedContexts.Gamification.Application.Queries.GetRecentAchievements;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Achievement system endpoints.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal static class AchievementEndpoints
{
    public static RouteGroupBuilder MapAchievementEndpoints(this RouteGroupBuilder group)
    {
        MapGetAchievementsEndpoint(group);
        MapGetRecentAchievementsEndpoint(group);

        return group;
    }

    private static void MapGetAchievementsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/achievements", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetAchievementsQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<List<AchievementDto>>(200)
        .Produces(401)
        .WithTags("Achievements")
        .WithSummary("Get all achievements with user unlock status")
        .WithDescription("Returns all active achievements with the authenticated user's progress and unlock status.")
        .WithOpenApi();
    }

    private static void MapGetRecentAchievementsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/achievements/recent", async (
            [FromQuery] int? limit,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetRecentAchievementsQuery(userId, limit ?? 5);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<List<RecentAchievementDto>>(200)
        .Produces(401)
        .WithTags("Achievements")
        .WithSummary("Get recently unlocked achievements")
        .WithDescription("Returns the most recently unlocked achievements for the authenticated user, ordered by unlock date descending.")
        .WithOpenApi();
    }

    private static bool TryGetUserId(HttpContext context, SessionStatusDto? session, out Guid userId)
    {
        userId = Guid.Empty;
        if (session != null)
        {
            userId = session.User!.Id;
            return true;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out userId))
        {
            return true;
        }

        return false;
    }
}
