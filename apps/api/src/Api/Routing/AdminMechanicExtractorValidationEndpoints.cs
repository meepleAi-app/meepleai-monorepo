using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Filters;
using MediatR;
using GoldenBggTagDto = Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden.BggTagDto;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for the AI Comprehension Validation pipeline (ADR-051 Sprint 1 / Task 32).
/// Mounted under <c>/admin/mechanic-extractor</c> alongside the existing Variant C draft surface
/// (see <see cref="AdminMechanicExtractorEndpoints"/>) — the new validation endpoints occupy
/// disjoint sub-paths (<c>/golden/...</c>, <c>/analyses/.../metrics</c>,
/// <c>/analyses/.../override-certification</c>, <c>/metrics/recalculate-all</c>,
/// <c>/dashboard</c>, <c>/dashboard/{id}/trend</c>, <c>/thresholds</c>) so the two surfaces
/// coexist cleanly without route collisions. Group is guarded by
/// <see cref="RequireAdminSessionFilter"/>; admin user ids are sourced from the validated
/// session, never from request bodies.
/// </summary>
internal static class AdminMechanicExtractorValidationEndpoints
{
    public static void MapAdminMechanicExtractorValidationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/mechanic-extractor")
            .WithTags("Admin - Mechanic Extractor Validation (ADR-051)")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // ──────────────────────────────────────────────────────────────────
        // Golden set: queries
        // ──────────────────────────────────────────────────────────────────

        // GET /api/v1/admin/mechanic-extractor/golden/{sharedGameId}
        group.MapGet("/golden/{sharedGameId:guid}", async (
            Guid sharedGameId,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation(
                "Admin loading golden set for shared game {SharedGameId}",
                sharedGameId);
            var query = new GetGoldenForGameQuery(sharedGameId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AdminGetMechanicGoldenForGame")
        .WithSummary("Load curated golden set (claims + BGG tags + version hash)")
        .WithDescription(
            "Returns the curated golden-set for a shared game, used by the AI Comprehension " +
            "Validation UI. Cached for 10 minutes via IHybridCacheService.");

        // GET /api/v1/admin/mechanic-extractor/golden/{sharedGameId}/version-hash
        group.MapGet("/golden/{sharedGameId:guid}/version-hash", async (
            Guid sharedGameId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetGoldenVersionHashQuery(sharedGameId);
            var versionHash = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(versionHash);
        })
        .WithName("AdminGetMechanicGoldenVersionHash")
        .WithSummary("Lightweight golden-set version hash")
        .WithDescription(
            "Returns the current version hash of the golden set for the specified shared game. " +
            "Used by the frontend to detect drift cheaply without loading the full DTO.");

        // ──────────────────────────────────────────────────────────────────
        // Golden set: CRUD
        // ──────────────────────────────────────────────────────────────────

        // POST /api/v1/admin/mechanic-extractor/golden
        group.MapPost("/golden", async (
            CreateMechanicGoldenClaimRequest body,
            HttpContext httpContext,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
            var curatorId = session.User!.Id;

            logger.LogInformation(
                "Admin {AdminId} creating golden claim for shared game {SharedGameId} (section {Section})",
                curatorId,
                body.SharedGameId,
                body.Section);

            var command = new CreateMechanicGoldenClaimCommand(
                SharedGameId: body.SharedGameId,
                Section: body.Section,
                Statement: body.Statement,
                ExpectedPage: body.ExpectedPage,
                SourceQuote: body.SourceQuote,
                CuratorUserId: curatorId);

            var newId = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/admin/mechanic-extractor/golden/{body.SharedGameId}", new { Id = newId });
        })
        .WithName("AdminCreateMechanicGoldenClaim")
        .WithSummary("Create a new golden claim")
        .WithDescription(
            "Curator-authored ground-truth claim used to score downstream MechanicAnalysis " +
            "outputs. CuratorUserId is sourced from the validated session.")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/v1/admin/mechanic-extractor/golden/{id}
        group.MapPut("/golden/{id:guid}", async (
            Guid id,
            UpdateMechanicGoldenClaimRequest body,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation("Admin updating golden claim {ClaimId}", id);
            await mediator.Send(body.ToCommand(id), ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .WithName("AdminUpdateMechanicGoldenClaim")
        .WithSummary("Update an existing golden claim")
        .WithDescription(
            "Updates the statement, expected page, and source quote of an existing golden claim. " +
            "Section is immutable: to reclassify, deactivate the existing claim and create a new " +
            "one under the correct section.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // DELETE /api/v1/admin/mechanic-extractor/golden/{id}
        group.MapDelete("/golden/{id:guid}", async (
            Guid id,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation("Admin deactivating golden claim {ClaimId}", id);
            await mediator.Send(new DeactivateMechanicGoldenClaimCommand(id), ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .WithName("AdminDeactivateMechanicGoldenClaim")
        .WithSummary("Soft-delete (deactivate) a golden claim")
        .WithDescription(
            "Soft-deletes the claim. Deactivation is a terminal state — the claim cannot be " +
            "re-activated. Returns 409 if the claim is already deactivated.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/v1/admin/mechanic-extractor/golden/{sharedGameId}/bgg-tags
        group.MapPost("/golden/{sharedGameId:guid}/bgg-tags", async (
            Guid sharedGameId,
            ImportBggTagsRequest body,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation(
                "Admin importing {TagCount} BGG tags for shared game {SharedGameId}",
                body.Tags.Count,
                sharedGameId);

            var tags = body.Tags
                .Select(t => new GoldenBggTagDto(t.Name, t.Category))
                .ToList();

            var upserted = await mediator
                .Send(new ImportBggTagsCommand(sharedGameId, tags), ct)
                .ConfigureAwait(false);

            return Results.Ok(new { Upserted = upserted });
        })
        .WithName("AdminImportBggTagsForGolden")
        .WithSummary("Bulk-import BoardGameGeek mechanic tags (upsert)")
        .WithDescription(
            "Additive upsert of BGG tags into the golden set for a shared game. Existing tags " +
            "not present in the request are left untouched; duplicates are deduped at the " +
            "repository layer.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        // ──────────────────────────────────────────────────────────────────
        // Metrics + certification override
        // ──────────────────────────────────────────────────────────────────

        // POST /api/v1/admin/mechanic-extractor/analyses/{id}/metrics
        group.MapPost("/analyses/{id:guid}/metrics", async (
            Guid id,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation("Admin calculating mechanic analysis metrics for {AnalysisId}", id);
            var metricsId = await mediator
                .Send(new CalculateMechanicAnalysisMetricsCommand(id), ct)
                .ConfigureAwait(false);
            return Results.Ok(new { MetricsId = metricsId });
        })
        .WithName("AdminCalculateMechanicAnalysisMetrics")
        .WithSummary("Compute AI comprehension validation metrics for a published analysis")
        .WithDescription(
            "Scores every claim on the target MechanicAnalysis against the golden set, persists " +
            "a MechanicAnalysisMetrics snapshot, and returns its primary key. The analysis must " +
            "be in Published status; otherwise returns 409.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/v1/admin/mechanic-extractor/analyses/{id}/override-certification
        group.MapPost("/analyses/{id:guid}/override-certification", async (
            Guid id,
            OverrideCertificationRequest body,
            HttpContext httpContext,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
            var actorId = session.User!.Id;

            logger.LogInformation(
                "Admin {AdminId} overriding certification for mechanic analysis {AnalysisId}",
                actorId,
                id);

            var command = new OverrideCertificationCommand(
                MechanicAnalysisId: id,
                Reason: body.Reason,
                UserId: actorId);

            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .WithName("AdminOverrideMechanicCertification")
        .WithSummary("Admin escalation: certify an analysis whose metrics did not meet thresholds")
        .WithDescription(
            "Persists a 20..500-char justification on the aggregate and certifies the analysis " +
            "as a deliberate override. Requires prior metrics on the analysis. UserId is sourced " +
            "from the validated session.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/v1/admin/mechanic-extractor/metrics/recalculate-all
        group.MapPost("/metrics/recalculate-all", async (
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation("Admin triggering mass recalculation of mechanic analysis metrics");
            var processed = await mediator
                .Send(new RecalculateAllMechanicMetricsCommand(), ct)
                .ConfigureAwait(false);
            return Results.Ok(new { Processed = processed });
        })
        .WithName("AdminRecalculateAllMechanicMetrics")
        .WithSummary("Recalculate metrics for every published mechanic analysis")
        .WithDescription(
            "Synchronous batch dispatcher: iterates every Published MechanicAnalysis (suppressed " +
            "rows included — suppression is orthogonal) and dispatches one CalculateMechanicAnalysisMetricsCommand " +
            "per id. Returns the count of analyses for which metrics were successfully recomputed. " +
            "Per-id NotFound/Conflict errors are logged and skipped.")
        .Produces(StatusCodes.Status200OK);

        // ──────────────────────────────────────────────────────────────────
        // Dashboard + trend
        // ──────────────────────────────────────────────────────────────────

        // GET /api/v1/admin/mechanic-extractor/dashboard
        group.MapGet("/dashboard", async (
            IMediator mediator,
            CancellationToken ct) =>
        {
            var rows = await mediator.Send(new GetDashboardQuery(), ct).ConfigureAwait(false);
            return Results.Ok(rows);
        })
        .WithName("AdminGetMechanicValidationDashboard")
        .WithSummary("Per-game certification dashboard")
        .WithDescription(
            "Returns per-game summary feed for the admin AI Comprehension Validation dashboard. " +
            "Each row carries the latest certification status and overall score for a shared game. " +
            "Cached for 5 minutes via IHybridCacheService.");

        // GET /api/v1/admin/mechanic-extractor/dashboard/{sharedGameId}/trend?take=20
        group.MapGet("/dashboard/{sharedGameId:guid}/trend", async (
            Guid sharedGameId,
            int? take,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetTrendQuery(sharedGameId, take ?? 20);
            var trend = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(trend);
        })
        .WithName("AdminGetMechanicValidationTrend")
        .WithSummary("Historical metrics trend for a shared game")
        .WithDescription(
            "Returns up to ?take=N historical MechanicAnalysisMetrics snapshots for a shared game, " +
            "ordered by computation time descending. Defaults to 20; capped at 100 by the validator. " +
            "Cached for 5 minutes per (sharedGameId, take) tuple.");

        // ──────────────────────────────────────────────────────────────────
        // Certification thresholds (singleton)
        // ──────────────────────────────────────────────────────────────────

        // GET /api/v1/admin/mechanic-extractor/thresholds
        group.MapGet("/thresholds", async (
            IMediator mediator,
            CancellationToken ct) =>
        {
            var thresholds = await mediator
                .Send(new GetCertificationThresholdsQuery(), ct)
                .ConfigureAwait(false);
            return Results.Ok(thresholds);
        })
        .WithName("AdminGetCertificationThresholds")
        .WithSummary("Read the operator-configurable certification thresholds singleton")
        .WithDescription(
            "Returns the active CertificationThresholds value object: minimum coverage %, " +
            "max page tolerance, minimum BGG match %, and minimum overall score. " +
            "Cached for 30 minutes via IHybridCacheService.");

        // PUT /api/v1/admin/mechanic-extractor/thresholds
        group.MapPut("/thresholds", async (
            UpdateCertificationThresholdsRequest body,
            HttpContext httpContext,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
            var actorId = session.User!.Id;

            logger.LogInformation(
                "Admin {AdminId} updating certification thresholds (coverage={Coverage}, pageTol={PageTol}, bgg={Bgg}, overall={Overall})",
                actorId,
                body.MinCoveragePct,
                body.MaxPageTolerance,
                body.MinBggMatchPct,
                body.MinOverallScore);

            var command = new UpdateCertificationThresholdsCommand(
                MinCoveragePct: body.MinCoveragePct,
                MaxPageTolerance: body.MaxPageTolerance,
                MinBggMatchPct: body.MinBggMatchPct,
                MinOverallScore: body.MinOverallScore,
                UserId: actorId);

            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .WithName("AdminUpdateCertificationThresholds")
        .WithSummary("Update the certification thresholds singleton")
        .WithDescription(
            "Replaces the active CertificationThresholds value object. UserId is threaded from " +
            "the validated session and persisted as UpdatedByUserId on the singleton aggregate. " +
            "This endpoint does NOT trigger a mass recertification — operators must call " +
            "/metrics/recalculate-all separately.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest);
    }
}

// ──────────────────────────────────────────────────────────────────────────
// Request DTOs (HTTP contracts decoupled from the internal command records)
// ──────────────────────────────────────────────────────────────────────────

/// <summary>
/// Request body for <c>POST /admin/mechanic-extractor/golden</c>. The curator user id is
/// sourced from the validated session, never from the body.
/// </summary>
internal sealed record CreateMechanicGoldenClaimRequest(
    Guid SharedGameId,
    MechanicSection Section,
    string Statement,
    int ExpectedPage,
    string SourceQuote);

/// <summary>
/// Request body for <c>PUT /admin/mechanic-extractor/golden/{id}</c>. The route-bound id is
/// woven in via <see cref="ToCommand"/>.
/// </summary>
internal sealed record UpdateMechanicGoldenClaimRequest(
    string Statement,
    int ExpectedPage,
    string SourceQuote)
{
    public UpdateMechanicGoldenClaimCommand ToCommand(Guid claimId) =>
        new(claimId, Statement, ExpectedPage, SourceQuote);
}

/// <summary>
/// Single BGG tag input for <c>POST /admin/mechanic-extractor/golden/{sharedGameId}/bgg-tags</c>.
/// Mirrors the command-side <c>BggTagDto</c> but kept in the routing layer so the HTTP contract
/// stays independent of the internal command DTO.
/// </summary>
internal sealed record BggTagInput(string Name, string Category);

/// <summary>
/// Request body for <c>POST /admin/mechanic-extractor/golden/{sharedGameId}/bgg-tags</c>.
/// </summary>
internal sealed record ImportBggTagsRequest(IReadOnlyList<BggTagInput> Tags);

/// <summary>
/// Request body for <c>POST /admin/mechanic-extractor/analyses/{id}/override-certification</c>.
/// The actor user id is sourced from the validated session.
/// </summary>
/// <param name="Reason">20..500-char justification persisted on the aggregate.</param>
internal sealed record OverrideCertificationRequest(string Reason);

/// <summary>
/// Request body for <c>PUT /admin/mechanic-extractor/thresholds</c>. The acting admin user id is
/// sourced from the validated session and persisted as <c>UpdatedByUserId</c>.
/// </summary>
internal sealed record UpdateCertificationThresholdsRequest(
    decimal MinCoveragePct,
    int MaxPageTolerance,
    decimal MinBggMatchPct,
    decimal MinOverallScore);
