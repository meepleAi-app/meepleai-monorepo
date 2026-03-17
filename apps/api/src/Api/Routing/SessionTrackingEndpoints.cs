using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Session tracking endpoints for collaborative game sessions.
/// </summary>
internal static class SessionTrackingEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static RouteGroupBuilder MapSessionTrackingEndpoints(this RouteGroupBuilder group)
    {
        // Command endpoints
        MapCreateSessionEndpoint(group);
        MapUpdateScoreEndpoint(group);
        MapAddParticipantEndpoint(group);
        MapAddNoteEndpoint(group);
        MapFinalizeSessionEndpoint(group);
        MapRollDiceEndpoint(group);

        // Card deck endpoints (Issue #3343)
        MapCreateDeckEndpoint(group);
        MapShuffleDeckEndpoint(group);
        MapDrawCardsEndpoint(group);
        MapDiscardCardsEndpoint(group);
        MapGetSessionDecksEndpoint(group);
        MapGetPlayerHandEndpoint(group);
        MapGetDiscardPileEndpoint(group);

        // Private notes endpoints (Issue #3344)
        MapSavePrivateNoteEndpoint(group);
        MapRevealNoteEndpoint(group);
        MapHideNoteEndpoint(group);
        MapDeletePrivateNoteEndpoint(group);
        MapGetSessionNotesEndpoint(group);
        MapGetNoteByIdEndpoint(group);

        // Random tools endpoints (Issue #3345)
        MapStartTimerEndpoint(group);
        MapPauseTimerEndpoint(group);
        MapResumeTimerEndpoint(group);
        MapResetTimerEndpoint(group);
        MapFlipCoinEndpoint(group);
        MapSpinWheelEndpoint(group);

        // Query endpoints
        MapGetActiveSessionEndpoint(group);
        MapGetSessionByCodeEndpoint(group);
        MapGetScoreboardEndpoint(group);
        MapGetSessionDetailsEndpoint(group);
        MapGetSessionHistoryEndpoint(group);
        MapGetDiceRollHistoryEndpoint(group);

        // GST-003: Real-time SSE stream
        MapSessionStreamEndpoint(group);
        // Issue #4764: Enhanced SSE stream with reconnection, typed events, selective broadcasting
        MapEnhancedSessionStreamEndpoint(group);

        // Session export and sharing endpoints (Issue #3347)
        MapExportSessionPdfEndpoint(group);
        MapGetShareableSessionEndpoint(group);
        MapGenerateShareLinkEndpoint(group);

        // Session invite link endpoints (Issue #3354)
        MapGenerateInviteTokenEndpoint(group);
        MapGetSessionByInviteEndpoint(group);
        MapJoinSessionByInviteEndpoint(group);

        // Session media endpoints (Issue #4760)
        MapUploadSessionMediaEndpoint(group);
        MapGetSessionMediaEndpoint(group);
        MapUpdateMediaCaptionEndpoint(group);
        MapDeleteSessionMediaEndpoint(group);

        // Session chat endpoints (Issue #4760)
        MapGetSessionChatEndpoint(group);
        MapSendSessionChatMessageEndpoint(group);
        MapAskSessionAgentEndpoint(group);
        MapDeleteChatMessageEndpoint(group);

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

        // Toolkit session state endpoints (Issue #5148 — Epic B5)
        MapGetToolkitSessionStateEndpoint(group);
        MapUpdateToolkitWidgetStateEndpoint(group);

        // Session diary / timeline endpoints (Issue #276)
        MapAddSessionEventEndpoint(group);
        MapGetSessionEventsEndpoint(group);

        // AI-powered turn summary (Issue #277)
        MapGetTurnSummaryEndpoint(group);

        // Session checkpoint / deep save endpoints (Issue #278)
        MapCreateCheckpointEndpoint(group);
        MapListCheckpointsEndpoint(group);
        MapRestoreCheckpointEndpoint(group);

        return group;
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

    private static void MapGetDiceRollHistoryEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/dice", async (
            Guid sessionId,
            IMediator mediator,
            int limit = 20,
            CancellationToken ct = default) =>
        {
            var query = new GetDiceRollHistoryQuery(sessionId, limit);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetDiceRollHistory")
        .WithTags("SessionTracking", "Dice")
        .WithSummary("Get recent dice rolls for a session")
        .WithDescription("Returns the most recent dice rolls for the session (default: 20).")
        .Produces(200)
        .Produces(401)
        .Produces(404);
    }

    private static void MapGetActiveSessionEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/active", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Extract user ID from authenticated context
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetActiveSessionQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result == null)
            {
                return Results.NotFound(new { error = "No active session found" });
            }

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetActiveSession")
        .WithTags("SessionTracking")
        .WithSummary("Get user's active session")
        .Produces(200)
        .Produces(401)
        .Produces(404);
    }

    private static void MapGetSessionByCodeEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/code/{code}", async (
            string code,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetSessionByCodeQuery(code);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result == null)
            {
                return Results.NotFound(new { error = "Session not found" });
            }

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetSessionByCode")
        .WithTags("SessionTracking")
        .WithSummary("Find session by code")
        .Produces(200)
        .Produces(401)
        .Produces(404);
    }

    private static void MapGetScoreboardEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/scoreboard", async (
            Guid sessionId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetScoreboardQuery(sessionId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetScoreboard")
        .WithTags("SessionTracking")
        .WithSummary("Get session scoreboard")
        .Produces(200)
        .Produces(401)
        .Produces(404);
    }

    private static void MapGetSessionDetailsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}", async (
            Guid sessionId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetSessionDetailsQuery(sessionId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result == null)
            {
                return Results.NotFound(new { error = "Session not found" });
            }

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetSessionDetails")
        .WithTags("SessionTracking")
        .WithSummary("Get session details")
        .Produces(200)
        .Produces(401)
        .Produces(404);
    }

    private static void MapGetSessionHistoryEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/history", async (
            IMediator mediator,
            HttpContext context,
            Guid? gameId = null,
            int limit = 20,
            int offset = 0,
            CancellationToken ct = default) =>
        {
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetSessionHistoryQuery(userId, gameId, limit, offset);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetSessionHistory")
        .WithTags("SessionTracking")
        .WithSummary("Get session history (paginated)")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }

    /// <summary>
    /// GST-003: Server-Sent Events (SSE) endpoint for real-time session updates.
    /// Streams events to connected clients for multi-device collaboration.
    /// </summary>
    private static void MapSessionStreamEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/stream", async (
            Guid sessionId,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Extract user ID from claims
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            // Get authorized event stream via CQRS query
            IAsyncEnumerable<INotification> eventStream;
            try
            {
                var query = new GetSessionStreamQuery(sessionId, userId);
                eventStream = await mediator.Send(query, ct).ConfigureAwait(false);
            }
            catch (Api.Middleware.Exceptions.NotFoundException)
            {
                return Results.NotFound(new { error = "Session not found" });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(403);
            }

            // Set SSE response headers
            context.Response.Headers.Append("Content-Type", "text/event-stream");
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("Connection", "keep-alive");
            context.Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering

            // Create heartbeat task for keep-alive
            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var heartbeatTask = Task.Run(async () =>
            {
                while (!heartbeatCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), heartbeatCts.Token).ConfigureAwait(false);
                        await context.Response.WriteAsync("event: heartbeat\n", heartbeatCts.Token).ConfigureAwait(false);
                        await context.Response.WriteAsync($"data: {{\"timestamp\":\"{DateTime.UtcNow:O}\"}}\n\n", heartbeatCts.Token).ConfigureAwait(false);
                        await context.Response.Body.FlushAsync(heartbeatCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, heartbeatCts.Token);

            try
            {
                // Stream events to client
                await foreach (var evt in eventStream.ConfigureAwait(false))
                {
                    var eventType = evt.GetType().Name;
                    var json = JsonSerializer.Serialize(evt, JsonOptions);

                    await context.Response.WriteAsync($"event: {eventType}\n", ct).ConfigureAwait(false);
                    await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                    await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
                }
            }
            finally
            {
                // Cleanup heartbeat task on disconnect
                await heartbeatCts.CancelAsync().ConfigureAwait(false);
                await heartbeatTask.ConfigureAwait(false);
            }

            return Results.Empty;
        })
        .RequireAuthenticatedUser()
        .WithName("StreamSessionEvents")
        .WithTags("SessionTracking", "Real-Time")
        .WithSummary("Subscribe to real-time session events via SSE")
        .WithDescription("Server-Sent Events stream for real-time collaborative session updates. Use EventSource API in browsers.")
        .Produces(200)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    // ========== Enhanced SSE Endpoint (Issue #4764) ==========

    /// <summary>
    /// Enhanced SSE endpoint with Last-Event-ID reconnection, typed events,
    /// connection pool limits, and selective broadcasting support.
    /// </summary>
    private static void MapEnhancedSessionStreamEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/stream/v2", async (
            Guid sessionId,
            HttpContext context,
            ISessionBroadcastService broadcastService,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Extract user ID from claims
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            // Verify session access via CQRS query
            try
            {
                var query = new GetSessionStreamQuery(sessionId, userId);
                await mediator.Send(query, ct).ConfigureAwait(false);
            }
            catch (Api.Middleware.Exceptions.NotFoundException)
            {
                return Results.NotFound(new { error = "Session not found" });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(403);
            }

            // Check connection pool limit
            if (broadcastService.GetConnectionCount(sessionId) >= 20) // MaxConnectionsPerSession
            {
                return Results.StatusCode(429); // Too Many Requests
            }

            // Get Last-Event-ID for reconnection
            var lastEventId = context.Request.Headers["Last-Event-ID"].FirstOrDefault();

            // Set SSE response headers
            context.Response.Headers.Append("Content-Type", "text/event-stream");
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("Connection", "keep-alive");
            context.Response.Headers.Append("X-Accel-Buffering", "no");

            // Create heartbeat task
            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var heartbeatTask = Task.Run(async () =>
            {
                while (!heartbeatCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), heartbeatCts.Token).ConfigureAwait(false);
                        await context.Response.WriteAsync(
                            $"event: heartbeat\ndata: {{\"timestamp\":\"{DateTime.UtcNow:O}\"}}\n\n",
                            heartbeatCts.Token).ConfigureAwait(false);
                        await context.Response.Body.FlushAsync(heartbeatCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, heartbeatCts.Token);

            try
            {
                // Stream events to client using enhanced broadcast service
                await foreach (var envelope in broadcastService.SubscribeAsync(sessionId, userId, lastEventId, ct).ConfigureAwait(false))
                {
                    var json = JsonSerializer.Serialize(envelope.Data, JsonOptions);

                    // Write SSE format with event ID for reconnection
                    await context.Response.WriteAsync($"id: {envelope.Id}\n", ct).ConfigureAwait(false);
                    await context.Response.WriteAsync($"event: {envelope.EventType}\n", ct).ConfigureAwait(false);
                    await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                    await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
                }
            }
            finally
            {
                await heartbeatCts.CancelAsync().ConfigureAwait(false);
                await heartbeatTask.ConfigureAwait(false);
            }

            return Results.Empty;
        })
        .RequireAuthenticatedUser()
        .WithName("StreamSessionEventsV2")
        .WithTags("SessionTracking", "Real-Time")
        .WithSummary("Enhanced SSE stream with reconnection, typed events, and selective broadcasting")
        .WithDescription("Server-Sent Events v2 with Last-Event-ID reconnection, typed event names (session:score, session:turn, etc.), connection pool limits, and per-player event filtering.")
        .Produces(200)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(429);
    }

    // ========== Card Deck Endpoints (Issue #3343) ==========

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

    // ========== Private Notes Endpoints (Issue #3344) ==========

    private static void MapSavePrivateNoteEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/private-notes", async (
            Guid sessionId,
            SaveNoteCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId)
            {
                return Results.BadRequest(new { error = "Session ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/game-sessions/{sessionId}/private-notes/{result.NoteId}", result);
        })
        .RequireAuthenticatedUser()
        .WithName("SavePrivateNote")
        .WithTags("SessionTracking", "PrivateNotes")
        .WithSummary("Save or update a private note")
        .WithDescription("Creates a new encrypted private note or updates an existing one. Notes are private by default.")
        .Produces(201)
        .Produces(400)
        .Produces(401)
        .Produces(403);
    }

    private static void MapRevealNoteEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/private-notes/{noteId:guid}/reveal", async (
            Guid sessionId,
            Guid noteId,
            RevealNoteCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId || noteId != command.NoteId)
            {
                return Results.BadRequest(new { error = "Session or Note ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("RevealNote")
        .WithTags("SessionTracking", "PrivateNotes")
        .WithSummary("Reveal a private note to all participants")
        .WithDescription("Makes the note content visible to all session participants. Only the note owner can reveal.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    private static void MapHideNoteEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/private-notes/{noteId:guid}/hide", async (
            Guid sessionId,
            Guid noteId,
            HideNoteCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (sessionId != command.SessionId || noteId != command.NoteId)
            {
                return Results.BadRequest(new { error = "Session or Note ID mismatch" });
            }

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("HideNote")
        .WithTags("SessionTracking", "PrivateNotes")
        .WithSummary("Hide a previously revealed note")
        .WithDescription("Makes the note private again. Only the note owner can hide.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    private static void MapDeletePrivateNoteEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/game-sessions/{sessionId:guid}/private-notes/{noteId:guid}", async (
            Guid sessionId,
            Guid noteId,
            Guid participantId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new DeleteNoteCommand(noteId, sessionId, participantId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("DeletePrivateNote")
        .WithTags("SessionTracking", "PrivateNotes")
        .WithSummary("Delete a private note")
        .WithDescription("Soft-deletes a note. Only the note owner can delete.")
        .Produces(200)
        .Produces(401)
        .Produces(403)
        .Produces(404);
    }

    private static void MapGetSessionNotesEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/private-notes", async (
            Guid sessionId,
            Guid requesterId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetSessionNotesQuery(sessionId, requesterId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetSessionNotes")
        .WithTags("SessionTracking", "PrivateNotes")
        .WithSummary("Get all visible notes in the session")
        .WithDescription("Returns the requester's own notes and any revealed notes from other participants.")
        .Produces(200)
        .Produces(401);
    }

    private static void MapGetNoteByIdEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/private-notes/{noteId:guid}", async (
            Guid sessionId,
            Guid noteId,
            Guid requesterId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetNoteByIdQuery(noteId, requesterId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result is null)
            {
                return Results.NotFound(new { error = "Note not found or not accessible" });
            }

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetNoteById")
        .WithTags("SessionTracking", "PrivateNotes")
        .WithSummary("Get a specific note by ID")
        .WithDescription("Returns the note if the requester is the owner or if the note is revealed.")
        .Produces(200)
        .Produces(401)
        .Produces(404);
    }

    // ========================================================================
    // Random Tools Endpoints (Issue #3345)
    // ========================================================================

    private static void MapStartTimerEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/timer/start", async (
            Guid sessionId,
            StartTimerCommand command,
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
        .WithName("StartTimer")
        .WithTags("SessionTracking", "RandomTools")
        .WithSummary("Start a countdown timer for the session")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }

    private static void MapPauseTimerEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/timer/pause", async (
            Guid sessionId,
            PauseTimerCommand command,
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
        .WithName("PauseTimer")
        .WithTags("SessionTracking", "RandomTools")
        .WithSummary("Pause the session timer")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }

    private static void MapResumeTimerEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/timer/resume", async (
            Guid sessionId,
            ResumeTimerCommand command,
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
        .WithName("ResumeTimer")
        .WithTags("SessionTracking", "RandomTools")
        .WithSummary("Resume a paused session timer")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }

    private static void MapResetTimerEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/timer/reset", async (
            Guid sessionId,
            ResetTimerCommand command,
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
        .WithName("ResetTimer")
        .WithTags("SessionTracking", "RandomTools")
        .WithSummary("Reset the session timer")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }

    private static void MapFlipCoinEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/coin-flip", async (
            Guid sessionId,
            FlipCoinCommand command,
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
        .WithName("FlipCoin")
        .WithTags("SessionTracking", "RandomTools")
        .WithSummary("Flip a coin for the session")
        .WithDescription("Returns a cryptographically secure random heads or tails result.")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }

    private static void MapSpinWheelEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/game-sessions/{sessionId:guid}/wheel-spin", async (
            Guid sessionId,
            SpinWheelCommand command,
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
        .WithName("SpinWheel")
        .WithTags("SessionTracking", "RandomTools")
        .WithSummary("Spin a wheel with custom options")
        .WithDescription("Performs weighted random selection from the provided options.")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }

    // ========================================================================
    // Session Export and Sharing Endpoints (Issue #3347)
    // ========================================================================

    private static void MapExportSessionPdfEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/export/pdf", async (
            Guid sessionId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct,
            bool includeScoreChart = true,
            bool includeDiceHistory = false,
            bool includeCardHistory = false) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var query = new ExportSessionPdfQuery(
                sessionId,
                userId,
                includeScoreChart,
                includeDiceHistory,
                includeCardHistory);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.File(
                result.PdfContent,
                result.ContentType,
                result.FileName);
        })
        .RequireAuthenticatedUser()
        .WithName("ExportSessionPdf")
        .WithTags("SessionTracking", "Export")
        .WithSummary("Export session as PDF")
        .WithDescription("Generates a PDF report of the session with scores, rankings, and optional history.")
        .Produces(200)
        .Produces(401)
        .Produces(404);
    }

    private static void MapGetShareableSessionEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/share", async (
            Guid sessionId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetShareableSessionQuery(sessionId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithName("GetShareableSession")
        .WithTags("SessionTracking", "Share")
        .WithSummary("Get shareable session summary")
        .WithDescription("Returns a public-facing session summary that can be shared without authentication.")
        .Produces(200)
        .Produces(404);
    }

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

    // ========================================================================
    // Session Invite Link Endpoints (Issue #3354)
    // ========================================================================

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

    private static void MapGetSessionByInviteEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/invite/{inviteToken}", async (
            string inviteToken,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetSessionByInviteQuery(inviteToken);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithName("GetSessionByInvite")
        .WithTags("SessionTracking", "Invite")
        .WithSummary("Get session information by invite token")
        .WithDescription("Returns session preview information for the invite link. Does not require authentication.")
        .Produces(200)
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

    // ========================================================================
    // Session Media Endpoints (Issue #4760)
    // ========================================================================

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

    private static void MapGetSessionMediaEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/media", async (
            Guid sessionId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetSessionMediaQuery(sessionId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetSessionMedia")
        .WithTags("SessionTracking", "Media")
        .WithSummary("Get all media for a session")
        .Produces(200)
        .Produces(401);
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

    // ========================================================================
    // Session Chat Endpoints (Issue #4760)
    // ========================================================================

    private static void MapGetSessionChatEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/chat", async (
            Guid sessionId,
            IMediator mediator,
            int? limit = null,
            int? offset = null,
            CancellationToken ct = default) =>
        {
            var query = new GetSessionChatQuery(sessionId, limit, offset);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetSessionChat")
        .WithTags("SessionTracking", "Chat")
        .WithSummary("Get chat messages for a session (paginated)")
        .Produces(200)
        .Produces(401);
    }

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
    // ========================================================================
    // Player Action Endpoints (Issue #4765)
    // ========================================================================

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

    // ========================================================================
    // Session Join + Role Management Endpoints (Issue #4766)
    // ========================================================================

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

    // ============================================================================
    // Toolkit session state endpoints (Issue #5148 — Epic B5)
    // ============================================================================

    /// <summary>
    /// Returns the current toolkit widget states for a session.
    /// Returns 204 if no state has been saved yet.
    /// </summary>
    private static void MapGetToolkitSessionStateEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/toolkit-state", async (
            Guid sessionId,
            [FromQuery] Guid toolkitId,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdStr = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Results.Unauthorized();

            var result = await mediator
                .Send(new GetToolkitSessionStateQuery(sessionId, toolkitId, userId), ct)
                .ConfigureAwait(false);

            return result is null ? Results.NoContent() : Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<ToolkitSessionStateDto>(200)
        .Produces(204)
        .Produces(401)
        .WithName("GetToolkitSessionState")
        .WithTags("Toolkit")
        .WithSummary("Get toolkit widget states for a session")
        .WithDescription("Returns the persisted widget states for the session's active toolkit. Returns 204 if no state saved yet. Issue #5148.")
        .WithOpenApi();
    }

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

    // ============================================================================
    // Session diary / timeline endpoints (Issue #276)
    // ============================================================================

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

    private static void MapGetSessionEventsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/events", async (
            Guid sessionId,
            IMediator mediator,
            [FromQuery] string? eventType = null,
            [FromQuery] int limit = 50,
            [FromQuery] int offset = 0,
            CancellationToken ct = default) =>
        {
            var query = new GetSessionEventsQuery(sessionId, eventType, limit, offset);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("GetSessionEvents")
        .WithTags("SessionTracking", "SessionDiary")
        .WithSummary("Get session timeline events")
        .WithDescription("Returns paginated session events, optionally filtered by event type. Issue #276.")
        .Produces<GetSessionEventsResult>(200)
        .Produces(401)
        .Produces(404);
    }

    // AI-powered turn summary (Issue #277)
    // ============================================================================

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


    // ============================================================================
    // Session checkpoint / deep save endpoints (Issue #278)
    // ============================================================================

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

    private static void MapListCheckpointsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/{sessionId:guid}/checkpoints", async (
            Guid sessionId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == Guid.Empty) return Results.Unauthorized();

            var query = new Api.BoundedContexts.SessionTracking.Application.Queries.ListSessionCheckpointsQuery(
                sessionId, userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .WithName("ListSessionCheckpoints")
        .WithTags("SessionTracking", "Checkpoints")
        .WithSummary("List session checkpoints")
        .WithDescription("Returns all checkpoints for a session. Issue #278.")
        .Produces<Api.BoundedContexts.SessionTracking.Application.Queries.ListSessionCheckpointsResult>(200)
        .Produces(401).Produces(404);
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

    internal static RouteGroupBuilder MapSessionStatisticsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/game-sessions/session-statistics", async (
            HttpContext httpContext, IMediator mediator, [FromQuery] int? monthsBack) =>
        {
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var parsedUserId))
                return Results.Unauthorized();

            var query = new Api.BoundedContexts.SessionTracking.Application.Queries.GetSessionStatisticsQuery(
                parsedUserId, monthsBack ?? 6);
            var result = await mediator.Send(query).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .Produces<Api.BoundedContexts.SessionTracking.Application.DTOs.SessionStatisticsDto>(200)
        .Produces(401)
        .WithName("GetSessionStatistics")
        .WithTags("SessionStatistics")
        .WithSummary("Get aggregated session statistics for the current user")
        .WithOpenApi();

        group.MapGet("/game-sessions/session-statistics/game/{gameId:guid}", async (
            Guid gameId, HttpContext httpContext, IMediator mediator) =>
        {
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var parsedUserId))
                return Results.Unauthorized();

            var query = new Api.BoundedContexts.SessionTracking.Application.Queries.GetGameStatisticsQuery(
                parsedUserId, gameId);
            var result = await mediator.Send(query).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .Produces<Api.BoundedContexts.SessionTracking.Application.DTOs.GameStatisticsDto>(200)
        .Produces(401)
        .WithName("GetGameStatistics")
        .WithTags("SessionStatistics")
        .WithSummary("Get statistics for a specific game for the current user")
        .WithOpenApi();

        return group;
    }
}

/// <summary>
/// Request body for joining a session by invite token.
/// </summary>
public sealed record JoinSessionByInviteRequest(string DisplayName);

/// <summary>
/// Request body for joining a session by code.
/// </summary>
public sealed record JoinSessionByCodeRequest(string DisplayName);

/// <summary>
/// Request body for assigning a participant role.
/// </summary>
public sealed record AssignParticipantRoleRequest(Api.BoundedContexts.SessionTracking.Domain.Enums.ParticipantRole NewRole);

/// <summary>Request body for updating a player score.</summary>
public sealed record UpdatePlayerScoreRequest(Guid ParticipantId, decimal ScoreValue, int? RoundNumber = null, string? Category = null);

/// <summary>Request body for rolling dice.</summary>
public sealed record RollSessionDiceRequest(Guid ParticipantId, string Formula, string? Label = null);

/// <summary>Request body for drawing cards from a deck.</summary>
public sealed record DrawSessionCardRequest(Guid ParticipantId, Guid DeckId, int Count = 1);

/// <summary>Request body for controlling the session timer.</summary>
public sealed record SessionTimerActionRequest(
    Api.BoundedContexts.SessionTracking.Application.Commands.TimerAction Action,
    string? ParticipantName = null,
    int? DurationSeconds = null,
    Guid? ParticipantId = null);

/// <summary>Request body for sending a chat action. SenderId is derived from the authenticated user.</summary>
public sealed record SendChatActionRequest(string Content, int? TurnNumber = null, string? MentionsJson = null);

/// <summary>Request body for adding a session event (Issue #276).</summary>
public sealed record AddSessionEventRequest(string EventType, string? Payload = null, string? Source = null);

/// <summary>Request body for requesting an AI-generated turn summary (Issue #277).</summary>
public sealed record TurnSummaryRequest(int? FromPhase = null, int? ToPhase = null, int? LastNEvents = null);

/// <summary>Request body for creating a session checkpoint (Issue #278).</summary>
internal record CreateCheckpointRequest(string Name);
