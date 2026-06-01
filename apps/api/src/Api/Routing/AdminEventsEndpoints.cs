using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Queries.AdminEvents;
using Api.Extensions;
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
/// SSE endpoint design (CQRS-compliant — all subscription/backfill/dedup logic lives in
/// <see cref="GetAdminEventsStreamQueryHandler"/>):
/// <list type="number">
///   <item>Parse <c>Last-Event-ID</c> header and construct <see cref="GetAdminEventsStreamQuery"/>.</item>
///   <item>Dispatch via <c>IMediator.CreateStream</c> — handler subscribes eagerly to
///         <c>IEventBroadcaster</c>, backfills from DB if cursor is present, then streams live events.</item>
///   <item>Write each yielded <see cref="DomainEventDto"/> in W3C SSE format:
///         <c>id: {guid}\ndata: {json}\n\n</c>.</item>
///   <item>Heartbeat comment <c>:hb\n\n</c> every 15 seconds (transport-level, stays in endpoint).</item>
///   <item>Clean unsubscribe on HTTP client disconnect (CT cancellation propagated to handler).</item>
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

        // ── Parse Last-Event-ID for reconnect backfill (W3C SSE spec) ──
        var lastEventIdHeader = context.Request.Headers["Last-Event-ID"].FirstOrDefault();
        Guid? lastEventId = Guid.TryParse(lastEventIdHeader, out var parsed) ? parsed : null;

        // ── Build streaming query — handler owns subscribe-eagerly + backfill + dedup ──
        var query = new GetAdminEventsStreamQuery(
            LastEventId: lastEventId,
            EventTypes: ParseCommaSeparated(eventTypes),
            AggregateTypes: ParseCommaSeparated(aggregateTypes),
            UserId: userId,
            AggregateId: aggregateId);

        // ── Start heartbeat task (transport-level: SSE comment, not a domain event) ──
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

        // ── Main consumer loop — delegate all event logic to the stream handler ──
        try
        {
            await foreach (var evt in mediator.CreateStream(query, ct).ConfigureAwait(false))
            {
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
    /// <remarks>
    /// ISSUE-1741: Replace type-name heuristic with <c>IStatusCodeHttpResult.StatusCode</c>
    /// inspection (public since .NET 7) or restructure <c>RequireAdminSession</c> return type.
    /// Current heuristic works but relies on internal ASP.NET Core type names.
    /// </remarks>
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
