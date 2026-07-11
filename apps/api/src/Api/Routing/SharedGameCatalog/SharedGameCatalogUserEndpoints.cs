using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameDocumentsForUser;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Authenticated user-facing SharedGameCatalog endpoints.
/// </summary>
internal static class SharedGameCatalogUserEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        // Get active documents for a shared game (authenticated users with access)
        group.MapGet("/shared-games/{gameId:guid}/documents", HandleGetGameDocumentsForUser)
            .RequireAuthorization()
            .WithName("GetSharedGameDocumentsForUser")
            .WithSummary("Get active documents for a shared game (Authenticated)")
            .WithDescription("Returns active documents for a game. Access requires the game to be RAG-public or the user to have the game in their library.")
            .Produces<IReadOnlyList<SharedGameDocumentDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // Get the active published mechanic card for a game (#528 ME-M1.6, login-gated).
        group.MapGet("/games/{gameId:guid}/card", HandleGetPublishedMechanicCard)
            .RequireAuthorization()
            .WithName("GetPublishedMechanicCard")
            .WithSummary("Get the published mechanic card for a game (Authenticated)")
            .WithDescription("Returns the active (non-suppressed) published mechanic card for a game. 404 when no card is published or the card was suppressed/taken down.")
            .Produces<PublishedMechanicCardDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleGetPublishedMechanicCard(
        Guid gameId,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetPublishedMechanicCardByGameQuery(gameId), ct).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> HandleGetGameDocumentsForUser(
        Guid gameId,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var query = new GetGameDocumentsForUserQuery(gameId, userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }
}
