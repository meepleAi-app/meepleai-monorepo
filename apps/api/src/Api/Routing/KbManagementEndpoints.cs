using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// User-facing endpoints for Knowledge Base lifecycle management operations.
///
/// Endpoints:
///   POST /games/{gameId:guid}/kb/reindex         — Re-embed all chunks for all PDFs of a game
///   POST /games/{gameId:guid}/kb/raptor/rebuild  — Rebuild RAPTOR summary tree (premium only)
///
/// Both endpoints follow the jobId pattern, returning 202 Accepted with:
/// <code>{ "jobId": "...", "status": "completed", "pdfCount": N }</code>
///
/// Note: Current implementation is synchronous (status always "completed").
/// True async background-job support is a future enhancement.
///
/// Issue #903: SG2 — KB lifecycle con re-index smoke test.
/// </summary>
internal static class KbManagementEndpoints
{
    public static RouteGroupBuilder MapKbManagementEndpoints(this RouteGroupBuilder group)
    {
        MapReindexEndpoint(group);
        MapReindexStatusEndpoint(group);
        MapRaptorRebuildEndpoint(group);
        return group;
    }

    /// <summary>
    /// POST /games/{gameId:guid}/kb/reindex
    ///
    /// Re-embed all chunks of all indexed PDFs belonging to a game's Knowledge Base.
    /// Returns 202 Accepted with a job descriptor.
    /// </summary>
    private static void MapReindexEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/games/{gameId:guid}/kb/reindex", async (
            Guid gameId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = httpContext.TryGetActiveSession();
            if (!authenticated) return error!;

            var command = new ReindexGameKbCommand(GameId: gameId, UserId: session.Principal!.Subject.Id);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Accepted(
                uri: (string?)null,
                value: new
                {
                    jobId = result.JobId,
                    status = result.Status,
                    pdfCount = result.PdfCount
                });
        })
        .RequireSession()
        .WithName("ReindexGameKb")
        .WithTags("Games", "KnowledgeBase")
        .WithSummary("Re-index all PDFs in a game's Knowledge Base")
        .WithDescription(
            "Re-embeds all chunks of all indexed PDFs for the given game. " +
            "Returns 202 Accepted with a job descriptor. " +
            "Current implementation: synchronous (status = 'completed'). " +
            "Issue #903: SG2 smoke test.");
    }

    /// <summary>
    /// GET /games/{gameId:guid}/kb/reindex/{jobId:guid}/status
    ///
    /// Returns the current state of a reindex job. Issue #941 / ADR-057.
    /// </summary>
    private static void MapReindexStatusEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/games/{gameId:guid}/kb/reindex/{jobId:guid}/status", async (
            Guid gameId,
            Guid jobId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = httpContext.TryGetActiveSession();
            if (!authenticated) return error!;

            var query = new GetKbReindexJobStatusQuery(
                GameId: gameId,
                JobId: jobId,
                RequestingUserId: session.Principal!.Subject.Id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return result is null
                ? Results.NotFound(new { error = "Job not found", jobId })
                : Results.Ok(result);
        })
        .RequireSession()
        .WithName("GetKbReindexJobStatus")
        .WithTags("Games", "KnowledgeBase")
        .WithSummary("Poll the status of a KB reindex job")
        .WithDescription(
            "Returns the persisted KbReindexJob row for the given (gameId, jobId). " +
            "Status values: queued, running, completed, failed. " +
            "Returns 404 if the job is unknown, 403 if it belongs to another user. " +
            "Issue #941 / ADR-057.");
    }

    /// <summary>
    /// POST /games/{gameId:guid}/kb/raptor/rebuild
    ///
    /// Rebuilds the RAPTOR hierarchical summary tree for all PDFs of a game.
    /// Requires premium subscription tier — free-tier users receive 403 with TIER_FEATURE_LOCKED.
    /// Returns 202 Accepted with a job descriptor on success.
    /// </summary>
    private static void MapRaptorRebuildEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/games/{gameId:guid}/kb/raptor/rebuild", async (
            Guid gameId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = httpContext.TryGetActiveSession();
            if (!authenticated) return error!;

            try
            {
                var command = new RebuildRaptorCommand(GameId: gameId, UserId: session.Principal!.Subject.Id);
                var result = await mediator.Send(command, ct).ConfigureAwait(false);

                return Results.Accepted(
                    uri: (string?)null,
                    value: new
                    {
                        jobId = result.JobId,
                        status = result.Status,
                        pdfCount = result.PdfCount
                    });
            }
            catch (TierFeatureLockedException ex)
            {
                // Return structured 403 with the feature name as specified in the SG2 contract.
                return Results.Json(
                    new
                    {
                        error = "TIER_FEATURE_LOCKED",
                        feature = ex.Feature,
                        message = ex.Message
                    },
                    statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .RequireSession()
        .WithName("RebuildRaptorForGame")
        .WithTags("Games", "KnowledgeBase")
        .WithSummary("Rebuild RAPTOR summary tree for a game's KB")
        .WithDescription(
            "Rebuilds the hierarchical RAPTOR summary tree for all indexed PDFs of the game. " +
            "Requires premium subscription tier. Free-tier returns 403 TIER_FEATURE_LOCKED. " +
            "Returns 202 Accepted on success. " +
            "Issue #903: SG2 smoke test.");
    }
}
