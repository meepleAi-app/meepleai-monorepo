using Api.Infrastructure.ExternalServices.BoardGameGeek;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Routing;

/// <summary>
/// BoardGameGeek API integration endpoints.
/// Issue #3120: Provides public search and game details lookup from BGG.
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
            IBggApiClient bggClient,
            CancellationToken cancellationToken) =>
        {
            var searchTerm = q ?? query;
            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
                return Results.BadRequest(new { error = "Query must be at least 2 characters" });

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var results = await bggClient.SearchGamesAsync(searchTerm, cancellationToken).ConfigureAwait(false);
            var resultsList = results.ToList();
            var total = resultsList.Count;
            var paged = resultsList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Results.Ok(new
            {
                results = paged,
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        })
        .RequireRateLimiting("BggSearch")
        .WithName("BggPublicSearch")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Search BoardGameGeek catalog";
            operation.Description = "Public search for board games on BoardGameGeek. Rate limited to 20 searches per hour per user.";
            return operation;
        });

        // GET /api/v1/bgg/games/{bggId}
        group.MapGet("/bgg/games/{bggId:int}", async (
            int bggId,
            IBggApiClient bggClient,
            CancellationToken cancellationToken) =>
        {
            if (bggId <= 0)
                return Results.BadRequest(new { error = "Invalid BGG ID" });

            var details = await bggClient.GetGameDetailsAsync(bggId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(details);
        })
        .RequireRateLimiting("BggSearch")
        .WithName("GetBggGameDetails")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Get BoardGameGeek game details";
            operation.Description = "Retrieve detailed information about a specific board game from BoardGameGeek by its BGG ID. Rate limited to 20 requests per hour per user.";
            return operation;
        });

        return group;
    }
}
