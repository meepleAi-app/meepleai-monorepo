using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Routing;

/// <summary>
/// User dashboard API endpoints (Issue #3314).
/// Provides aggregated dashboard data for authenticated users.
/// </summary>
internal static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder group)
    {
        // Issue #3314: Dashboard aggregated endpoint
        group.MapGet("/dashboard", HandleGetDashboard)
            .RequireSession()
            .RequireAuthorization()
            .WithName("GetDashboard")
            .WithTags("Dashboard")
            .WithSummary("Get user dashboard data")
            .WithDescription(@"Returns aggregated dashboard data for the authenticated user including:
- User info (name, last access)
- Stats (collection, played, chats, wishlist)
- Active sessions
- Library snapshot with quota and top games
- Recent activity timeline
- Recent chat threads

**Performance**: Target latency < 500ms (p99) with 5-minute Redis cache.

**Authorization**: Requires active session (cookie-based authentication).")
            .Produces<DashboardResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi();

        return group;
    }

    private static async Task<IResult> HandleGetDashboard(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct = default)
    {
        // Session validated by RequireSession filter
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        var query = new GetDashboardQuery(session.User!.Id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Json(result);
    }
}
