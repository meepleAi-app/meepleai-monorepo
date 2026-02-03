using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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

        // Query endpoints
        MapGetActiveSessionEndpoint(group);
        MapGetSessionByCodeEndpoint(group);
        MapGetScoreboardEndpoint(group);
        MapGetSessionDetailsEndpoint(group);
        MapGetSessionHistoryEndpoint(group);

        // GST-003: Real-time SSE stream
        MapSessionStreamEndpoint(group);

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
}
