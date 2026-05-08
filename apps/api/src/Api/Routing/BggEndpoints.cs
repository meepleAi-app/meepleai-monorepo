using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Extensions;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Routing;

/// <summary>
/// BoardGameGeek API integration endpoints.
/// Issue #3120: Originally provided admin-only search and game details lookup from BGG.
/// Issue #805 (Wave 3 Phase 0): Endpoints now require any authenticated user (not just admin)
/// so SP6 wizard step 1 BGG tab + future user-facing flows can consume the catalog.
/// BGG data is publicly available; the per-user rate limiter (BggSearch policy:
/// 60 req/hour/user) preserves the BGG external-API quota and prevents abuse.
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
        .RequireAuthenticatedUser()
        .RequireRateLimiting("BggSearch")
        .WithName("BggSearch")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Search BoardGameGeek catalog";
            operation.Description = "Search for board games on BoardGameGeek. Requires authentication. Rate limited to 60 searches per hour per user (per-user sliding window).";
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
        .RequireAuthenticatedUser()
        .RequireRateLimiting("BggSearch")
        .WithName("GetBggGameDetails")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Get BoardGameGeek game details";
            operation.Description = "Retrieve detailed information about a specific board game from BoardGameGeek by its BGG ID. Requires authentication. Rate limited to 60 requests per hour per user (shared with /bgg/search).";
            return operation;
        });

        return group;
    }
}
