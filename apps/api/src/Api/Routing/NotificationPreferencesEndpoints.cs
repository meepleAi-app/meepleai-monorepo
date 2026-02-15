using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Notification preferences and email history endpoints.
/// Issue #4220: Multi-channel notification configuration.
/// Issue #4416: Push notification subscribe/unsubscribe.
/// Issue #4417: Email queue history and resend.
/// </summary>
internal static class NotificationPreferencesEndpoints
{
    public static RouteGroupBuilder MapNotificationPreferencesEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/notifications/preferences", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var query = new GetNotificationPreferencesQuery(session!.User!.Id);
            var prefs = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(prefs);
        })
        .RequireSession()
        .WithName("GetNotificationPreferences");

        group.MapPut("/notifications/preferences", async (
            UpdateNotificationPreferencesCommand command,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var updatedCommand = command with { UserId = session!.User!.Id };
            await mediator.Send(updatedCommand, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireSession()
        .WithName("UpdateNotificationPreferences");

        // Issue #4416: Push notification subscription management
        group.MapPost("/notifications/push/subscribe", async (
            SubscribePushNotificationsCommand command,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var updatedCommand = command with { UserId = session!.User!.Id };
            await mediator.Send(updatedCommand, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireSession()
        .WithName("SubscribePushNotifications");

        group.MapDelete("/notifications/push/unsubscribe", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var command = new UnsubscribePushNotificationsCommand(session!.User!.Id);
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireSession()
        .WithName("UnsubscribePushNotifications");

        // Issue #4416: Send test push notification
        group.MapPost("/notifications/push/test", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var command = new SendTestPushNotificationCommand(session!.User!.Id);
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(new { message = "Test notification sent" });
        })
        .RequireSession()
        .WithName("SendTestPushNotification");

        group.MapGet("/notifications/push/vapid-key", async (IMediator mediator, CancellationToken ct) =>
        {
            var query = new GetVapidPublicKeyQuery();
            var publicKey = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(new { publicKey });
        })
        .WithName("GetVapidPublicKey");

        // Issue #4417: Email queue history
        group.MapGet("/emails", async (
            [AsParameters] EmailHistoryRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var query = new GetEmailHistoryQuery(
                session!.User!.Id,
                request.Skip ?? 0,
                request.Take ?? 20);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("GetEmailHistory");

        // Issue #4417: Resend failed email
        group.MapPost("/emails/{emailId}/resend", async (
            Guid emailId,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var command = new ResendFailedEmailCommand(emailId, session!.User!.Id);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return result ? Results.Ok() : Results.NotFound();
        })
        .RequireSession()
        .WithName("ResendFailedEmail");

        return group;
    }

    // Issue #4417: Parameter binding for email history query
    internal record EmailHistoryRequest(int? Skip, int? Take);
}
