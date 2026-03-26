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
/// All GET/query endpoints: active session, scoreboard, session details/history/code,
/// dice history, SSE streams (v1 and v2), export/share, invite, media, chat,
/// toolkit state, diary events, checkpoints, and session statistics.
/// </summary>
internal static class SessionQueryEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Map(RouteGroupBuilder group)
    {
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

        // Session invite link endpoints (Issue #3354)
        MapGetSessionByInviteEndpoint(group);

        // Session media endpoints (Issue #4760)
        MapGetSessionMediaEndpoint(group);

        // Session chat endpoints (Issue #4760)
        MapGetSessionChatEndpoint(group);

        // Toolkit session state endpoints (Issue #5148 — Epic B5)
        MapGetToolkitSessionStateEndpoint(group);

        // Session diary / timeline endpoints (Issue #276)
        MapGetSessionEventsEndpoint(group);

        // Session checkpoint / deep save endpoints (Issue #278)
        MapListCheckpointsEndpoint(group);
    }

    // ========== Core Query Endpoints ==========

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

    // ========== SSE Stream Endpoints ==========

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

    // ========== Export and Sharing Endpoints (Issue #3347) ==========

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

    // ========== Invite Link Endpoints (Issue #3354) ==========

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

    // ========== Media Endpoints (Issue #4760) ==========

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

    // ========== Chat Endpoints (Issue #4760) ==========

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

    // ========== Toolkit Session State Endpoints (Issue #5148 — Epic B5) ==========

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

    // ========== Session Diary / Timeline Endpoints (Issue #276) ==========

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

    // ========== Session Checkpoint Endpoints (Issue #278) ==========

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
}
