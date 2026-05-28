using Api.BoundedContexts.Administration.Application.Queries.ActivityFeed;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// User-facing cross-entity activity feed endpoint.
/// Reads from <c>domain_event_logs</c> scoped to the authenticated caller.
/// BE-3 #1590 Task 8 — <c>GET /api/v1/activity</c>.
/// </summary>
internal static class ActivityFeedEndpoints
{
    public static RouteGroupBuilder MapActivityFeedEndpoints(this RouteGroupBuilder group)
    {
        MapGetActivityFeedEndpoint(group);
        return group;
    }

    private static void MapGetActivityFeedEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/activity", async (
            [FromQuery] int? limit,
            [FromQuery] DateTime? since,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            var userId = session!.Principal!.Subject!.Id;

            var query = new GetActivityFeedQuery(
                UserId: userId,
                Limit: limit ?? 20,
                Since: since
            );

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                success = true,
                items = result.Items,
                count = result.Count,
            });
        })
        .RequireAuthenticatedUser()
        .Produces(200)
        .Produces(401)
        .Produces(422)
        .WithTags("Activity")
        .WithSummary("Get cross-entity activity feed")
        .WithDescription(
            "Returns the caller's cross-entity activity feed from domain_event_logs. " +
            "Events are scoped to the authenticated user, filtered to the last 90 days, " +
            "ordered by loggedAt DESC. " +
            "?limit (1-100, default 20), ?since (cursor: only events before this timestamp). " +
            "BE-3 #1590.")
        .WithOpenApi();
    }
}
