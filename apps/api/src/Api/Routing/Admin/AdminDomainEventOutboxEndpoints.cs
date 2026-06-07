using Api.BoundedContexts.Administration.Application.Commands.DomainEventOutbox;
using Api.BoundedContexts.Administration.Application.Queries.DomainEventOutbox;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing.Admin;

/// <summary>
/// Issue #1535 T6 — admin endpoints for the <c>domain_event_outbox</c> health surface.
/// Routes under <c>/api/v1/admin/event-outbox</c>. All endpoints require an Admin session
/// (group filter); per-handler responsibilities are strictly MediatR dispatch.
///
/// <para>Endpoint summary:</para>
/// <list type="bullet">
///   <item><c>GET  /stats</c> — aggregate snapshot (Pending / Failed / Sent24h / oldest age)</item>
///   <item><c>GET  /failed?limit=N</c> — top-N most-recent terminal Failed rows</item>
///   <item><c>GET  /pending?limit=N</c> — oldest-first Pending rows (queue head)</item>
///   <item><c>POST /{id}/retry</c> — operator-triggered Failed → Pending re-arm</item>
/// </list>
/// </summary>
internal static class AdminDomainEventOutboxEndpoints
{
    private const int DefaultListLimit = 50;

    public static RouteGroupBuilder MapAdminDomainEventOutboxEndpoints(this RouteGroupBuilder group)
    {
        // Group-level auth: matches the convention established by AdminCatalogSeedEndpoints.
        // Unauthenticated → 401, non-admin → 403, BEFORE any handler runs.
        group.AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapGet("/stats", HandleStats)
            .WithName("AdminDomainEventOutbox_Stats")
            .WithTags("Admin", "EventOutbox");

        group.MapGet("/failed", HandleFailed)
            .WithName("AdminDomainEventOutbox_Failed")
            .WithTags("Admin", "EventOutbox");

        group.MapGet("/pending", HandlePending)
            .WithName("AdminDomainEventOutbox_Pending")
            .WithTags("Admin", "EventOutbox");

        group.MapPost("/{id:guid}/retry", HandleRetry)
            .WithName("AdminDomainEventOutbox_Retry")
            .WithTags("Admin", "EventOutbox");

        return group;
    }

    // =========================================================================
    // Handlers — pure MediatR dispatch (CQRS rule: zero direct service injection)
    // =========================================================================

    private static async Task<IResult> HandleStats(
        IMediator mediator,
        CancellationToken ct)
    {
        var stats = await mediator.Send(new GetEventOutboxStatsQuery(), ct).ConfigureAwait(false);
        return Results.Ok(stats);
    }

    private static async Task<IResult> HandleFailed(
        [FromQuery] int? limit,
        IMediator mediator,
        CancellationToken ct)
    {
        var rows = await mediator
            .Send(new GetFailedEventOutboxRowsQuery(limit ?? DefaultListLimit), ct)
            .ConfigureAwait(false);
        return Results.Ok(rows);
    }

    private static async Task<IResult> HandlePending(
        [FromQuery] int? limit,
        IMediator mediator,
        CancellationToken ct)
    {
        var rows = await mediator
            .Send(new GetPendingEventOutboxRowsQuery(limit ?? DefaultListLimit), ct)
            .ConfigureAwait(false);
        return Results.Ok(rows);
    }

    private static async Task<IResult> HandleRetry(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        try
        {
            var rearmed = await mediator
                .Send(new RetryEventOutboxRowCommand(id), ct)
                .ConfigureAwait(false);
            return rearmed ? Results.NoContent() : Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            // Entity guard fired: row exists but is not in Failed status. The processor
            // may already own this row (Pending) or it has already been dispatched
            // (Sent). Either way, the operator's action is a no-op that should surface
            // as a 409 Conflict rather than corrupt state silently.
            return Results.Problem(
                title: "Cannot re-arm non-Failed outbox row",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    }
}
