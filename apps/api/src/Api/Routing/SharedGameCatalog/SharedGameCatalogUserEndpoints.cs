using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
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

        // Submit 👍/👎 feedback on a single claim of a published card (#533 ME-M3.1).
        group.MapPost("/mechanic-cards/{cardId:guid}/feedback", HandleSubmitCardFeedback)
            .RequireAuthorization()
            .WithName("SubmitMechanicCardFeedback")
            .WithSummary("Submit feedback on a mechanic card claim (Authenticated)")
            .WithDescription("Records the user's up/down feedback on a claim. Idempotent per (card, user, claim). 201 on create, 200 on update, 404 when the card is missing/suppressed, 429 when the per-day cap is hit.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);
    }

    private static async Task<IResult> HandleSubmitCardFeedback(
        Guid cardId,
        SubmitMechanicCardFeedbackRequest body,
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

        var command = new SubmitMechanicCardFeedbackCommand(
            cardId, userId, body.ClaimId, body.IsPositive, body.ErrorType, body.Description, body.SuggestedCitation);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        return result.Outcome switch
        {
            SubmitFeedbackOutcome.Created => Results.StatusCode(StatusCodes.Status201Created),
            SubmitFeedbackOutcome.Updated => Results.Ok(),
            SubmitFeedbackOutcome.CardNotFound => Results.NotFound(),
            SubmitFeedbackOutcome.RateLimited => Results.StatusCode(StatusCodes.Status429TooManyRequests),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
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
