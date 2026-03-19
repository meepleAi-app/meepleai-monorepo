using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Session tracking endpoints for collaborative game sessions.
/// Delegates to focused sub-files in the SessionTracking/ directory.
/// </summary>
internal static class SessionTrackingEndpoints
{
    public static RouteGroupBuilder MapSessionTrackingEndpoints(this RouteGroupBuilder group)
    {
        // Core session commands
        SessionCommandEndpoints.Map(group);

        // Card deck management (Issue #3343)
        SessionCardDeckEndpoints.Map(group);

        // Private notes (Issue #3344)
        SessionNotesEndpoints.Map(group);

        // Random tools: timer/coin/wheel (Issue #3345)
        SessionToolsEndpoints.Map(group);

        // All GET/query endpoints (active session, scoreboard, details, history, SSE, export, invite, media, chat, toolkit, diary, checkpoints)
        SessionQueryEndpoints.Map(group);

        // Player actions, join/role, write operations for media/chat/invite/toolkit/diary/AI/checkpoints
        SessionPlayerActionsEndpoints.Map(group);

        return group;
    }

    internal static RouteGroupBuilder MapSessionStatisticsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/session-statistics", async (
            HttpContext httpContext, IMediator mediator, [FromQuery] int? monthsBack) =>
        {
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var parsedUserId))
                return Results.Unauthorized();

            var query = new Api.BoundedContexts.SessionTracking.Application.Queries.GetSessionStatisticsQuery(
                parsedUserId, monthsBack ?? 6);
            var result = await mediator.Send(query).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .Produces<Api.BoundedContexts.SessionTracking.Application.DTOs.SessionStatisticsDto>(200)
        .Produces(401)
        .WithName("GetSessionStatistics")
        .WithTags("SessionStatistics")
        .WithSummary("Get aggregated session statistics for the current user")
        .WithOpenApi();

        group.MapGet("/game-sessions/session-statistics/game/{gameId:guid}", async (
            Guid gameId, HttpContext httpContext, IMediator mediator) =>
        {
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var parsedUserId))
                return Results.Unauthorized();

            var query = new Api.BoundedContexts.SessionTracking.Application.Queries.GetGameStatisticsQuery(
                parsedUserId, gameId);
            var result = await mediator.Send(query).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .Produces<Api.BoundedContexts.SessionTracking.Application.DTOs.GameStatisticsDto>(200)
        .Produces(401)
        .WithName("GetGameStatistics")
        .WithTags("SessionStatistics")
        .WithSummary("Get statistics for a specific game for the current user")
        .WithOpenApi();

        return group;
    }
}
