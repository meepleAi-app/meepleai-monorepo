using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Extensions;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing.Admin;

/// <summary>
/// Issue #1903 M6.2 — Admin endpoints for the catalog seed pipeline. Routes
/// under <c>/api/v1/admin/catalog/seeds</c>. All endpoints require an Admin
/// session.
/// </summary>
/// <remarks>
/// <para>CQRS compliance: every handler dispatches via <see cref="IMediator"/>;
/// the only non-mediator dependency is <see cref="ICatalogSeedStreamService"/>
/// for the SSE endpoint where streaming is an HTTP-level concern, not domain
/// logic.</para>
/// <para>Endpoint summary:</para>
/// <list type="bullet">
/// <item><c>POST    /</c> — enqueue a single seed draft</item>
/// <item><c>POST    /bulk</c> — bulk enqueue (max 100 BGG ids)</item>
/// <item><c>GET     /</c> — paginated list (filter by status)</item>
/// <item><c>GET     /{id}</c> — single draft detail (incl. ProvenanceJson)</item>
/// <item><c>POST    /{id}/approve</c> — transition Fetched → Approved</item>
/// <item><c>POST    /{id}/reject</c> — soft-delete with reason</item>
/// <item><c>GET     /stream</c> — SSE: batch + per-item events</item>
/// <item><c>POST    /wikidata-search</c> — proxy SPARQL search for the UI picker</item>
/// </list>
/// </remarks>
internal static class AdminCatalogSeedEndpoints
{
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static RouteGroupBuilder MapAdminCatalogSeedEndpoints(this RouteGroupBuilder group)
    {
        // Filter chain order MATTERS. Filters added via AddEndpointFilter execute
        // in REGISTRATION order on the request (first-added runs first).
        //
        //   1. RequireAdminSessionFilter — gates the entire pipeline behind a
        //      valid Admin session. Unauthenticated callers get 401, non-admins
        //      get 403, BEFORE the feature-flag check runs. This prevents the
        //      info-disclosure leak where an anonymous probe could distinguish
        //      "endpoint exists + flag off" (503) from "endpoint missing" (404).
        //
        //   2. Feature flag kill-switch — once auth is established, return 503
        //      Service Unavailable when the runtime flag is disabled. Covers
        //      all REST methods AND the SSE stream; the SSE handler writes
        //      the status code directly on the response when reached without
        //      a flag block, before the body opens.
        group.AddEndpointFilter<RequireAdminSessionFilter>();

        // Issue #1903 M7.2: kill-switch endpoint filter. Runs AFTER auth, so
        // 503 is only ever observable by admins (never by anonymous probes).
        group.AddEndpointFilter(async (filterContext, next) =>
        {
            var flag = filterContext.HttpContext.RequestServices
                .GetService<ICatalogSeedFeatureFlag>();
            if (flag is not null)
            {
                var enabled = await flag
                    .IsEnabledAsync(filterContext.HttpContext.RequestAborted)
                    .ConfigureAwait(false);
                if (!enabled)
                {
                    return Results.Problem(
                        title: "Catalog seed pipeline disabled",
                        detail: $"The admin catalog seed pipeline is disabled via runtime flag '{ICatalogSeedFeatureFlag.FlagKey}'.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            }
            return await next(filterContext).ConfigureAwait(false);
        });

        group.MapPost("/", HandleEnqueue)
            .WithName("AdminCatalogSeed_Enqueue")
            .WithTags("Admin", "CatalogSeed");

        group.MapPost("/bulk", HandleBulkEnqueue)
            .WithName("AdminCatalogSeed_BulkEnqueue")
            .WithTags("Admin", "CatalogSeed");

        group.MapGet("/", HandleList)
            .WithName("AdminCatalogSeed_List")
            .WithTags("Admin", "CatalogSeed");

        group.MapGet("/{id:guid}", HandleGetById)
            .WithName("AdminCatalogSeed_GetById")
            .WithTags("Admin", "CatalogSeed");

        group.MapPost("/{id:guid}/approve", HandleApprove)
            .WithName("AdminCatalogSeed_Approve")
            .WithTags("Admin", "CatalogSeed");

        group.MapPost("/{id:guid}/reject", HandleReject)
            .WithName("AdminCatalogSeed_Reject")
            .WithTags("Admin", "CatalogSeed");

        group.MapGet("/stream", HandleStream)
            .WithName("AdminCatalogSeed_Stream")
            .WithTags("Admin", "CatalogSeed")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream");

        group.MapPost("/wikidata-search", HandleWikidataSearch)
            .WithName("AdminCatalogSeed_WikidataSearch")
            .WithTags("Admin", "CatalogSeed");

        return group;
    }

    // =========================================================================
    // Handlers
    // =========================================================================

    private static async Task<IResult> HandleEnqueue(
        EnqueueCatalogSeedRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Auth + admin role already enforced by group-level
        // RequireAdminSessionFilter; we only need to extract the user id here.
        var command = new EnqueueCatalogSeedCommand(
            BggId: request.BggId,
            WikidataQid: request.WikidataQid,
            SearchTermInput: request.SearchTermInput,
            CreatedByUserId: context.User.GetUserId());

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleBulkEnqueue(
        BulkEnqueueRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Auth gated at group level (RequireAdminSessionFilter).
        var command = new BulkEnqueueCatalogSeedsCommand(
            BggIds: request.BggIds,
            CreatedByUserId: context.User.GetUserId());

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleList(
        [FromQuery] string? status,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        IMediator mediator,
        CancellationToken ct)
    {
        // Auth gated at group level (RequireAdminSessionFilter).
        var query = new ListCatalogSeedsQuery(
            StatusFilter: status,
            Skip: skip ?? 0,
            Take: take ?? 50);

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetById(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        // Auth gated at group level (RequireAdminSessionFilter).
        var dto = await mediator.Send(new GetCatalogSeedByIdQuery(id), ct).ConfigureAwait(false);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> HandleApprove(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        // Auth gated at group level (RequireAdminSessionFilter).
        var result = await mediator
            .Send(new ApproveCatalogSeedCommand(id, context.User.GetUserId()), ct)
            .ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleReject(
        Guid id,
        RejectRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Auth gated at group level (RequireAdminSessionFilter).
        await mediator
            .Send(new RejectCatalogSeedCommand(id, context.User.GetUserId(), request.Reason), ct)
            .ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task HandleStream(
        HttpContext context,
        ICatalogSeedStreamService stream,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Auth gated at group level (RequireAdminSessionFilter) — runs BEFORE
        // this handler is invoked, so response headers have not been committed
        // yet and the filter can safely set a 401/403 status code. No in-handler
        // re-check needed; the previous defense-in-depth call was removed when
        // the group filter was introduced (M7 review fix).
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        // Disable buffering at proxies; harmless on Caddy/Cloudflare Tunnel.
        context.Response.Headers["X-Accel-Buffering"] = "no";

        // Commit headers immediately so the client can start reading.
        await context.Response.WriteAsync(":ok\n\n", ct).ConfigureAwait(false);
        await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);

        try
        {
            await foreach (var evt in stream.SubscribeAsync(ct).ConfigureAwait(false))
            {
                var json = JsonSerializer.Serialize<object>(evt, SseJsonOptions);
                await context.Response.WriteAsync($"event: {evt.EventType}\n", ct).ConfigureAwait(false);
                await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected — client disconnected or server shutting down.
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Transport-layer I/O failure during shutdown / disconnect. Log at
            // Debug because OperationCanceledException + ObjectDisposedException
            // on disconnect is the expected path — anything noisier would spam
            // logs on every client navigation. Best-effort: connection may already
            // be torn down so the logger call itself could throw.
            try
            {
                logger.LogDebug(ex,
                    "CatalogSeed SSE stream terminated for subscriber (likely client disconnect)");
            }
#pragma warning disable CA1031
            catch
#pragma warning restore CA1031
            {
                // Logger itself failed — nothing more we can do.
            }
        }
    }

    private static async Task<IResult> HandleWikidataSearch(
        WikidataSearchRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Auth gated at group level (RequireAdminSessionFilter).
        var result = await mediator
            .Send(new WikidataSearchQuery(request.SearchTerm), ct)
            .ConfigureAwait(false);
        return Results.Ok(result);
    }
}

/// <summary>Request body for <c>POST /api/v1/admin/catalog/seeds</c>.</summary>
internal sealed record EnqueueCatalogSeedRequest(int? BggId, string? WikidataQid, string? SearchTermInput);

/// <summary>Request body for <c>POST /api/v1/admin/catalog/seeds/bulk</c>.</summary>
internal sealed record BulkEnqueueRequest(IReadOnlyList<int> BggIds);

/// <summary>Request body for <c>POST /api/v1/admin/catalog/seeds/{id}/reject</c>.</summary>
internal sealed record RejectRequest(string Reason);

/// <summary>Request body for <c>POST /api/v1/admin/catalog/seeds/wikidata-search</c>.</summary>
internal sealed record WikidataSearchRequest(string SearchTerm);
