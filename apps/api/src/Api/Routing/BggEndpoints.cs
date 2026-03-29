using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Extensions;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Routing;

/// <summary>
/// BoardGameGeek API integration endpoints.
/// Issue #3120: Provides admin-only search and game details lookup from BGG.
/// Restricted to admin due to BGG commercial use licensing.
/// Unified: Uses IBggApiService (rich DTOs with categories, mechanics, etc.)
/// </summary>
internal static class BggEndpoints
{
    public static RouteGroupBuilder MapBggEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/v1/bgg/search?q={term} (also accepts ?query={term})
        group.MapGet("/bgg/search", async (
            [FromQuery(Name = "q")] string? q,
            [FromQuery(Name = "query")] string? query,
            [FromQuery(Name = "page")] int page,
            [FromQuery(Name = "pageSize")] int pageSize,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var searchTerm = q ?? query;
            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
                return Results.BadRequest(new { error = "Query must be at least 2 characters" });

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var results = await mediator.Send(new SearchBggGamesQuery(searchTerm), cancellationToken).ConfigureAwait(false);
            var total = results.Count;
            var paged = results.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Results.Ok(new
            {
                results = paged,
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        })
        .RequireAdminSession()
        .RequireRateLimiting("BggSearch")
        .WithName("BggAdminSearch")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Search BoardGameGeek catalog (Admin only)";
            operation.Description = "Admin-only search for board games on BoardGameGeek. Restricted due to BGG commercial use licensing. Rate limited to 60 searches per hour per user.";
            return operation;
        });

        // GET /api/v1/bgg/games/{bggId}
        group.MapGet("/bgg/games/{bggId:int}", async (
            int bggId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            if (bggId <= 0)
                return Results.BadRequest(new { error = "Invalid BGG ID" });

            var details = await mediator.Send(new GetBggGameDetailsQuery(bggId), cancellationToken).ConfigureAwait(false);

            if (details == null)
                return Results.NotFound(new { error = $"BGG game {bggId} not found" });

            return Results.Ok(details);
        })
        .RequireAdminSession()
        .RequireRateLimiting("BggSearch")
        .WithName("GetBggGameDetails")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Get BoardGameGeek game details (Admin only)";
            operation.Description = "Admin-only retrieval of detailed information about a specific board game from BoardGameGeek by its BGG ID. Restricted due to BGG commercial use licensing. Rate limited to 60 requests per hour per user.";
            return operation;
        });

        return group;
    }
}
