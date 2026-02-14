using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Notification preferences endpoints.
/// Issue #4220: Multi-channel notification configuration.
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

        return group;
    }
}
