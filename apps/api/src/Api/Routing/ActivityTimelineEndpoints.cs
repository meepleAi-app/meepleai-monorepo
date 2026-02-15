using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Activity timeline API endpoint with page/pageSize pagination
/// and date range filtering (Issue #4315).
/// </summary>
internal static class ActivityTimelineEndpoints
{
    public static RouteGroupBuilder MapActivityTimelineEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/activity/timeline", HandleGetActivityTimeline)
            .RequireSession()
            .RequireAuthorization()
            .WithName("GetActivityTimelinePaged")
            .WithTags("Activity")
            .WithSummary("Get user activity timeline with page-based pagination and date range filters")
            .WithDescription(@"Returns a chronological timeline of user activities with page-based pagination.

**Event Types**: `game_added`, `session_completed`, `chat_saved`, `wishlist_added`
**Filtering**: `type` parameter accepts comma-separated event types
**Search**: `search` parameter performs case-insensitive partial match on game name/title/topic
**Date Range**: `dateFrom` and `dateTo` for time-bounded queries (max 1 year range)
**Pagination**: `page` (default: 1) and `pageSize` (default: 20, max: 100)
**Sorting**: `order` parameter accepts `asc` or `desc` (default: `desc`)

**Performance**: Target latency < 500ms with 5-minute cache.")
            .Produces<ActivityTimelineResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi();

        return group;
    }

    private static async Task<IResult> HandleGetActivityTimeline(
        HttpContext context,
        IMediator mediator,
        string? type = null,
        string? search = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int page = 1,
        int pageSize = 20,
        string? order = null,
        CancellationToken ct = default)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        // Parse comma-separated type filter
        string[]? types = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            types = type.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // Parse sort direction
        var sortDirection = string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase)
            ? SortDirection.Ascending
            : SortDirection.Descending;

        // Convert page/pageSize to skip/take
        var clampedPage = Math.Max(1, page);
        var clampedPageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (clampedPage - 1) * clampedPageSize;

        var query = new GetActivityTimelineQuery(
            UserId: session.User!.Id,
            Types: types,
            SearchTerm: search,
            DateFrom: dateFrom,
            DateTo: dateTo,
            Skip: skip,
            Take: clampedPageSize,
            Order: sortDirection);

        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Json(result);
    }
}
