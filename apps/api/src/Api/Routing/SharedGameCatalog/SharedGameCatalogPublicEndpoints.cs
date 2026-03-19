using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Public (unauthenticated) SharedGameCatalog endpoints for game search and browsing.
/// </summary>
internal static class SharedGameCatalogPublicEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        // Search shared games with filtering and full-text search
        group.MapGet("/shared-games", HandleSearchGames)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("SearchSharedGames")
            .WithSummary("Search shared games catalog")
            .WithDescription("Search games with full-text search, category/mechanic filters, player count, and playing time filters. Returns published games only for public access.")
            .Produces<PagedResult<SharedGameDto>>();

        // Get game details by ID
        group.MapGet("/shared-games/{id:guid}", HandleGetGameById)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetSharedGameById")
            .WithSummary("Get shared game details")
            .WithDescription("Get detailed information about a shared game including designers, publishers, categories, mechanics, FAQs, and errata. Returns only published games for public access.")
            .Produces<SharedGameDetailDto>()
            .Produces(StatusCodes.Status404NotFound);

        // Get all game categories
        group.MapGet("/shared-games/categories", HandleGetCategories)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetGameCategories")
            .WithSummary("Get all game categories")
            .WithDescription("Returns all available game categories for filtering.")
            .Produces<List<GameCategoryDto>>();

        // Get all game mechanics
        group.MapGet("/shared-games/mechanics", HandleGetMechanics)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetGameMechanics")
            .WithSummary("Get all game mechanics")
            .WithDescription("Returns all available game mechanics for filtering.")
            .Produces<List<GameMechanicDto>>();

        // Get FAQs for a game with pagination - Issue #2681
        group.MapGet("/games/{gameId:guid}/faqs", HandleGetGameFaqs)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetGameFaqs")
            .WithSummary("Get FAQs for a game")
            .WithDescription("Returns FAQs for a published game with pagination. Ordered by display order then by upvote count (descending).")
            .Produces<GetGameFaqsResultDto>()
            .Produces(StatusCodes.Status404NotFound);

        // Upvote a FAQ - Issue #2681
        group.MapPost("/faqs/{faqId:guid}/upvote", HandleUpvoteFaq)
            .AllowAnonymous()
            .RequireRateLimiting("FaqUpvote")
            .WithName("UpvoteFaq")
            .WithSummary("Upvote a FAQ")
            .WithDescription("Increments the upvote count for a FAQ. Rate limited to prevent abuse.")
            .Produces<UpvoteFaqResultDto>()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleSearchGames(
        IMediator mediator,
        [FromQuery] string? search,
        [FromQuery] Guid[]? categoryIds,
        [FromQuery] Guid[]? mechanicIds,
        [FromQuery] int? minPlayers,
        [FromQuery] int? maxPlayers,
        [FromQuery] int? maxPlayingTime,
        [FromQuery] decimal? minComplexity,
        [FromQuery] decimal? maxComplexity,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "Title",
        [FromQuery] bool sortDescending = false,
        CancellationToken ct = default)
    {
        var query = new SearchSharedGamesQuery(
            search,
            categoryIds?.ToList(),
            mechanicIds?.ToList(),
            minPlayers,
            maxPlayers,
            maxPlayingTime,
            minComplexity,
            maxComplexity,
            Status: null, // Public always gets Published only (filtered in handler)
            pageNumber,
            pageSize,
            sortBy,
            sortDescending);

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetGameById(
        IMediator mediator,
        HttpContext context,
        Guid id,
        CancellationToken ct)
    {
        var query = new GetSharedGameByIdQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        // Public access: only return Published games
        // Admin/Editor can see all statuses
        if (result is not null)
        {
            var isAdminOrEditor = context.User.IsInRole("Admin") || context.User.IsInRole("Editor");
            if (!isAdminOrEditor && result.Status != GameStatus.Published)
            {
                return Results.NotFound(); // Hide draft/archived games from public
            }
            return Results.Ok(result);
        }

        return Results.NotFound();
    }

    private static async Task<IResult> HandleGetCategories(
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetGameCategoriesQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetMechanics(
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetGameMechanicsQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetGameFaqs(
        Guid gameId,
        IMediator mediator,
        [FromQuery] int limit = 10,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var query = new GetGameFaqsQuery(gameId, limit, offset);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUpvoteFaq(
        Guid faqId,
        IMediator mediator,
        CancellationToken ct = default)
    {
        try
        {
            var command = new UpvoteFaqCommand(faqId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }
}
