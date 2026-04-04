using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Player action endpoints (Issue #4765), session join + role management (Issue #4766),
/// export/share write operations (Issue #3347), invite write operations (Issue #3354),
/// media write operations (Issue #4760), chat write operations (Issue #4760),
/// toolkit state write (Issue #5148), diary write (Issue #276),
/// AI turn summary (Issue #277), and checkpoint write operations (Issue #278).
/// </summary>
internal static class SessionPlayerActionsEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        // Player action endpoints (Issue #4765)
        MapMarkPlayerReadyEndpoint(group);
        MapKickParticipantEndpoint(group);
        MapUpdatePlayerScoreEndpoint(group);
        MapRollSessionDiceEndpoint(group);
        MapDrawSessionCardEndpoint(group);
        MapSessionTimerActionEndpoint(group);
        MapSendChatActionEndpoint(group);

        // Session join + role management endpoints (Issue #4766)
        MapJoinSessionByCodeEndpoint(group);
        MapAssignParticipantRoleEndpoint(group);

        // Session export and sharing write endpoints (Issue #3347)
        MapGenerateShareLinkEndpoint(group);

        // Session invite link write endpoints (Issue #3354)
        MapGenerateInviteTokenEndpoint(group);
        MapJoinSessionByInviteEndpoint(group);

        // Session media write endpoints (Issue #4760)
        MapUploadSessionMediaEndpoint(group);
        MapUpdateMediaCaptionEndpoint(group);
        MapDeleteSessionMediaEndpoint(group);

        // Session chat write endpoints (Issue #4760)
        MapSendSessionChatMessageEndpoint(group);
        MapAskSessionAgentEndpoint(group);
        MapDeleteChatMessageEndpoint(group);

        // Toolkit session state write endpoints (Issue #5148 — Epic B5)
        MapUpdateToolkitWidgetStateEndpoint(group);

        // Session diary write endpoints (Issue #276)
        MapAddSessionEventEndpoint(group);

        // AI-powered turn summary (Issue #277)
        MapGetTurnSummaryEndpoint(group);

        // Session checkpoint write endpoints (Issue #278)
        MapCreateCheckpointEndpoint(group);
        MapRestoreCheckpointEndpoint(group);
    }

    // ========== Player Actions (Issue #4765) ==========

    private static void MapMarkPlayerReadyEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/players/{participantId:guid}/ready", async (
            Guid sessionId,
            Guid participantId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var command = new MarkPlayerReadyCommand(sessionId, participantId, userId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("MarkPlayerReady")
        .WithTags("SessionTracking", "PlayerActions")
        .WithSummary("Mark a player as ready")
        .WithDescription("Marks the specified participant as ready for the next phase/turn. Requires Player role.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409);
    }

    private static void MapKickParticipantEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/game-sessions/{sessionId:guid}/players/{participantId:guid}/kick", async (
            Guid sessionId,
            Guid participantId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var command = new KickParticipantCommand(sessionId, participantId, userId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("KickParticipant")
        .WithTags("SessionTracking", "PlayerActions")
        .WithSummary("Kick a participant from the session")
        .WithDescription("Removes a participant from the session. Host-only action. Cannot kick the host.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409);
    }

    private static void MapUpdatePlayerScoreEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/actions/score", async (
            Guid sessionId,
            UpdatePlayerScoreRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var command = new UpdatePlayerScoreCommand(
                sessionId,
                request.ParticipantId,
                userId,
                request.ScoreValue,
                request.RoundNumber,
                request.Category);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("UpdatePlayerScore")
        .WithTags("SessionTracking", "PlayerActions")
        .WithSummary("Update player score")
        .WithDescription("Submits a score delta for the specified participant. Requires Player role. Handles optimistic concurrency conflicts.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409);
    }

    private static void MapRollSessionDiceEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/actions/roll-dice", async (
            Guid sessionId,
            RollSessionDiceRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var command = new RollSessionDiceCommand(sessionId, request.ParticipantId, userId, request.Formula, request.Label);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("RollSessionDice")
        .WithTags("SessionTracking", "PlayerActions")
        .WithSummary("Roll dice")
        .WithDescription("Rolls dice using the given formula. Requires Player role. Result is broadcast to all participants.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409);
    }

    private static void MapDrawSessionCardEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/actions/draw-card", async (
            Guid sessionId,
            DrawSessionCardRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var command = new DrawSessionCardCommand(sessionId, request.DeckId, request.ParticipantId, userId, request.Count);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("DrawSessionCard")
        .WithTags("SessionTracking", "PlayerActions")
        .WithSummary("Draw cards from a deck")
        .WithDescription("Draws one or more cards from a session deck. Requires Player role.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409);
    }

    private static void MapSessionTimerActionEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/actions/timer", async (
            Guid sessionId,
            SessionTimerActionRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var command = new SessionTimerActionCommand(
                sessionId,
                request.ParticipantId ?? userId,
                userId,
                request.Action,
                request.ParticipantName ?? string.Empty,
                request.DurationSeconds ?? 60);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("SessionTimerAction")
        .WithTags("SessionTracking", "PlayerActions")
        .WithSummary("Control session timer")
        .WithDescription("Start, pause, resume, or reset the session countdown timer. Requires Player role.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(409);
    }

    private static void MapSendChatActionEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/actions/chat", async (
            Guid sessionId,
            SendChatActionRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var command = new SendChatActionCommand(
                sessionId,
                userId,
                userId,
                request.Content,
                request.TurnNumber,
                request.MentionsJson);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("SendChatAction")
        .WithTags("SessionTracking", "PlayerActions")
        .WithSummary("Send a chat message")
        .WithDescription("Sends a chat message. Available to all participants including spectators.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    // ========== Session Join + Role Management (Issue #4766) ==========

    private static void MapJoinSessionByCodeEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/code/{code}/join", async (
            string code,
            JoinSessionByCodeRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var command = new JoinSessionByCodeCommand(code, userId, request.DisplayName);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("JoinSessionByCode")
        .WithTags("SessionTracking", "SessionJoin")
        .WithSummary("Join a session using session code")
        .WithDescription("Joins the session identified by the 6-character code. Creates participant with Player role.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .Produces(409);
    }

    private static void MapAssignParticipantRoleEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/game-sessions/{sessionId:guid}/participants/{participantId:guid}/role", async (
            Guid sessionId,
            Guid participantId,
            AssignParticipantRoleRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var command = new AssignParticipantRoleCommand(sessionId, participantId, request.NewRole, userId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("AssignParticipantRole")
        .WithTags("SessionTracking", "PlayerActions")
        .WithSummary("Assign role to participant")
        .WithDescription("Changes a participant's role. Host-only action. Cannot demote the last host.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409);
    }

    // ========== Export and Sharing Write Endpoints (Issue #3347) ==========

    private static void MapGenerateShareLinkEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/share-link", async (
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

            var query = new GenerateSessionShareLinkQuery(sessionId, userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GenerateShareLink")
        .WithTags("SessionTracking", "Share")
        .WithSummary("Generate shareable link with OG metadata")
        .WithDescription("Creates a shareable URL with Open Graph metadata for social media sharing.")
        .Produces(200)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    // ========== Invite Link Write Endpoints (Issue #3354) ==========

    private static void MapGenerateInviteTokenEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/generate-invite", async (
            Guid sessionId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct,
            int? expiresInHours = null) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var command = new GenerateInviteTokenCommand(sessionId, userId, expiresInHours);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GenerateInviteToken")
        .WithTags("SessionTracking", "Invite")
        .WithSummary("Generate invite link for session")
        .WithDescription("Generates an invite token and URL with optional QR code for sharing the session.")
        .Produces(200)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    private static void MapJoinSessionByInviteEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/invite/{inviteToken}/join", async (
            string inviteToken,
            JoinSessionByInviteRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var command = new JoinSessionByInviteCommand(
                inviteToken,
                userId,
                request.DisplayName);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("JoinSessionByInvite")
        .WithTags("SessionTracking", "Invite")
        .WithSummary("Join session using invite link")
        .WithDescription("Adds the authenticated user as a participant to the session using the invite token.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .Produces(409);
    }

    // ========== Media Write Endpoints (Issue #4760) ==========

    private static void MapUploadSessionMediaEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/media", async (
            Guid sessionId,
            UploadSessionMediaCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/game-sessions/{sessionId}/media/{result.MediaId}", result);
        })
        .RequireAuthenticatedUser()
        .WithName("UploadSessionMedia")
        .WithTags("SessionTracking", "Media")
        .WithSummary("Upload media to a session")
        .WithDescription("Attaches a media file (photo, screenshot, document) to the session. File must be pre-uploaded to blob storage.")
        .Produces(201)
        .Produces(400)
        .Produces(401)
        .Produces(404);
    }

    private static void MapUpdateMediaCaptionEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/game-sessions/{sessionId:guid}/media/{mediaId:guid}/caption", async (
            Guid sessionId,
            Guid mediaId,
            UpdateMediaCaptionCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (mediaId != command.MediaId)
            {
                return Results.BadRequest(new { error = "Media ID mismatch" });
            }

            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthenticatedUser()
        .WithName("UpdateMediaCaption")
        .WithTags("SessionTracking", "Media")
        .WithSummary("Update media caption")
        .Produces(204)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    private static void MapDeleteSessionMediaEndpoint(RouteGroupBuilder group)
    {
        // Note: participantId comes from query string. Future auth refactor should extract from HttpContext.User.
        group.MapDelete("/game-sessions/{sessionId:guid}/media/{mediaId:guid}", async (
            Guid sessionId,
            Guid mediaId,
            Guid participantId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new DeleteSessionMediaCommand(mediaId, participantId);
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthenticatedUser()
        .WithName("DeleteSessionMedia")
        .WithTags("SessionTracking", "Media")
        .WithSummary("Delete session media")
        .WithDescription("Soft-deletes a media file. Only the owner can delete.")
        .Produces(204)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    // ========== Chat Write Endpoints (Issue #4760) ==========

    private static void MapSendSessionChatMessageEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/chat", async (
            Guid sessionId,
            SendSessionChatMessageCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/game-sessions/{sessionId}/chat/{result.MessageId}", result);
        })
        .RequireAuthenticatedUser()
        .WithName("SendSessionChatMessage")
        .WithTags("SessionTracking", "Chat")
        .WithSummary("Send a chat message in the session")
        .Produces(201)
        .Produces(400)
        .Produces(401)
        .Produces(404);
    }

    private static void MapAskSessionAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/chat/ask-agent", async (
            Guid sessionId,
            AskSessionAgentCommand command,
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
        .WithName("AskSessionAgent")
        .WithTags("SessionTracking", "Chat", "AI")
        .WithSummary("Ask the RAG agent a question in session context [STUB]")
        .WithDescription("Sends a question to the AI agent which answers using the game's knowledge base and session context. Currently returns a stub response pending RAG pipeline integration.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(404);
    }

    private static void MapDeleteChatMessageEndpoint(RouteGroupBuilder group)
    {
        // Note: requesterId comes from query string. Future auth refactor should extract from HttpContext.User.
        group.MapDelete("/game-sessions/{sessionId:guid}/chat/{messageId:guid}", async (
            Guid sessionId,
            Guid messageId,
            Guid requesterId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new DeleteChatMessageCommand(messageId, requesterId);
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthenticatedUser()
        .WithName("DeleteSessionChatMessage")
        .WithTags("SessionTracking", "Chat")
        .WithSummary("Delete a chat message")
        .WithDescription("Soft-deletes a text message. Only the sender can delete.")
        .Produces(204)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    // ========== Toolkit Session State Write Endpoints (Issue #5148 — Epic B5) ==========

    /// <summary>
    /// Updates the runtime state of a single widget within a session.
    /// Auto-creates the ToolkitSessionState record on first call.
    /// </summary>
    private static void MapUpdateToolkitWidgetStateEndpoint(RouteGroupBuilder group)
    {
        group.MapPatch("/game-sessions/{sessionId:guid}/toolkit-state/{widgetType}", async (
            Guid sessionId,
            string widgetType,
            [FromQuery] Guid toolkitId,
            [FromBody] UpdateWidgetStateRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdStr = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Results.Unauthorized();

            var command = new UpdateWidgetStateCommand(sessionId, toolkitId, widgetType, request.StateJson, userId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<ToolkitSessionStateDto>(200)
        .Produces(400)
        .Produces(401)
        .WithName("UpdateToolkitWidgetState")
        .WithTags("Toolkit")
        .WithSummary("Update a widget's runtime state in a session")
        .WithDescription("Persists the runtime state for a single widget (turn count, scores, resources etc.). Auto-creates the state record on first save. Issue #5148.")
        .WithOpenApi();
    }

    // ========== Session Diary Write Endpoints (Issue #276) ==========

    private static void MapAddSessionEventEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/events", async (
            Guid sessionId,
            AddSessionEventRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var command = new AddSessionEventCommand(
                sessionId,
                userId,
                request.EventType,
                request.Payload,
                request.Source);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/game-sessions/{sessionId}/events/{result.EventId}", result);
        })
        .RequireAuthenticatedUser()
        .WithName("AddSessionEvent")
        .WithTags("SessionTracking", "SessionDiary")
        .WithSummary("Add an event to the session timeline")
        .WithDescription("Records a new event in the session diary. Requires Player role. Issue #276.")
        .Produces(201)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409);
    }

    // ========== AI Turn Summary (Issue #277) ==========

    private static void MapGetTurnSummaryEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/turn-summary", async (
            Guid sessionId,
            TurnSummaryRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var command = new GetTurnSummaryCommand(
                sessionId,
                userId,
                request.FromPhase,
                request.ToPhase,
                request.LastNEvents);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetTurnSummary")
        .WithTags("SessionTracking", "AI")
        .WithSummary("Generate an AI-powered turn summary")
        .WithDescription("Uses LLM to summarize recent session events. Requires Player role. Issue #277.")
        .Produces<TurnSummaryResult>(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409);
    }

    // ========== Session Checkpoint Write Endpoints (Issue #278) ==========

    private static void MapCreateCheckpointEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/checkpoints", async (
            Guid sessionId,
            CreateCheckpointRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty) return Results.Unauthorized();

            var command = new Api.BoundedContexts.SessionTracking.Application.Commands.CreateSessionCheckpointCommand(
                sessionId, userId, request.Name);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/game-sessions/{sessionId}/checkpoints/{result.CheckpointId}", result);
        })
        .RequireAuthenticatedUser()
        .WithName("CreateSessionCheckpoint")
        .WithTags("SessionTracking", "Checkpoints")
        .WithSummary("Create a session checkpoint (deep save)")
        .WithDescription("Captures toolkit widget states and diary event count. Issue #278.")
        .Produces(201).Produces(400).Produces(401).Produces(403).Produces(404).Produces(409);
    }

    private static void MapRestoreCheckpointEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/checkpoints/{checkpointId:guid}/restore", async (
            Guid sessionId,
            Guid checkpointId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty) return Results.Unauthorized();

            var command = new Api.BoundedContexts.SessionTracking.Application.Commands.RestoreSessionCheckpointCommand(
                sessionId, userId, checkpointId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("RestoreSessionCheckpoint")
        .WithTags("SessionTracking", "Checkpoints")
        .WithSummary("Restore session from checkpoint")
        .WithDescription("Restores toolkit widget states from a saved checkpoint. Issue #278.")
        .Produces<Api.BoundedContexts.SessionTracking.Application.Commands.RestoreSessionCheckpointResult>(200)
        .Produces(400).Produces(401).Produces(403).Produces(404).Produces(409);
    }
}
