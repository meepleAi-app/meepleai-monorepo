using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Session Flow v2.1 — T9 routing.
/// Exposes the HTTP surface for the "1-click start → play → pause/resume → diary"
/// session flow:
/// <list type="bullet">
///   <item><c>GET  /games/{gameId}/kb-readiness</c> — pre-session KB probe (T3)</item>
///   <item><c>POST /sessions/{sessionId}/pause</c> — pause an active session (T5)</item>
///   <item><c>POST /sessions/{sessionId}/resume</c> — resume a paused session (T5)</item>
///   <item><c>PUT  /sessions/{sessionId}/turn-order</c> — manual or seeded-random turn order (T6)</item>
///   <item><c>POST /sessions/{sessionId}/scores-with-diary</c> — upsert score with diary (T8)</item>
///   <item><c>GET  /sessions/{sessionId}/diary</c> — read single-session diary (T9)</item>
///   <item><c>GET  /game-nights/{gameNightId}/diary</c> — read cross-session night diary (T9)</item>
/// </list>
/// <para>
/// The score endpoint uses <c>/scores-with-diary</c> to avoid colliding with the
/// pre-existing <c>PUT /game-sessions/{id}/scores</c> route (GST-003) which wires
/// the legacy <see cref="UpdateScoreCommand"/>. Session Flow v2.1 dispatches the
/// diary-aware <see cref="UpsertScoreWithDiaryCommand"/>.
/// </para>
/// </summary>
internal static class SessionFlowEndpoints
{
    public static void MapSessionFlowEndpoints(this IEndpointRouteBuilder app)
    {
        // KB readiness probe (read)
        app.MapGet("/games/{gameId:guid}/kb-readiness", async (
            Guid gameId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetKbReadinessQuery(gameId), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("SessionFlow_GetKbReadiness")
        .WithTags("SessionFlow")
        .WithSummary("Probe whether the Knowledge Base for a game is ready to power an agent.")
        .Produces(200)
        .Produces(401);

        // Pause session
        app.MapPost("/sessions/{sessionId:guid}/pause", async (
            Guid sessionId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            await mediator.Send(new PauseSessionCommand(sessionId, userId), ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthenticatedUser()
        .WithName("SessionFlow_PauseSession")
        .WithTags("SessionFlow")
        .WithSummary("Pause an active session.")
        .Produces(204)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409);

        // Resume session
        app.MapPost("/sessions/{sessionId:guid}/resume", async (
            Guid sessionId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            await mediator.Send(new ResumeSessionCommand(sessionId, userId), ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthenticatedUser()
        .WithName("SessionFlow_ResumeSession")
        .WithTags("SessionFlow")
        .WithSummary("Resume a paused session (auto-pausing any other active session in the same night).")
        .Produces(204)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409);

        // Set turn order
        app.MapPut("/sessions/{sessionId:guid}/turn-order", async (
            Guid sessionId,
            SetTurnOrderRequestBody body,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            if (body is null || string.IsNullOrWhiteSpace(body.Method))
            {
                return Results.BadRequest(new { error = "Method is required." });
            }

            if (!Enum.TryParse<TurnOrderMethod>(body.Method, ignoreCase: true, out var method))
            {
                return Results.BadRequest(new { error = $"Unknown turn order method: {body.Method}" });
            }

            var result = await mediator
                .Send(new SetTurnOrderCommand(sessionId, userId, method, body.Order), ct)
                .ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("SessionFlow_SetTurnOrder")
        .WithTags("SessionFlow")
        .WithSummary("Set the turn order for a session (manual list or cryptographic shuffle).")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404);

        // Advance turn (Plan 1bis T1)
        app.MapPost("/sessions/{sessionId:guid}/turn/advance", async (
            Guid sessionId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var result = await mediator
                .Send(new AdvanceTurnCommand(sessionId, userId), ct)
                .ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("SessionFlow_AdvanceTurn")
        .WithTags("SessionFlow")
        .WithSummary("Advance the turn index to the next participant (cyclic) and emit a turn_advanced diary event.")
        .Produces(200)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409);

        // Upsert score with diary
        app.MapPost("/sessions/{sessionId:guid}/scores-with-diary", async (
            Guid sessionId,
            ScoreUpsertRequestBody body,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            if (body is null)
            {
                return Results.BadRequest(new { error = "Request body is required." });
            }

            var result = await mediator.Send(
                new UpsertScoreWithDiaryCommand(
                    SessionId: sessionId,
                    ParticipantId: body.ParticipantId,
                    RequesterId: userId,
                    NewValue: body.NewValue,
                    RoundNumber: body.RoundNumber,
                    Category: body.Category,
                    Reason: body.Reason),
                ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("SessionFlow_UpsertScoreWithDiary")
        .WithTags("SessionFlow")
        .WithSummary("Upsert a participant score and emit a score_updated diary event.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404);

        // Session diary (read)
        app.MapGet("/sessions/{sessionId:guid}/diary", async (
            Guid sessionId,
            IMediator mediator,
            CancellationToken ct,
            string? eventTypes = null,
            DateTime? since = null,
            int? limit = null) =>
        {
            var types = string.IsNullOrWhiteSpace(eventTypes)
                ? null
                : eventTypes
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToArray();

            var result = await mediator
                .Send(new GetSessionDiaryQuery(sessionId, types, since, limit ?? 100), ct)
                .ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("SessionFlow_GetSessionDiary")
        .WithTags("SessionFlow")
        .WithSummary("Read the append-only diary for a single session (chronological).")
        .Produces(200)
        .Produces(401);

        // GameNight diary (read)
        app.MapGet("/game-nights/{gameNightId:guid}/diary", async (
            Guid gameNightId,
            IMediator mediator,
            CancellationToken ct,
            string? eventTypes = null,
            DateTime? since = null,
            int? limit = null) =>
        {
            var types = string.IsNullOrWhiteSpace(eventTypes)
                ? null
                : eventTypes
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToArray();

            var result = await mediator
                .Send(new GetGameNightDiaryQuery(gameNightId, types, since, limit ?? 500), ct)
                .ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("SessionFlow_GetGameNightDiary")
        .WithTags("SessionFlow")
        .WithSummary("Read the append-only diary for a whole game night (unions all attached sessions).")
        .Produces(200)
        .Produces(401);

        // Complete game night (cascade finalize all sessions)
        app.MapPost("/game-nights/{gameNightId:guid}/complete", async (
            Guid gameNightId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var result = await mediator
                .Send(new CompleteGameNightCommand(gameNightId, userId), ct)
                .ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("SessionFlow_CompleteGameNight")
        .WithTags("SessionFlow")
        .WithSummary("Complete an ad-hoc game night: cascade-finalize all sessions and emit diary events.")
        .Produces(200)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409);
    }

    /// <summary>
    /// HTTP body for <c>PUT /sessions/{sessionId}/turn-order</c>.
    /// </summary>
    public sealed record SetTurnOrderRequestBody(string Method, IReadOnlyList<Guid>? Order);

    /// <summary>
    /// HTTP body for <c>POST /sessions/{sessionId}/scores-with-diary</c>.
    /// </summary>
    public sealed record ScoreUpsertRequestBody(
        Guid ParticipantId,
        decimal NewValue,
        int? RoundNumber,
        string? Category,
        string? Reason);
}
