using Api.BoundedContexts.SharedGameCatalog.Application.Commands.ToggleBadgeDisplay;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetAllBadges;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetBadgeLeaderboard;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserBadges;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Badge and leaderboard endpoints for SharedGameCatalog (Issue #2736).
/// </summary>
internal static class SharedGameCatalogBadgeEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        // Public: Get all badge definitions
        group.MapGet("/badges", HandleGetAllBadges)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetAllBadges")
            .WithSummary("Get all badge definitions")
            .WithDescription("Returns all available badge types with requirement descriptions. Results are cached for 24 hours.")
            .Produces<List<BadgeDefinitionDto>>();

        // Public: Get user's badges (excludes hidden)
        group.MapGet("/users/{id:guid}/badges", HandleGetUserBadges)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetUserBadges")
            .WithSummary("Get user's badges")
            .WithDescription("Returns all visible badges earned by a specific user. Hidden badges are excluded.")
            .Produces<List<UserBadgeDto>>();

        // Private: Get my badges (includes hidden)
        group.MapGet("/users/me/badges", HandleGetMyBadges)
            .RequireAuthorization()
            .RequireRateLimiting("UserDashboard")
            .WithName("GetMyBadges")
            .WithSummary("Get my badges")
            .WithDescription("Returns all badges earned by the authenticated user, including hidden badges.")
            .Produces<List<UserBadgeDto>>();

        // Public: Get badge leaderboard
        group.MapGet("/badges/leaderboard", HandleGetLeaderboard)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetBadgeLeaderboard")
            .WithSummary("Get badge leaderboard")
            .WithDescription("Returns top contributors with badge counts, highest tier, and top 3 badges. Supports period filtering (Week, Month, AllTime). Results are cached for 1 hour.")
            .Produces<List<LeaderboardEntryDto>>();

        // Private: Toggle badge visibility
        group.MapPut("/users/me/badges/{id:guid}/display", HandleToggleBadgeDisplay)
            .RequireAuthorization()
            .RequireRateLimiting("UserDashboard")
            .WithName("ToggleBadgeDisplay")
            .WithSummary("Toggle badge visibility")
            .WithDescription("Toggle whether a badge is displayed on your public profile.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    // ========================================
    // BADGE HANDLERS (Issue #2736)
    // ========================================

    private static async Task<IResult> HandleGetAllBadges(
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetAllBadgesQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetUserBadges(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetUserBadgesQuery(id, IncludeHidden: false);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetMyBadges(
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract user ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var query = new GetUserBadgesQuery(userId, IncludeHidden: true);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetLeaderboard(
        [FromQuery] LeaderboardPeriod period,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        IMediator mediator = default!,
        CancellationToken ct = default)
    {
        var query = new GetBadgeLeaderboardQuery(period, pageNumber, pageSize);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleToggleBadgeDisplay(
        Guid id,
        ToggleBadgeDisplayRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract user ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var command = new ToggleBadgeDisplayCommand(id, userId, request.IsDisplayed);

        await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.NoContent();
    }
}
