using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Queries.UserStats;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.Sessions;
using Api.BoundedContexts.UserLibrary.Application.Queries.Games;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

// Issue #3319: Dashboard insights response DTO

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Routing;

/// <summary>
/// User dashboard API endpoints (Issue #3314, #3972).
/// Provides aggregated dashboard data for authenticated users.
/// SSE real-time updates (Issue #3324).
/// </summary>
internal static class DashboardEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder group)
    {
        // Epic #4575: Gaming Hub Dashboard endpoints
        MapGamingHubEndpoints(group);

        // Issue #3314: Dashboard aggregated endpoint (V1 - deprecated)
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

        // Issue #3319: AI-powered dashboard insights
        group.MapGet("/dashboard/insights", HandleGetDashboardInsights)
            .RequireSession()
            .RequireAuthorization()
            .WithName("GetDashboardInsights")
            .WithTags("Dashboard")
            .WithSummary("Get AI-powered insights for user dashboard")
            .WithDescription(@"Returns personalized AI insights based on user's library, play history, and activity.

**Insight Types**:
- **Backlog**: Games not played for 30+ days
- **RulesReminder**: Recently saved chat rules
- **Recommendation**: Games similar to user's favorites
- **Streak**: Play streak encouragement
- **Achievement**: Progress toward achievements

**Performance**: 15-minute Redis cache for optimal response times.

**Authorization**: Requires active session (cookie-based authentication).")
            .Produces<DashboardInsightsResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi();

        // Issue #4124: AI insight accuracy metrics
        group.MapGet("/dashboard/insights/accuracy", HandleGetInsightAccuracy)
            .RequireSession()
            .RequireAuthorization()
            .WithName("GetInsightAccuracy")
            .WithTags("Dashboard")
            .WithSummary("Get AI insight accuracy metrics from user feedback")
            .WithDescription(@"Returns accuracy metrics calculated from user feedback on AI insights.
Accuracy = (relevant feedback / total feedback) × 100. Target: >75%.

**Breakdown**: Overall accuracy + per insight type breakdown.

**Authorization**: Requires active session (cookie-based authentication).")
            .Produces<InsightAccuracyResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi();

        // Issue #4124: AI insight feedback for accuracy tracking
        group.MapPost("/dashboard/insights/feedback", HandleSubmitInsightFeedback)
            .RequireSession()
            .RequireAuthorization()
            .WithName("SubmitInsightFeedback")
            .WithTags("Dashboard")
            .WithSummary("Submit feedback on a dashboard AI insight")
            .WithDescription(@"Allows users to rate the relevance of AI-generated insights.
Feedback is used to calculate accuracy metrics (target: >75% relevance).

**Request Body**:
- `insightId`: The insight ID from the insights response
- `insightType`: One of Backlog, RulesReminder, Recommendation, Streak, Achievement
- `isRelevant`: Whether the insight was useful/relevant to the user
- `comment`: Optional text feedback (max 500 chars)

**Authorization**: Requires active session (cookie-based authentication).")
            .Produces<InsightFeedbackResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi();

        // Issue #3973, #3923: Activity timeline with advanced filters
        group.MapGet("/dashboard/activity-timeline", HandleGetActivityTimeline)
            .RequireSession()
            .RequireAuthorization()
            .WithName("GetActivityTimeline")
            .WithTags("Dashboard")
            .WithSummary("Get user activity timeline with filters, search, and pagination")
            .WithDescription(@"Returns a chronological timeline of user activities aggregated from multiple sources:
- **game_added**: Games added to library
- **session_completed**: Game sessions played
- **chat_saved**: Chat conversations
- **wishlist_added**: Wishlist additions

**Filtering**: `type` parameter accepts comma-separated event types (e.g., `type=game_added,session_completed`)
**Search**: `search` parameter performs case-insensitive partial match on game name/title/topic
**Pagination**: `skip` (default: 0) and `take` (default: 20, max: 100)
**Sorting**: `order` parameter accepts `asc` or `desc` (default: `desc`)

**Performance**: Target latency < 500ms with 5-minute cache.")
            .Produces<ActivityTimelineResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi();

        // Issue #3324: SSE real-time dashboard stream
        MapDashboardStreamEndpoint(group);

        return group;
    }

    /// <summary>
    /// Epic #4575: Gaming Hub Dashboard endpoints (Issues #4578, #4579, #4580)
    /// </summary>
    private static void MapGamingHubEndpoints(RouteGroupBuilder group)
    {
        // Issue #4578: User stats
        group.MapGet("/users/me/stats", async (
            IMediator mediator,
            CancellationToken ct) =>
        {
            var stats = await mediator.Send(new GetUserStatsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(stats);
        })
        .RequireAuthorization()
        .WithTags("Dashboard", "Gaming Hub")
        .WithName("GetUserStats")
        .WithSummary("Get user dashboard statistics")
        .Produces<UserStatsDto>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Issue #4579: Recent sessions
        group.MapGet("/sessions/recent", async (
            [FromQuery] int limit,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetRecentSessionsQuery { Limit = limit == 0 ? 3 : limit };
            var sessions = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(sessions);
        })
        .RequireAuthorization()
        .WithTags("Dashboard", "Gaming Hub", "Sessions")
        .WithName("GetRecentSessions")
        .WithSummary("Get recent gaming sessions")
        .Produces<List<SessionSummaryDto>>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Issue #4580: User games collection
        group.MapGet("/users/me/games", async (
            [FromQuery] string? category,
            [FromQuery] string? sort,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetUserGamesQuery
            {
                Category = category,
                Sort = sort ?? "alphabetical",
                Page = page == 0 ? 1 : page,
                PageSize = pageSize == 0 ? 20 : pageSize
            };

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Dashboard", "Gaming Hub", "User Library")
        .WithName("GetUserGames")
        .WithSummary("Get user's game collection")
        .Produces<PagedResult<UserGameDto>>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// Maps the SSE stream endpoint for real-time dashboard updates.
    /// </summary>
    private static void MapDashboardStreamEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/dashboard/stream", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Session validated by RequireSession filter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var userId = session.User!.Id;

            // Get event stream via CQRS query
            IAsyncEnumerable<MediatR.INotification> eventStream;
            try
            {
                var query = new GetDashboardStreamQuery(userId);
                eventStream = await mediator.Send(query, ct).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(403);
            }

            // Set SSE response headers
            context.Response.Headers.Append("Content-Type", "text/event-stream");
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("Connection", "keep-alive");
            context.Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx/Traefik buffering

            // Create heartbeat task for keep-alive (30s interval)
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
                    var json = JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions);

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
        .RequireSession()
        .RequireAuthorization()
        .WithName("StreamDashboardEvents")
        .WithTags("Dashboard", "Real-Time")
        .WithSummary("Subscribe to real-time dashboard events via SSE")
        .WithDescription(@"Server-Sent Events stream for real-time dashboard updates.
Use the EventSource API in browsers to connect.

**Event Types**:
- `DashboardStatsUpdatedEvent`: Stats changes (collection, played, sessions)
- `DashboardActivityEvent`: New activity (game added, PDF uploaded, etc.)
- `DashboardSessionUpdatedEvent`: Game session updates
- `DashboardNotificationEvent`: User notifications
- `heartbeat`: Keep-alive signal every 30 seconds

**Example**:
```javascript
const eventSource = new EventSource('/api/v1/dashboard/stream', { withCredentials: true });
eventSource.addEventListener('DashboardStatsUpdatedEvent', (e) => {
  const data = JSON.parse(e.data);
  console.log('Stats updated:', data);
});
```")
        .Produces(200)
        .Produces(401);
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

    private static async Task<IResult> HandleGetActivityTimeline(
        HttpContext context,
        IMediator mediator,
        string? type = null,
        string? search = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int skip = 0,
        int take = 20,
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

        var query = new GetActivityTimelineQuery(
            UserId: session.User!.Id,
            Types: types,
            SearchTerm: search,
            DateFrom: dateFrom,
            DateTo: dateTo,
            Skip: Math.Max(0, skip),
            Take: Math.Clamp(take, 1, 100),
            Order: sortDirection);

        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Json(result);
    }

    private static async Task<IResult> HandleGetDashboardInsights(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct = default)
    {
        // Session validated by RequireSession filter
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        var query = new GetDashboardInsightsQuery(session.User!.Id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Json(result);
    }

    private static async Task<IResult> HandleGetInsightAccuracy(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var query = new GetInsightAccuracyQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Json(result);
    }

    private static async Task<IResult> HandleSubmitInsightFeedback(
        HttpContext context,
        IMediator mediator,
        InsightFeedbackRequestDto request,
        CancellationToken ct = default)
    {
        // Session validated by RequireSession filter
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

        var command = new SubmitInsightFeedbackCommand
        {
            UserId = session.User!.Id,
            InsightId = request.InsightId,
            InsightType = request.InsightType,
            IsRelevant = request.IsRelevant,
            Comment = request.Comment
        };

        var feedbackId = await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.Json(new InsightFeedbackResponseDto(feedbackId));
    }
}

/// <summary>
/// Request DTO for insight feedback submission (Issue #4124).
/// </summary>
public record InsightFeedbackRequestDto(
    string InsightId,
    string InsightType,
    bool IsRelevant,
    string? Comment = null
);

/// <summary>
/// Response DTO for insight feedback submission (Issue #4124).
/// </summary>
public record InsightFeedbackResponseDto(Guid FeedbackId);
