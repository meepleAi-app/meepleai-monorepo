using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Notification preferences endpoints.
/// Issue #4220: Multi-channel notification configuration.
/// Issue #4416: Push notification subscribe/unsubscribe.
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

        group.MapGet("/notifications/push/vapid-key", async (IMediator mediator, CancellationToken ct) =>
        {
            var query = new GetVapidPublicKeyQuery();
            var publicKey = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(new { publicKey });
        })
        .WithName("GetVapidPublicKey");

        return group;
    }
}
