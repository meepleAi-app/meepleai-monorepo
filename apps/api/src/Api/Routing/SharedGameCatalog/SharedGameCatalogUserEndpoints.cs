using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameDocumentsForUser;
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
