using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Card deck management endpoints (Issue #3343): create deck, shuffle, draw, discard, get decks/hand/discard-pile.
/// </summary>
internal static class SessionCardDeckEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapCreateDeckEndpoint(group);
        MapShuffleDeckEndpoint(group);
        MapDrawCardsEndpoint(group);
        MapDiscardCardsEndpoint(group);
        MapGetSessionDecksEndpoint(group);
        MapGetPlayerHandEndpoint(group);
        MapGetDiscardPileEndpoint(group);
    }

    private static void MapCreateDeckEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/decks", async (
            Guid sessionId,
            CreateDeckCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/game-sessions/{sessionId}/decks/{result.DeckId}", result);
        })
        .RequireAuthenticatedUser()
        .WithName("CreateDeck")
        .WithTags("SessionTracking", "CardDeck")
        .WithSummary("Create a new deck in the session")
        .Produces(201)
        .Produces(400)
        .Produces(401);
    }

    private static void MapShuffleDeckEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/decks/{deckId:guid}/shuffle", async (
            Guid sessionId,
            Guid deckId,
            ShuffleDeckCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId || deckId != command.DeckId)
            {
                return Results.BadRequest(new { error = "Session or Deck ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("ShuffleDeck")
        .WithTags("SessionTracking", "CardDeck")
        .WithSummary("Shuffle the deck")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(404);
    }

    private static void MapDrawCardsEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/decks/{deckId:guid}/draw", async (
            Guid sessionId,
            Guid deckId,
            DrawCardsCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId || deckId != command.DeckId)
            {
                return Results.BadRequest(new { error = "Session or Deck ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("DrawSessionCards")
        .WithTags("SessionTracking", "CardDeck")
        .WithSummary("Draw cards from the deck")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(404);
    }

    private static void MapDiscardCardsEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/decks/{deckId:guid}/discard", async (
            Guid sessionId,
            Guid deckId,
            DiscardCardsCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId || deckId != command.DeckId)
            {
                return Results.BadRequest(new { error = "Session or Deck ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("DiscardCards")
        .WithTags("SessionTracking", "CardDeck")
        .WithSummary("Discard cards from hand")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(404);
    }

    private static void MapGetSessionDecksEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/decks", async (
            Guid sessionId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetSessionDecksQuery { SessionId = sessionId };
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetSessionDecks")
        .WithTags("SessionTracking", "CardDeck")
        .WithSummary("Get all decks in the session")
        .Produces(200)
        .Produces(401);
    }

    private static void MapGetPlayerHandEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/decks/{deckId:guid}/hand/{participantId:guid}", async (
            Guid sessionId,
            Guid deckId,
            Guid participantId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetPlayerHandQuery
            {
                SessionId = sessionId,
                DeckId = deckId,
                ParticipantId = participantId
            };
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetPlayerHand")
        .WithTags("SessionTracking", "CardDeck")
        .WithSummary("Get a player's hand (only visible to the player)")
        .Produces(200)
        .Produces(401)
        .Produces(404);
    }

    private static void MapGetDiscardPileEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/decks/{deckId:guid}/discard", async (
            Guid sessionId,
            Guid deckId,
            IMediator mediator,
            int limit = 10,
            CancellationToken ct = default) =>
        {
            var query = new GetDiscardPileQuery
            {
                SessionId = sessionId,
                DeckId = deckId,
                Limit = limit
            };
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetDiscardPile")
        .WithTags("SessionTracking", "CardDeck")
        .WithSummary("Get the discard pile (visible to all)")
        .Produces(200)
        .Produces(401)
        .Produces(404);
    }
}
