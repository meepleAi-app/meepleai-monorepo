using Api.BoundedContexts.GameToolkit.Application.Commands.InstallToolkit;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitDetail;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitVersions;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing.GameToolkit;

/// <summary>
/// Marketplace endpoints for the SP4 /toolkits/[id] route
/// (Wave 3 Phase 2, PR #732 §5.3 / Issue #805).
/// </summary>
/// <remarks>
/// Per PR #732 §5.1 (Newman BC decomposition), these endpoints EXTEND the
/// existing <c>GameToolkit</c> bounded context rather than introducing a new
/// <c>MarketplaceBC</c> — the toolkit aggregate is shared between internal
/// session toolkits and community marketplace, only differing in publishing
/// state and authorship.
///
/// All three endpoints use <c>IMediator.Send()</c> only (CLAUDE.md CQRS
/// rule); no service is injected directly. Auth: <c>RequireAuthorization()</c>
/// (any authenticated user) — server-side variant gating is enforced inside
/// the handlers via <c>ViewerContext.IsOwner</c> / visibility checks.
///
/// Mounted under <c>/api/v1/toolkits</c> rather than the existing
/// <c>/api/v1/game-toolkits</c> group because PR #732 §5.3 explicitly
/// scopes the marketplace surface to the shorter prefix; the legacy
/// session-toolkit CRUD endpoints stay on their own prefix.
/// </remarks>
internal static class ToolkitMarketplaceEndpoints
{
    public static RouteGroupBuilder MapToolkitMarketplaceEndpoints(this RouteGroupBuilder group)
    {
        var toolkits = group.MapGroup("/toolkits")
            .WithTags("ToolkitMarketplace")
            .RequireAuthorization();

        // ── GET /api/v1/toolkits/recommended ────────────────────────────────
        // Wave 3 Phase 4a (Issue #805 / PR #732 §4.3.4): SP4 /discover rail
        // "Recommended toolkits". Static route registered BEFORE /{toolkitId:guid}
        // so the /recommended literal does not get parsed as a Guid route value.
        toolkits.MapGet("/recommended", HandleGetRecommendedToolkits)
            .WithName("GetRecommendedToolkits")
            .WithSummary("Get recommended marketplace toolkits")
            .WithDescription(
                "Returns published toolkits sorted by Bayesian score "
                + "(ratingAverage * log(ratingCount + 1) DESC, installCount DESC "
                + "tiebreak, createdAt DESC final tiebreak). Limit clamped 1..50, "
                + "default 10. Schema reality v1 carryover: rating + install "
                + "metrics are 0/null until the ToolkitRating + ToolkitInstallation "
                + "entities ship — effective sort collapses to createdAt DESC. "
                + "Cache: 30min HybridCache. Powers the SP4 /discover \"Recommended "
                + "toolkits\" rail. Wave 3 Phase 4a (Issue #805 / PR #732 §4.3.4).")
            .Produces<RecommendedToolkitsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        // ── GET /api/v1/toolkits/{toolkitId} ────────────────────────────────
        toolkits.MapGet("/{toolkitId:guid}", HandleGetToolkitDetail)
            .WithName("GetToolkitDetail")
            .WithSummary("Get marketplace toolkit detail with viewer context")
            .WithDescription(
                "Returns the toolkit detail envelope plus per-viewer flags "
                + "(isOwner, hasInstalled, canRate). 404 when the toolkit is "
                + "unpublished or yanked AND the viewer is not the author "
                + "(server-enforced security boundary per PR #732 §5.2). "
                + "Cache: 10min HybridCache, partitioned per (toolkitId, viewerId).")
            .Produces<ToolkitDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // ── GET /api/v1/toolkits/{toolkitId}/versions ──────────────────────
        toolkits.MapGet("/{toolkitId:guid}/versions", HandleGetToolkitVersions)
            .WithName("GetToolkitVersions")
            .WithSummary("List toolkit version history")
            .WithDescription(
                "Returns the published version history sorted by publishedAt "
                + "DESC. 404 when the toolkit is missing or hidden from the "
                + "viewer. Cache: 10min HybridCache (no per-viewer partition).")
            .Produces<ToolkitVersionsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // ── POST /api/v1/toolkits/{toolkitId}/install ──────────────────────
        toolkits.MapPost("/{toolkitId:guid}/install", HandleInstallToolkit)
            .WithName("InstallToolkit")
            .WithSummary("Install a toolkit (idempotent)")
            .WithDescription(
                "Idempotent install action — repeated calls return 200 with "
                + "the current state, never 409 (PR #732 §5.3.5 Nygard note). "
                + "404 when the toolkit is missing or yanked. Side-effect: "
                + "invalidates the discover:popularAgents cache so the rail "
                + "reflects the new install on the next read.")
            .Produces<InstallToolkitResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return toolkits;
    }

    private static async Task<IResult> HandleGetToolkitDetail(
        Guid toolkitId,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var viewerId = httpContext.User.GetUserId();
        if (viewerId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var query = new GetToolkitDetailQuery(toolkitId, viewerId);
        var response = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return response is null
            ? Results.NotFound()
            : Results.Ok(response);
    }

    private static async Task<IResult> HandleGetRecommendedToolkits(
        [FromQuery] int? limit,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        // Limit clamping at the endpoint layer (PR #732 §3.3 — UI-friendly
        // silent clamp rather than 400). Validator on the query enforces the
        // same range for the CQRS handler.
        var safeLimit = limit.GetValueOrDefault(10);
        if (safeLimit < 1) safeLimit = 10;
        if (safeLimit > 50) safeLimit = 50;

        var query = new GetRecommendedToolkitsQuery(safeLimit);
        var response = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleGetToolkitVersions(
        Guid toolkitId,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var viewerId = httpContext.User.GetUserId();
        if (viewerId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var query = new GetToolkitVersionsQuery(toolkitId, viewerId);
        var response = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return response is null
            ? Results.NotFound()
            : Results.Ok(response);
    }

    private static async Task<IResult> HandleInstallToolkit(
        Guid toolkitId,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var viewerId = httpContext.User.GetUserId();
        if (viewerId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var command = new InstallToolkitCommand(toolkitId, viewerId);
        var response = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return response is null
            ? Results.NotFound()
            : Results.Ok(response);
    }
}
