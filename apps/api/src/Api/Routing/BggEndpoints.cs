using Api.Infrastructure.ExternalServices.BoardGameGeek;
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
        // GET /api/v1/bgg/search?query={term}
        group.MapGet("/search", async (
            string query,
            IBggApiClient bggClient,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Results.BadRequest(new { error = "Query must be at least 2 characters" });

            var results = await bggClient.SearchGamesAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(results);
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
        group.MapGet("/games/{bggId:int}", async (
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
