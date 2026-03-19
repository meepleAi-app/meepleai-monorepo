using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Core session command endpoints: create, update score, add participant, add note, finalize, roll dice.
/// </summary>
internal static class SessionCommandEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapCreateSessionEndpoint(group);
        MapUpdateScoreEndpoint(group);
        MapAddParticipantEndpoint(group);
        MapAddNoteEndpoint(group);
        MapFinalizeSessionEndpoint(group);
        MapRollDiceEndpoint(group);
    }

    private static void MapCreateSessionEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions", async (
            CreateSessionCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/game-sessions/{result.SessionId}", result);
        })
        .RequireAuthenticatedUser()
        .WithName("CreateSession")
        .WithTags("SessionTracking")
        .WithSummary("Create a new collaborative game session")
        .Produces(201)
        .Produces(400)
        .Produces(401);
    }

    private static void MapUpdateScoreEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/game-sessions/{sessionId:guid}/scores", async (
            Guid sessionId,
            UpdateScoreCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Ensure route parameter matches command
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("UpdateScore")
        .WithTags("SessionTracking")
        .WithSummary("Update participant score")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(404);
    }

    private static void MapAddParticipantEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/participants", async (
            Guid sessionId,
            AddParticipantCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/sessions/{sessionId}/participants/{result.ParticipantId}", result);
        })
        .RequireAuthenticatedUser()
        .WithName("AddParticipant")
        .WithTags("SessionTracking")
        .WithSummary("Add participant to session")
        .Produces(201)
        .Produces(400)
        .Produces(401)
        .Produces(404);
    }

    private static void MapAddNoteEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/notes", async (
            Guid sessionId,
            AddNoteCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/sessions/{sessionId}/notes/{result.NoteId}", result);
        })
        .RequireAuthenticatedUser()
        .WithName("AddNote")
        .WithTags("SessionTracking")
        .WithSummary("Add note to session")
        .Produces(201)
        .Produces(400)
        .Produces(401)
        .Produces(404);
    }

    private static void MapFinalizeSessionEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/finalize", async (
            Guid sessionId,
            FinalizeSessionCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("FinalizeSession")
        .WithTags("SessionTracking")
        .WithSummary("Finalize session with final rankings")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(404);
    }

    private static void MapRollDiceEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/dice", async (
            Guid sessionId,
            RollDiceCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/game-sessions/{sessionId}/dice/{result.DiceRollId}", result);
        })
        .RequireAuthenticatedUser()
        .WithName("RollDice")
        .WithTags("SessionTracking", "Dice")
        .WithSummary("Roll dice in a session")
        .WithDescription("Rolls dice using standard formulas (e.g., 2d6+3, 1d20-2). Broadcasts result via SSE.")
        .Produces(201)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .Produces(409);
    }
}
