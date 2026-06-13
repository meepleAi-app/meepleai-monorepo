using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Extensions;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing.Admin;

/// <summary>
/// Issue #1823 Wave 3 M12 — admin endpoint for the Wikidata cover enrichment
/// pipeline. Routes under <c>/api/v1/admin/wikidata/enrichment</c>. Admin-only
/// (gated by <see cref="RequireAdminSessionFilter"/>).
/// </summary>
/// <remarks>
/// <para>Endpoint summary:</para>
/// <list type="bullet">
///   <item><c>POST /{gameId:guid}</c> — manually trigger enrichment for one
///   shared game. Body: <c>{ "forceRefresh": bool }</c>. Returns the flattened
///   <see cref="AdminEnrichWikidataCoverResult"/> with outcome <c>"success"</c>
///   / <c>"skipped"</c> / <c>"failed"</c>.</item>
/// </list>
/// <para>CQRS compliance: dispatches exclusively via <see cref="IMediator"/>.</para>
/// </remarks>
internal static class AdminWikidataCoverEnrichmentEndpoints
{
    public static RouteGroupBuilder MapAdminWikidataCoverEnrichmentEndpoints(this RouteGroupBuilder group)
    {
        // Mirror of AdminCatalogSeedEndpoints: group-level admin auth filter.
        // Anonymous probes get 401; non-admin sessions get 403 BEFORE the
        // route handler runs.
        group.AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapPost("/{gameId:guid}", HandleTrigger)
            .WithName("AdminWikidataCoverEnrichment_Trigger")
            .WithTags("Admin", "WikidataCoverEnrichment");

        group.MapGet("/dead-letters", HandleListDeadLetters)
            .WithName("AdminWikidataCoverEnrichment_ListDeadLetters")
            .WithTags("Admin", "WikidataCoverEnrichment");

        // Phase E F2 — bulk-retry endpoint.
        group.MapPost("/bulk-retry", HandleBulkRetry)
            .WithName("AdminWikidataCoverEnrichment_BulkRetry")
            .WithTags("Admin", "WikidataCoverEnrichment");

        // Phase F F5 (#2254) — bulk-acknowledge endpoint.
        group.MapPost("/bulk-acknowledge", HandleBulkAcknowledge)
            .WithName("AdminWikidataCoverEnrichment_BulkAcknowledge")
            .WithTags("Admin", "WikidataCoverEnrichment");

        // Phase E F3 — per-game attempt timeline (drawer payload).
        group.MapGet("/games/{gameId:guid}/attempts", HandleGetAttemptTimeline)
            .WithName("AdminWikidataCoverEnrichment_GetAttemptTimeline")
            .WithTags("Admin", "WikidataCoverEnrichment");

        // Phase E F4 — SSE stream of attempt-recorded events for live admin
        // dead-letter row updates.
        group.MapGet("/events", HandleEventsStream)
            .WithName("AdminWikidataCoverEnrichment_EventsStream")
            .WithTags("Admin", "WikidataCoverEnrichment");

        return group;
    }

    /// <summary>
    /// Request body for the trigger endpoint. <see cref="ForceRefresh"/> is
    /// optional — null is treated as <see langword="false"/>.
    /// </summary>
    internal sealed record AdminTriggerWikidataEnrichmentRequest(bool? ForceRefresh);

    private static async Task<IResult> HandleTrigger(
        Guid gameId,
        AdminTriggerWikidataEnrichmentRequest? request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        // Auth already enforced at the group level via RequireAdminSessionFilter.
        // GameId is parsed by the route constraint :guid — an invalid GUID
        // returns 400 BEFORE this handler runs. The request body is optional;
        // null body is treated as forceRefresh=false.
        var command = new AdminEnrichWikidataCoverCommand(
            GameId: gameId,
            ForceRefresh: request?.ForceRefresh ?? false,
            TriggeredByUserId: context.User.GetUserId());

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    /// <summary>
    /// Issue #1823 Wave 3 M13 — paginated dead-letter visibility for the admin
    /// page. Query string: <c>?skip=0&amp;take=50&amp;reason=r2-upload-error&amp;includeAcknowledged=true</c>.
    /// Phase F (#2254) added the optional <c>includeAcknowledged</c> toggle so
    /// the admin UI can switch between the default open-work view (hide acked
    /// rows) and the historical audit view (show everything).
    /// </summary>
    private static async Task<IResult> HandleListDeadLetters(
        [FromQuery] int? skip,
        [FromQuery] int? take,
        [FromQuery] string? reason,
        [FromQuery] bool? includeAcknowledged,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetWikidataDeadLetterAttemptsQuery(
            Skip: skip ?? 0,
            Take: take ?? 50,
            ReasonFilter: string.IsNullOrWhiteSpace(reason) ? null : reason,
            IncludeAcknowledged: includeAcknowledged ?? false);

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    /// <summary>Body for the F2 bulk-retry endpoint.</summary>
    internal sealed record AdminBulkRetryWikidataRequest(IReadOnlyList<Guid>? AttemptIds);

    /// <summary>
    /// Issue #1823 Phase E F2 — bulk re-trigger of one or more dead-letter
    /// attempts. Each id is resolved to its parent SharedGameId and dispatched
    /// via the M9 runner with <c>forceRefresh=true</c>. Returns a per-row
    /// envelope so the admin UI can render partial success/failure.
    /// </summary>
    private static async Task<IResult> HandleBulkRetry(
        AdminBulkRetryWikidataRequest? request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new AdminBulkRetryWikidataCoverCommand(
            AttemptIds: request?.AttemptIds ?? Array.Empty<Guid>(),
            TriggeredByUserId: context.User.GetUserId());

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    /// <summary>Body for the F5 bulk-acknowledge endpoint.</summary>
    internal sealed record AdminBulkAcknowledgeWikidataRequest(
        IReadOnlyList<Guid>? AttemptIds,
        string? Note);

    /// <summary>
    /// Issue #1823 Phase F F5 (#2254) — bulk-acknowledge one or more dead-letter
    /// attempts. Each id is hydrated by the handler, mutated via
    /// <c>WikidataCoverEnrichmentAttempt.Acknowledge(by, at)</c> (idempotent on
    /// re-call) and persisted via the unit of work. Returns a per-row envelope
    /// so the admin UI can render partial success/failure (acked /
    /// already-acked / not-found / wrong-state).
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="HandleBulkRetry"/> in shape: nullable body, defensive
    /// fallback to <see cref="Array.Empty{T}"/>, dispatch via MediatR (CQRS —
    /// zero direct service injection). The optional <c>Note</c> is DEC-F-4
    /// log-only (not persisted on the attempt row).
    /// </remarks>
    private static async Task<IResult> HandleBulkAcknowledge(
        AdminBulkAcknowledgeWikidataRequest? request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: request?.AttemptIds ?? Array.Empty<Guid>(),
            Note: request?.Note,
            TriggeredByUserId: context.User.GetUserId());

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    /// <summary>
    /// Issue #1823 Phase E F3 — per-game attempt timeline. Query string:
    /// <c>?limit=20</c> (default 50, max 200).
    /// </summary>
    private static async Task<IResult> HandleGetAttemptTimeline(
        Guid gameId,
        [FromQuery] int? limit,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetWikidataAttemptTimelineQuery(
            GameId: gameId,
            Limit: limit ?? 50);

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static readonly TimeSpan SseHeartbeatInterval = TimeSpan.FromSeconds(15);

    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Issue #1823 Phase E F4 — SSE stream of attempt-recorded events. Each
    /// event is a single JSON-serialised <see cref="WikidataEnrichmentEvent"/>.
    /// </summary>
    /// <remarks>
    /// Mirror of the <see cref="Api.Routing.AdminEventsEndpoints"/> pattern:
    /// 15s <c>:hb\n\n</c> heartbeat keeps proxies / load balancers from
    /// closing the connection, <c>X-Accel-Buffering: no</c> defeats nginx
    /// buffering if it ever lands in the stack, the body is committed
    /// immediately with <c>:ok\n\n</c> so the client sees headers without
    /// waiting for the first event.
    /// </remarks>
    private static async Task HandleEventsStream(
        HttpContext context,
        IWikidataEnrichmentEventBroadcaster broadcaster,
        CancellationToken ct)
    {
        // Group-level RequireAdminSessionFilter has already authorised the
        // request — SSE handlers can't return IResult once the body has
        // started, so any auth failure would have happened before we got
        // here.

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        // Commit headers + flush so the client sees the open stream
        // immediately (and so TestServer's ResponseHeadersRead mode unblocks).
        await context.Response.WriteAsync(":ok\n\n", ct).ConfigureAwait(false);
        await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);

        var heartbeatTask = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(SseHeartbeatInterval, ct).ConfigureAwait(false);
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
                    // Client disconnected — stop heartbeat silently.
                    break;
                }
            }
        }, ct);

        try
        {
            await foreach (var payload in broadcaster.SubscribeAsync(ct).ConfigureAwait(false))
            {
                var json = JsonSerializer.Serialize(payload, SseJsonOptions);
                await context.Response.WriteAsync($"event: attempt-recorded\ndata: {json}\n\n", ct)
                    .ConfigureAwait(false);
                await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected: client disconnected or server shutting down.
        }
        finally
        {
            try
            {
                await heartbeatTask.ConfigureAwait(false);
            }
            catch
            {
                // Heartbeat task swallows its own errors; rethrowing here would
                // mask the cancellation that triggered the finally.
            }
        }
    }
}
