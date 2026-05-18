using Api.BoundedContexts.GameToolkit.Application.Commands.InstallToolkit;
using Api.BoundedContexts.GameToolkit.Application.Commands.PublishToolkitVersion;
using Api.BoundedContexts.GameToolkit.Application.Commands.YankToolkitVersion;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetSystemPrompt;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitDetail;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitRatings;
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

        // ── GET /api/v1/toolkits/{toolkitId}/ratings ───────────────────────
        // Wave 3 Phase 4b (Issue #805 / PR #732 §5.3.3): ratings list with
        // 5-star breakdown for the SP4 /toolkits/[id] ratings tab. Schema
        // reality v1 carryover (Gate B): empty stub — ToolkitRating entity
        // ships in Phase 5.
        toolkits.MapGet("/{toolkitId:guid}/ratings", HandleGetToolkitRatings)
            .WithName("GetToolkitRatings")
            .WithSummary("List toolkit ratings (cursor paginated)")
            .WithDescription(
                "Returns ratings sorted by createdAt DESC with 5-star "
                + "breakdown distribution and averageStars (1-5 with one "
                + "decimal). Cursor opaque, nextCursor=null when exhausted. "
                + "Limit clamped 1..50, default 20. 404 when toolkit is "
                + "missing or hidden from the viewer (PR #732 §5.2 security "
                + "boundary). Empty-state contract per §3.4: 200 with "
                + "{ items: [] } rather than 404. Schema reality v1 carryover: "
                + "ToolkitRating entity not yet shipped — handler returns "
                + "stub envelope (items=[], breakdown all zero, averageStars=0, "
                + "totalCount=0) once visibility check passes. Cache: 5min "
                + "HybridCache. Wave 3 Phase 4b (Issue #805 / PR #732 §5.3.3).")
            .Produces<ToolkitRatingsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // ── POST /api/v1/toolkits/{toolkitId}/ratings ──────────────────────
        // Wave 3 Phase 4b (Issue #805 / PR #732 §5.3.4): rating submission
        // STUB — returns 501 Not Implemented because the ToolkitRating
        // entity + persistence ship in Phase 5. Wire shape (route literal
        // and method) is reserved so the FE submission flow can call this
        // surface today and detect the stub via 501.
        toolkits.MapPost("/{toolkitId:guid}/ratings", HandleSubmitToolkitRating)
            .WithName("SubmitToolkitRating")
            .WithSummary("Submit a toolkit rating (Phase 4b stub — 501)")
            .WithDescription(
                "Phase 4b stub. Returns 501 Not Implemented with a "
                + "machine-readable body { error: \"ratings_submission_not_yet_available\", "
                + "phase: \"4b-stub\" } so the FE can surface a \"coming soon\" "
                + "affordance without breaking on a 404. Persistence + "
                + "validation rules (one rating per (userId, toolkitId), "
                + "hasInstalled gate, viewer != author, edit window 24h) "
                + "ship in Phase 5. Wave 3 Phase 4b (Issue #805 / PR #732 §5.3.4).")
            .Produces(StatusCodes.Status501NotImplemented)
            .Produces(StatusCodes.Status401Unauthorized);

        // ── GET /api/v1/toolkits/{toolkitId}/system-prompt ─────────────────
        // Issue #822 / Phase 5 PR-2 (spec-panel 2026-05-18 §2): owner sees the
        // full prompt + agent mode + timestamp; public sees mode + char count
        // only. Visibility check mirrors /toolkits/{id} detail.
        toolkits.MapGet("/{toolkitId:guid}/system-prompt", HandleGetSystemPrompt)
            .WithName("GetToolkitSystemPrompt")
            .WithSummary("Get marketplace toolkit agent system-prompt projection")
            .WithDescription(
                "Returns either a SystemPromptOwnerDto (FullPrompt + AgentMode + GeneratedAt) "
                + "when viewer is the toolkit author, or a SystemPromptPublicDto (AgentMode + "
                + "CharacterCount) otherwise. 404 when the toolkit is missing or hidden "
                + "(non-author + unpublished). Cache: 10min HybridCache partitioned by "
                + "viewer class (owner/public). Issue #822 Phase 5 PR-2.")
            .Produces<SystemPromptResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // ── POST /api/v1/toolkits/{toolkitId}/versions ─────────────────────
        // Issue #822 / Phase 5 PR-2 (spec-panel 2026-05-18 §3): publish a new
        // semver version. Owner-only enforcement; 409 on duplicate or
        // non-monotonic version. Emits ToolkitVersionPublishedEvent + flushes
        // the cache invalidation matrix per spec-panel §5.
        toolkits.MapPost("/{toolkitId:guid}/versions", HandlePublishToolkitVersion)
            .WithName("PublishToolkitVersion")
            .WithSummary("Publish a new toolkit version (owner only)")
            .WithDescription(
                "Creates a new ToolkitVersion row, flips GameToolkit.IsPublished=true, "
                + "bumps VersionSemver. 403 when caller is not the author. "
                + "409 when versionNumber duplicates an existing row (including yanked) "
                + "or is not strictly greater than the latest non-yanked version. "
                + "Issue #822 Phase 5 PR-2.")
            .Produces<PublishedToolkitVersionResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // ── POST /api/v1/toolkits/{toolkitId}/versions/{versionId}/yank ────
        // Issue #822 / Phase 5 PR-2 (spec-panel 2026-05-18 §1 + §4): yank
        // (soft-delete + audit) a published version. Cascades to
        // GameToolkit.IsPublished=false if no non-yanked versions remain.
        toolkits.MapPost("/{toolkitId:guid}/versions/{versionId:guid}/yank", HandleYankToolkitVersion)
            .WithName("YankToolkitVersion")
            .WithSummary("Yank (soft-delete) a published toolkit version (owner only)")
            .WithDescription(
                "Marks the version with YankedAt/YankedBy/YankReason. The row remains "
                + "for audit + installed-user reads but is filtered from marketplace listings. "
                + "Version numbers are permanently retired — cannot be re-published. "
                + "Cascade: if this yank leaves the toolkit with zero non-yanked versions, "
                + "GameToolkit.IsPublished flips to false in the same transaction. "
                + "Issue #822 Phase 5 PR-2.")
            .Produces<YankedToolkitVersionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

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

    private static async Task<IResult> HandleGetToolkitRatings(
        Guid toolkitId,
        HttpContext httpContext,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var viewerId = httpContext.User.GetUserId();
        if (viewerId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        // Limit clamping at the endpoint layer (PR #732 §3.3 — UI-friendly
        // silent clamp rather than 400). Validator on the query enforces the
        // same range for the CQRS handler.
        var safeLimit = limit.GetValueOrDefault(20);
        if (safeLimit < 1) safeLimit = 20;
        if (safeLimit > 50) safeLimit = 50;

        var query = new GetToolkitRatingsQuery(toolkitId, viewerId, cursor, safeLimit);
        var response = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return response is null
            ? Results.NotFound()
            : Results.Ok(response);
    }

    private static IResult HandleSubmitToolkitRating(
        Guid toolkitId,
        HttpContext httpContext)
    {
        var viewerId = httpContext.User.GetUserId();
        if (viewerId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        // Phase 4b stub — ToolkitRating entity ships in Phase 5. Body shape
        // is intentionally minimal and machine-readable so the FE can detect
        // the stub state without parsing prose error messages.
        return Results.Json(
            new
            {
                error = "ratings_submission_not_yet_available",
                phase = "4b-stub",
                toolkitId
            },
            statusCode: StatusCodes.Status501NotImplemented);
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

    // ── Phase 5 PR-2 endpoint handlers (issue #822) ──────────────────────────

    private static async Task<IResult> HandleGetSystemPrompt(
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

        var query = new GetSystemPromptQuery(toolkitId, viewerId);
        var response = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return response is null
            ? Results.NotFound()
            : Results.Ok(response);
    }

    private static async Task<IResult> HandlePublishToolkitVersion(
        Guid toolkitId,
        HttpContext httpContext,
        [FromBody] PublishVersionRequestBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var viewerId = httpContext.User.GetUserId();
        if (viewerId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var command = new PublishToolkitVersionCommand(
            ToolkitId: toolkitId,
            ViewerId: viewerId,
            VersionNumber: body.VersionNumber,
            Changelog: body.Changelog);

        var response = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return response is null
            ? Results.NotFound()
            : Results.Created(
                $"/api/v1/toolkits/{toolkitId}/versions/{response.Id}",
                response);
    }

    private static async Task<IResult> HandleYankToolkitVersion(
        Guid toolkitId,
        Guid versionId,
        HttpContext httpContext,
        [FromBody] YankVersionRequestBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var viewerId = httpContext.User.GetUserId();
        if (viewerId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var command = new YankToolkitVersionCommand(
            ToolkitId: toolkitId,
            VersionId: versionId,
            ViewerId: viewerId,
            Reason: body.Reason);

        var response = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return response is null
            ? Results.NotFound()
            : Results.Ok(response);
    }

    // ── Inline request body DTOs (route-local; not part of the BC contract) ──

    /// <summary>JSON body for POST /toolkits/{id}/versions (issue #822 PR-2).</summary>
    internal sealed record PublishVersionRequestBody(string VersionNumber, string? Changelog);

    /// <summary>JSON body for POST /toolkits/{id}/versions/{versionId}/yank (issue #822 PR-2).</summary>
    internal sealed record YankVersionRequestBody(string Reason);
}
