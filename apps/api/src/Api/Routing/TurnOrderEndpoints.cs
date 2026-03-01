using Api.BoundedContexts.GameManagement.Application.Commands.TurnOrder;
using Api.BoundedContexts.GameManagement.Application.DTOs.TurnOrder;
using Api.BoundedContexts.GameManagement.Application.Queries.TurnOrder;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Endpoints for TurnOrder management within live sessions.
/// Part of the base toolkit — always available regardless of custom toolkit.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal static class TurnOrderEndpoints
{
    public static RouteGroupBuilder MapTurnOrderEndpoints(this RouteGroupBuilder group)
    {
        // === Commands ===

        group.MapPost("/live-sessions/{sessionId}/turn-order/initialize", HandleInitialize)
            .RequireAuthenticatedUser()
            .Produces<TurnOrderDto>(201)
            .Produces(400)
            .Produces(404)
            .Produces(409)
            .WithTags("TurnOrder")
            .WithSummary("Initialize turn order for a session")
            .WithDescription("Creates a TurnOrder with the given player list. Fails if already initialized. Issue #4970.");

        group.MapPost("/live-sessions/{sessionId}/turn-order/advance", HandleAdvance)
            .RequireAuthenticatedUser()
            .Produces<TurnOrderDto>()
            .Produces(404)
            .WithTags("TurnOrder")
            .WithSummary("Advance to the next player")
            .WithDescription("Moves to the next player. Wraps around and increments the round when past the last player. Broadcasts turn:advanced SSE event.");

        group.MapPut("/live-sessions/{sessionId}/turn-order/reorder", HandleReorder)
            .RequireAuthenticatedUser()
            .Produces<TurnOrderDto>()
            .Produces(400)
            .Produces(404)
            .WithTags("TurnOrder")
            .WithSummary("Reorder players")
            .WithDescription("Replaces the player order with a new ordered list.");

        group.MapPost("/live-sessions/{sessionId}/turn-order/reset", HandleReset)
            .RequireAuthenticatedUser()
            .Produces<TurnOrderDto>()
            .Produces(404)
            .WithTags("TurnOrder")
            .WithSummary("Reset turn order")
            .WithDescription("Resets to round 1, first player.");

        // === Queries ===

        group.MapGet("/live-sessions/{sessionId}/turn-order", HandleGet)
            .RequireAuthenticatedUser()
            .Produces<TurnOrderDto>()
            .Produces(404)
            .WithTags("TurnOrder")
            .WithSummary("Get current turn order state")
            .WithDescription("Returns the current TurnOrder state including current player and round number.");

        return group;
    }

    private static async Task<IResult> HandleInitialize(
        Guid sessionId, InitializeTurnOrderRequest request, IMediator mediator, CancellationToken cancellationToken)
    {
        var command = new InitializeTurnOrderCommand(sessionId, request.PlayerOrder);
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/live-sessions/{sessionId}/turn-order", result);
    }

    private static async Task<IResult> HandleAdvance(
        Guid sessionId, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AdvanceTurnCommand(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleReorder(
        Guid sessionId, ReorderPlayersRequest request, IMediator mediator, CancellationToken cancellationToken)
    {
        var command = new ReorderPlayersCommand(sessionId, request.PlayerOrder);
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleReset(
        Guid sessionId, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ResetTurnOrderCommand(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGet(
        Guid sessionId, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTurnOrderQuery(sessionId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

/// <summary>Request body for initializing turn order.</summary>
internal sealed record InitializeTurnOrderRequest(IReadOnlyList<string> PlayerOrder);

/// <summary>Request body for reordering players.</summary>
internal sealed record ReorderPlayersRequest(IReadOnlyList<string> PlayerOrder);
