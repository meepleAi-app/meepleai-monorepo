using System.Security.Claims;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// User notification endpoints.
/// Handles notification retrieval, marking as read, and unread count.
/// Issue #2053: User notifications for upload/processing completion.
/// </summary>
public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
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
            Guid userId;
            if (session != null)
            {
                userId = session.User!.Id;
            }
            else
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out userId))
                {
                    return Results.Unauthorized();
                }
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

        // Get unread notification count (optimized for badge)
        group.MapGet("/notifications/unread-count", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            Guid userId;
            if (session != null)
            {
                userId = session.User!.Id;
            }
            else
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out userId))
                {
                    return Results.Unauthorized();
                }
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

        // Mark single notification as read
        group.MapPost("/notifications/{notificationId}/mark-read", async (
            Guid notificationId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            Guid userId;
            if (session != null)
            {
                userId = session.User!.Id;
            }
            else
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out userId))
                {
                    return Results.Unauthorized();
                }
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

        // Mark all notifications as read
        group.MapPost("/notifications/mark-all-read", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            Guid userId;
            if (session != null)
            {
                userId = session.User!.Id;
            }
            else
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out userId))
                {
                    return Results.Unauthorized();
                }
            }

            var command = new MarkAllNotificationsReadCommand(userId);
            var updatedCount = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(new { updatedCount });
        })
        .RequireAuthenticatedUser()
        .Produces<object>(200)
        .WithTags("Notifications")
        .WithSummary("Mark all notifications as read")
        .WithDescription("Bulk operation to mark all unread notifications as read for authenticated user. Returns count of updated notifications.");

        return group;
    }
}
