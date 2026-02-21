using System.Security.Claims;
using System.Text.Json;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// User notification endpoints.
/// Handles notification retrieval, marking as read, and unread count.
/// Issue #2053: User notifications for upload/processing completion.
/// </summary>
internal static class NotificationEndpoints
{
    private static readonly JsonSerializerOptions SseJsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        MapGetNotificationsEndpoint(group);
        MapGetUnreadCountEndpoint(group);
        MapMarkNotificationReadEndpoint(group);
        MapMarkAllNotificationsReadEndpoint(group);
        MapNotificationStreamEndpoint(group);  // Issue #5005

        return group;
    }

    private static void MapGetNotificationsEndpoint(RouteGroupBuilder group)
    {
        // Get notifications for authenticated user
        group.MapGet("/notifications", async (
            [FromQuery] bool? unreadOnly,
            [FromQuery] int? limit,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // User authenticated via session OR API key (RequireAuthenticatedUserFilter)
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            // Extract userId from session or API key claims
            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetNotificationsQuery(userId, unreadOnly, limit);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<List<NotificationDto>>(200)
        .WithTags("Notifications")
        .WithSummary("Get user notifications with optional filtering")
        .WithDescription("Returns notifications for authenticated user. Supports filtering by read status and limiting results.");
    }

    private static void MapGetUnreadCountEndpoint(RouteGroupBuilder group)
    {
        // Get unread notification count (optimized for badge)
        group.MapGet("/notifications/unread-count", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetUnreadCountQuery(userId);
            var count = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(new { count });
        })
        .RequireAuthenticatedUser()
        .Produces<object>(200)
        .WithTags("Notifications")
        .WithSummary("Get unread notification count")
        .WithDescription("Returns count of unread notifications for badge display. Optimized query.");
    }

    private static void MapMarkNotificationReadEndpoint(RouteGroupBuilder group)
    {
        // Mark single notification as read
        group.MapPost("/notifications/{notificationId}/mark-read", async (
            Guid notificationId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new MarkNotificationReadCommand(notificationId, userId);
            var success = await mediator.Send(command, ct).ConfigureAwait(false);

            return success ? Results.Ok(new { success = true }) : Results.NotFound();
        })
        .RequireAuthenticatedUser()
        .Produces<object>(200)
        .Produces(404)
        .WithTags("Notifications")
        .WithSummary("Mark notification as read")
        .WithDescription("Marks a single notification as read for authenticated user. Returns 404 if notification not found or unauthorized.");
    }

    private static void MapMarkAllNotificationsReadEndpoint(RouteGroupBuilder group)
    {
        // Mark all notifications as read
        // Issue #2155: Rate limited to prevent abuse (10 req/min)
        group.MapPost("/notifications/mark-all-read", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new MarkAllNotificationsReadCommand(userId);
            var updatedCount = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(new { updatedCount });
        })
        .RequireAuthenticatedUser()
        .RequireNotificationRateLimit()
        .Produces<object>(200)
        .Produces(429)
        .WithTags("Notifications")
        .WithSummary("Mark all notifications as read")
        .WithDescription("Bulk operation to mark all unread notifications as read for authenticated user. Returns count of updated notifications. Rate limited: 10 requests per minute.");
    }

    private static void MapNotificationStreamEndpoint(RouteGroupBuilder group)
    {
        // SSE endpoint: real-time notification delivery (Issue #5005)
        group.MapGet("/notifications/stream", async (
            IUserNotificationBroadcaster broadcaster,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            var response = context.Response;
            response.Headers["Content-Type"] = "text/event-stream";
            response.Headers["Cache-Control"] = "no-cache";
            response.Headers["Connection"] = "keep-alive";
            response.Headers["X-Accel-Buffering"] = "no";

            await response.Body.FlushAsync(ct).ConfigureAwait(false);

            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var heartbeatTask = Task.Run(async () =>
            {
                while (!heartbeatCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), heartbeatCts.Token).ConfigureAwait(false);
                        await response.WriteAsync(": heartbeat\n\n", heartbeatCts.Token).ConfigureAwait(false);
                        await response.Body.FlushAsync(heartbeatCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, heartbeatCts.Token);

            try
            {
                await foreach (var notification in broadcaster.SubscribeAsync(userId, ct).ConfigureAwait(false))
                {
                    var json = JsonSerializer.Serialize(notification, SseJsonOptions);
                    await response.WriteAsync($"event: notification\ndata: {json}\n\n", ct).ConfigureAwait(false);
                    await response.Body.FlushAsync(ct).ConfigureAwait(false);
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
        .Produces(200)
        .WithTags("Notifications")
        .WithSummary("SSE stream for real-time notifications")
        .WithDescription("Server-Sent Events stream delivering real-time notifications to authenticated users. Sends 30-second heartbeat comments to keep the connection alive.");
    }

    private static bool TryGetUserId(HttpContext context, SessionStatusDto? session, out Guid userId)
    {
        userId = Guid.Empty;
        if (session != null)
        {
            userId = session.User!.Id;
            return true;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out userId))
        {
            return true;
        }

        return false;
    }
}
