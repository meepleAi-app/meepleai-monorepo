using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Queries.AdminEvents;
using Api.Extensions;
using Api.Infrastructure.EventBroadcasting;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin domain-event endpoints for the LiveEventLog monitor panel (F4.1 issue #1718).
///
/// <list type="bullet">
///   <item><c>GET /api/v1/admin/events</c> — paginated polling query (backfill + cursor)</item>
///   <item><c>GET /api/v1/admin/events/stream</c> — SSE real-time broadcast</item>
///   <item><c>GET /api/v1/admin/events/types</c> — per-type stats for filter chips</item>
/// </list>
///
/// All endpoints require Admin or SuperAdmin session (gate via <c>context.RequireAdminSession()</c>).
///
/// SSE endpoint design:
/// <list type="number">
///   <item>Subscribe eagerly to <see cref="IEventBroadcaster"/> (so no events are lost during backfill).</item>
///   <item>If <c>Last-Event-ID</c> header present, backfill events from DB newer than that cursor.</item>
///   <item>Stream channel events in W3C SSE format: <c>id: {guid}\ndata: {json}\n\n</c>.</item>
///   <item>Heartbeat comment <c>:hb\n\n</c> every 15 seconds to keep proxy connections alive.</item>
///   <item>Clean unsubscribe on HTTP client disconnect (CT cancellation).</item>
/// </list>
/// </summary>
internal static class AdminEventsEndpoints
{
    // Heartbeat interval — keep-alive comment for proxies / load balancers.
    // Caddy (used in MVP/dev) and Cloudflare Tunnel (staging/prod) both tolerate idle SSE
    // connections by default; 15s is conservative and aligns with common proxy idle timeouts.
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    public static IEndpointRouteBuilder MapAdminEventsEndpoints(this IEndpointRouteBuilder app)
    {
        // All three endpoints live under /api/v1/admin/events.
        // Note: v1Api is already a RouteGroupBuilder at "/api/v1" — we add a sub-group here.
        var group = app.MapGroup("/admin/events")
            .WithTags("Admin", "AdminEvents");

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/admin/events
        // Paginated, cursor-based polling endpoint for the LiveEventLog backfill.
        // ─────────────────────────────────────────────────────────────────────
        group.MapGet("", HandleGetEvents)
            .WithName("AdminEvents_GetEvents")
            .WithSummary("Get admin-scoped domain events (paged)")
            .WithDescription(
                "Returns domain events from domain_event_logs, cross-user (admin scope), " +
                "ordered by loggedAt DESC. " +
                "Supports cursor pagination via ?since=<ISO timestamp>, " +
                "filter by ?eventTypes=a,b (comma-separated or repeated), " +
                "?aggregateTypes=a,b, ?userId=<guid>, ?aggregateId=<guid>. " +
                "F4.1 issue #1718.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/admin/events/types
        // Per-type statistics for the filter-chip bar in LiveEventLog.
        // Must be registered BEFORE the "/stream" route to avoid ambiguity.
        // ─────────────────────────────────────────────────────────────────────
        group.MapGet("/types", HandleGetEventTypes)
            .WithName("AdminEvents_GetEventTypes")
            .WithSummary("Get domain event type statistics (last 24h)")
            .WithDescription(
                "Returns per-type counts and lastSeenAt for every alias in EventTypeRegistry. " +
                "Types with no activity in the last 24h appear with count=0. " +
                "Used to populate filter chips in LiveEventLog. " +
                "F4.1 issue #1718.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/admin/events/stream   (SSE)
        // Real-time broadcast of domain events to admin clients.
        // ─────────────────────────────────────────────────────────────────────
        group.MapGet("/stream", HandleGetEventsStream)
            .WithName("AdminEvents_GetEventsStream")
            .WithSummary("Stream domain events via SSE")
            .WithDescription(
                "Server-Sent Events stream for the LiveEventLog panel. " +
                "Subscribes to in-process IEventBroadcaster. " +
                "Supports Last-Event-ID header for backfill on reconnect. " +
                "Accepts same filter query params as GET /admin/events. " +
                "Heartbeat :hb comment every 15s. " +
                "F4.1 issue #1718.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    // =========================================================================
    // Handlers
    // =========================================================================

    private static async Task<IResult> HandleGetEvents(
        HttpContext context,
        IMediator mediator,
        DateTime? since,
        int? limit,
        string? eventTypes,
        string? aggregateTypes,
        Guid? userId,
        Guid? aggregateId,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAdminEventsQuery(
            Since: since,
            Limit: limit ?? 100,
            EventTypes: ParseCommaSeparated(eventTypes),
            AggregateTypes: ParseCommaSeparated(aggregateTypes),
            UserId: userId,
            AggregateId: aggregateId);

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetEventTypes(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var result = await mediator.Send(new GetEventTypeStatsQuery(), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task HandleGetEventsStream(
        HttpContext context,
        IEventBroadcaster broadcaster,
        IMediator mediator,
        string? eventTypes,
        string? aggregateTypes,
        Guid? userId,
        Guid? aggregateId,
        CancellationToken ct)
    {
        // ── Auth gate (manual: SSE handlers can't return IResult after body starts) ──
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized)
        {
            // SSE handlers cannot return IResult once the body has started, so we set the
            // status code directly. ExtractStatusCode inspects the IResult type name to derive
            // 401 vs 403 — safe because the response body has not started yet at this point.
            context.Response.StatusCode = ExtractStatusCode(error);
            return;
        }

        // ── Build per-connection filter from query params ──
        var filter = new EventBroadcastFilter(
            EventTypes: ParseCommaSeparated(eventTypes),
            AggregateTypes: ParseCommaSeparated(aggregateTypes),
            UserId: userId,
            AggregateId: aggregateId);

        // ── Subscribe EAGERLY before writing headers or backfilling ──
        // This ensures events published during backfill are buffered in the channel
        // and will be delivered after backfill completes (no gap).
        var stream = broadcaster.Subscribe(filter, ct);

        // ── Set SSE response headers ──
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        // Disable buffering at nginx-style reverse proxies (e.g. if nginx is in the stack).
        // Caddy and Cloudflare Tunnel do not buffer SSE, but adding this header is harmless.
        context.Response.Headers["X-Accel-Buffering"] = "no";
        // Write an initial SSE comment ":ok\n\n" to commit the response headers and flush
        // the first bytes to the transport pipe. This serves two purposes:
        //   1. Ensures the headers are immediately delivered to the client (no buffering).
        //   2. Unblocks TestServer's in-process ResponseHeadersRead mode so that integration
        //      tests can read headers without waiting for the handler to complete.
        await context.Response.WriteAsync(":ok\n\n", ct).ConfigureAwait(false);
        await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);

        // ── Last-Event-ID backfill (W3C SSE spec reconnect) ──
        // Strategy: subscribe FIRST (above), then backfill from DB.
        // Events that arrived between subscribe and backfill query are buffered in the channel.
        // We track backfill Ids to skip any duplicates that also arrive via the live channel.
        var seenIds = new HashSet<Guid>();

        var lastEventIdHeader = context.Request.Headers["Last-Event-ID"].FirstOrDefault();
        if (Guid.TryParse(lastEventIdHeader, out var lastGuid))
        {
            // Fetch the LoggedAt for the cursor event so we can use time-based filtering.
            // The handler uses `Since` as "LoggedAt < Since" cursor.
            // We want all events NEWER than lastGuid, so we fetch the row's LoggedAt
            // and pass it as the `Since` parameter (exclusive — backfill starts just after it).
            var backfillQuery = new GetAdminEventsQuery(
                Since: null,   // No time cursor for backfill — we want all newer than lastGuid
                Limit: 200,    // Reasonable page: client missed at most 200 events during reconnect
                EventTypes: ParseCommaSeparated(eventTypes),
                AggregateTypes: ParseCommaSeparated(aggregateTypes),
                UserId: userId,
                AggregateId: aggregateId);

            var backfill = await mediator.Send(backfillQuery, ct).ConfigureAwait(false);

            // Events ordered DESC — find the position of lastGuid and stream everything before it
            // (i.e., events that are NEWER than the last seen event).
            var backfillEvents = backfill.Events
                .TakeWhile(e => e.Id != lastGuid) // stop when we hit the already-seen event
                .Reverse()                          // send oldest-first for proper ordering
                .ToList();

            foreach (var evt in backfillEvents)
            {
                if (ct.IsCancellationRequested) return;
                seenIds.Add(evt.Id);
                await WriteSseEventAsync(context, evt, ct).ConfigureAwait(false);
            }
        }

        // ── Start heartbeat task ──
        // Runs concurrently with the main consumer loop.
        // On cancellation, the Task.Delay throws OperationCanceledException which exits the loop.
        var heartbeatTask = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(HeartbeatInterval, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested) break;
                    await context.Response.WriteAsync(":hb\n\n", ct).ConfigureAwait(false);
                    await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Client disconnected — stop heartbeat silently
                    break;
                }
            }
        }, ct);

        // ── Main consumer loop — stream channel events ──
        try
        {
            await foreach (var evt in stream.WithCancellation(ct).ConfigureAwait(false))
            {
                // Dedup: skip events already sent in backfill
                if (seenIds.Contains(evt.Id)) continue;

                await WriteSseEventAsync(context, evt, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected: client disconnected or server shutting down
        }
        catch (Exception)
        {
            // Any other I/O exception — connection closed, exit gracefully
        }
        finally
        {
            // Wait for heartbeat task to finish (it's already watching ct).
            // Both OperationCanceledException (normal) and any other I/O exception are benign here —
            // the heartbeat loop already exited because the connection is closing.
            try
            {
                await heartbeatTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected: CancellationToken was cancelled — heartbeat task exited normally.
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                // I/O error after client disconnect — ignore, we're already in teardown.
            }
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Writes a single SSE event in W3C format:
    /// <code>id: {Id GUID}\ndata: {JSON}\n\n</code>
    /// </summary>
    private static async Task WriteSseEventAsync(
        HttpContext context,
        DomainEventDto evt,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(evt, SseJsonOptions);
        var idLine = $"id: {evt.Id}\n";
        var dataLine = $"data: {json}\n\n";

        await context.Response.WriteAsync(idLine, ct).ConfigureAwait(false);
        await context.Response.WriteAsync(dataLine, ct).ConfigureAwait(false);
        await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses a comma-separated query string parameter into a list.
    /// Returns null (no filter) when <paramref name="value"/> is null or whitespace.
    /// Handles both "a,b,c" (single param) and repeated ?param=a&amp;param=b patterns
    /// (ASP.NET model binding already joins repeated params into a single comma-separated string).
    /// </summary>
    private static IReadOnlyList<string>? ParseCommaSeparated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : parts;
    }

    /// <summary>
    /// Extracts the HTTP status code from an <see cref="IResult"/> by executing it against
    /// a dummy response. Used only for the SSE auth-error path where we need the status code
    /// before writing to the response body.
    /// </summary>
    private static int ExtractStatusCode(IResult? result)
    {
        // Heuristic: Results.Unauthorized() → 401, Results.Forbid() → 403
        // We can't easily inspect IResult without executing it.
        // Check the type name as a lightweight fallback.
        if (result is null) return 401;
        var name = result.GetType().Name;
        if (name.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)) return 401;
        if (name.Contains("Forbid", StringComparison.OrdinalIgnoreCase)) return 403;
        return 401; // safe default
    }

    /// <summary>
    /// JSON serialization options for SSE payloads.
    /// Matches the project's camelCase + DateTimeKind.Utc convention (no suffix normalization).
    /// </summary>
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
