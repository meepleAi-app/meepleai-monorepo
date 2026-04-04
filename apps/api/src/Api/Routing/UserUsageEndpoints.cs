using Api.BoundedContexts.Administration.Application.Queries.Usage;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using Api.SharedKernel.Services;
using MediatR;

namespace Api.Routing;

/// <summary>
/// User tier usage endpoints.
/// E2-2: Game Night Improvvisata - authenticated users can view their current usage vs. tier limits.
/// </summary>
internal static class UserUsageEndpoints
{
    public static RouteGroupBuilder MapUserUsageEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/users/me/usage", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var userId = session!.User!.Id;

            var query = new GetUserUsageQuery(userId);
            var usage = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(usage);
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetMyUsage")
        .WithTags("User", "Usage")
        .WithSummary("Get current tier usage for authenticated user")
        .WithDescription(@"Returns a snapshot of the authenticated user's current resource usage against their tier limits.

**E2-2**: Game Night Improvvisata - User Usage Endpoint.

**Authorization**: Requires active session (cookie-based authentication).

**Response**: UsageSnapshot with current counts and maximums for all tier-limited resources:
- Private games count vs. max
- PDF uploads this month vs. max
- Agent queries today vs. max
- Session queries vs. max
- Agents count vs. max
- Photos this session vs. max
- Session save enabled flag
- Catalog proposals this week vs. max")
        .Produces<UsageSnapshot>(200)
        .Produces(401);

        return group;
    }
}
