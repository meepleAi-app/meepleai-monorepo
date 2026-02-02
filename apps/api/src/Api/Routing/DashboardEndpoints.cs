using System.Text.Json;
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

        // Issue #3324: SSE real-time dashboard stream
        MapDashboardStreamEndpoint(group);

        return group;
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
}
