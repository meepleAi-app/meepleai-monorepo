using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
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
        MapUpdateSessionScoresEndpoint(group); // T10 #1896 — polymorphic
        MapAddParticipantEndpoint(group);
        MapAddNoteEndpoint(group);
        MapFinalizeSessionEndpoint(group);
        MapRollDiceEndpoint(group);
    }

    /// <summary>
    /// Asse A semantic alignment #1896 (T10, DEC-1): request DTO for the polymorphic
    /// scores update endpoint. Distinct from <see cref="UpdateScoreCommand"/> (per-participant
    /// score entry) — this carries the polymorphic <c>ScoringType</c> + <c>ScoreData</c>
    /// JSON payload validated by per-type IScoringStrategy.
    /// </summary>
    public record UpdateSessionScoresRequest(ScoreType ScoringType, string ScoreData);

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

    /// <summary>
    /// Asse A semantic alignment #1896 (T10, DEC-1): polymorphic mid-game score update
    /// endpoint. Mounted at <c>/game-sessions/{id}/scores-polymorphic</c> to avoid clashing
    /// with the legacy <see cref="UpdateScoreCommand"/> endpoint at
    /// <c>/game-sessions/{id}/scores</c> (per-participant score entry).
    ///
    /// <para>Payload: <c>{ scoringType: Points|BinaryWin|Objectives|Ranking, scoreData: "JSON string" }</c>.
    /// FluentValidation enforces per-type JSON schema via <c>ScoringStrategyFactory</c>.
    /// Returns the computed winner playerId when a single winner can be determined.</para>
    /// </summary>
    private static void MapUpdateSessionScoresEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/game-sessions/{sessionId:guid}/scores-polymorphic", async (
            Guid sessionId,
            UpdateSessionScoresRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // IDOR guard (security review high-priority): extract authenticated user id
            // from claims; handler verifies the caller is the session owner or a participant.
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var command = new UpdateSessionScoresCommand(sessionId, request.ScoringType, request.ScoreData, userId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("UpdateSessionScoresPolymorphic")
        .WithTags("SessionTracking")
        .WithSummary("Update session polymorphic scores (Points/BinaryWin/Objectives/Ranking)")
        .WithDescription("Asse A #1896 (T10): replaces ScoringType + ScoreData atomically with per-type JSON schema validation. Caller must be session owner or registered participant (IDOR-protected).")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
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
            HttpResponse response,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            // Invariante #13 (#1896 WP2 T4): non-blocking warning header for FE toast when
            // a draft+live session coexist in the same GameNightEvent. The operation itself
            // succeeded (200 OK); the header is purely informational.
            if (result.LiveActiveWarning)
            {
                response.Headers.Append("X-Warning-Code", "SAVED_WHILE_LIVE_ACTIVE");
            }

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
