using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameContributors;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributions;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributionStats;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Contributor-related SharedGameCatalog endpoints (Issue #2735).
/// </summary>
internal static class SharedGameCatalogContributorEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        // Public: Get contributors for a shared game
        group.MapGet("/shared-games/{id:guid}/contributors", HandleGetGameContributors)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetGameContributors")
            .WithSummary("Get contributors for a shared game")
            .WithDescription("Returns all contributors who have contributed to a shared game, including their contribution count and top badges.")
            .Produces<List<GameContributorDto>>()
            .Produces(StatusCodes.Status404NotFound);

        // Public: Get user's contributions (paginated)
        group.MapGet("/users/{id:guid}/contributions", HandleGetUserContributions)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetUserContributions")
            .WithSummary("Get user's contributions")
            .WithDescription("Returns paginated list of contributions made by a specific user to shared games.")
            .Produces<PagedResult<UserContributionDto>>();

        // Private: Get my contribution stats
        group.MapGet("/users/me/contribution-stats", HandleGetMyContributionStats)
            .RequireAuthorization()
            .RequireRateLimiting("UserDashboard")
            .WithName("GetMyContributionStats")
            .WithSummary("Get my contribution statistics")
            .WithDescription("Returns detailed contribution statistics for the authenticated user, including badges, rate limits, and approval rate.")
            .Produces<UserContributionStatsDto>();
    }

    // ========================================
    // CONTRIBUTOR HANDLERS
    // ========================================

    private static async Task<IResult> HandleGetGameContributors(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetGameContributorsQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetUserContributions(
        Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        IMediator mediator = default!,
        CancellationToken ct = default)
    {
        var query = new GetUserContributionsQuery(
            id,
            pageNumber,
            pageSize);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetMyContributionStats(
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

        var query = new GetUserContributionStatsQuery(userId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
