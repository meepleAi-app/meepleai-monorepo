using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for the AI-generated Mechanic Analysis workflow (ISSUE-524 / M1.2, ADR-051).
/// Complements the older Variant C draft workflow by exposing an asynchronous pipeline:
/// <list type="bullet">
///   <item><c>GET  /admin/mechanic-analyses</c> — paged discovery list (spec-panel gap #2).</item>
///   <item><c>POST /admin/mechanic-analyses</c> — kicks off the LLM pipeline (202 Accepted).</item>
///   <item><c>GET  /admin/mechanic-analyses/{id}/status</c> — polls pipeline progress.</item>
///   <item><c>POST /admin/mechanic-analyses/{id}/submit-review</c> — Draft/Rejected → InReview.</item>
///   <item><c>POST /admin/mechanic-analyses/{id}/approve</c> — InReview → Published.</item>
///   <item><c>POST /admin/mechanic-analyses/{id}/suppress</c> — T5 kill-switch (orthogonal).</item>
///   <item><c>GET  /admin/mechanic-analyses/{id}/claims</c> — list claims with citations (ISSUE-584).</item>
///   <item><c>POST /admin/mechanic-analyses/{id}/claims/{claimId}/approve</c> — per-claim approve.</item>
///   <item><c>POST /admin/mechanic-analyses/{id}/claims/{claimId}/reject</c> — per-claim reject with note.</item>
///   <item><c>POST /admin/mechanic-analyses/{id}/claims/bulk-approve</c> — bulk approve every Pending claim.</item>
/// </list>
/// The admin's user id is always read from the validated session (never trusted from the body).
/// </summary>
internal static class AdminMechanicAnalysesEndpoints
{
    public static void MapAdminMechanicAnalysesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/mechanic-analyses")
            .WithTags("Admin - Mechanic Analyses (M1.2)")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/mechanic-analyses?page=1&pageSize=20
        // Discovery list (spec-panel gap #2): paged index of recent analyses so the admin
        // page does not require pasting an UUID to find existing work.
        group.MapGet("/", async (
            int? page,
            int? pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new ListMechanicAnalysesQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20);

            var response = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .WithName("AdminListMechanicAnalyses")
        .WithSummary("List recent mechanic analyses (paged)")
        .WithDescription(
            "Returns a page of mechanic analyses ordered by CreatedAt DESC. Bypasses the " +
            "IsSuppressed query filter so suppressed rows remain reachable from the index. " +
            "Page defaults to 1, pageSize to 20 (capped at 100).");

        // POST /api/v1/admin/mechanic-analyses
        // Enqueues the async AI pipeline (B5=B). Returns 202 Accepted with polling URL.
        group.MapPost("/", async (
            GenerateMechanicAnalysisRequest request,
            HttpContext httpContext,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
            var adminId = session!.User!.Id;

            logger.LogInformation(
                "Admin {AdminId} kicking off mechanic analysis for game {SharedGameId}, PDF {PdfDocumentId}",
                adminId,
                request.SharedGameId,
                request.PdfDocumentId);

            CostCapOverrideInput? overrideInput = request.CostCapOverride is null
                ? null
                : new CostCapOverrideInput(request.CostCapOverride.NewCapUsd, request.CostCapOverride.Reason);

            var command = new GenerateMechanicAnalysisCommand(
                SharedGameId: request.SharedGameId,
                PdfDocumentId: request.PdfDocumentId,
                RequestedBy: adminId,
                CostCapUsd: request.CostCapUsd,
                CostCapOverride: overrideInput);

            var response = await mediator.Send(command, ct).ConfigureAwait(false);

            // 202 Accepted — pipeline runs asynchronously; client polls StatusUrl.
            return Results.Accepted(response.StatusUrl, response);
        })
        .WithName("AdminGenerateMechanicAnalysis")
        .WithSummary("Enqueue AI mechanic analysis pipeline (async)")
        .WithDescription(
            "Creates a Draft MechanicAnalysis aggregate and schedules the six-section LLM pipeline " +
            "to run in the background. Returns 202 Accepted with a StatusUrl for polling.");

        // GET /api/v1/admin/mechanic-analyses/{id}/status
        // Returns lifecycle + per-section run telemetry for admin observability.
        group.MapGet("/{id:guid}/status", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetMechanicAnalysisStatusQuery(id);
            var status = await mediator.Send(query, ct).ConfigureAwait(false);

            return status is not null
                ? Results.Ok(status)
                : Results.NotFound();
        })
        .WithName("AdminGetMechanicAnalysisStatus")
        .WithSummary("Poll mechanic analysis lifecycle + per-section telemetry")
        .WithDescription(
            "Returns the current status of a mechanic analysis including per-section " +
            "LLM execution metrics (provider, model, tokens, latency, cost).");

        // POST /api/v1/admin/mechanic-analyses/{id}/submit-review
        // Draft/Rejected → InReview. Requires at least one claim on the aggregate.
        group.MapPost("/{id:guid}/submit-review", async (
            Guid id,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
            var adminId = session!.User!.Id;

            var command = new SubmitMechanicAnalysisForReviewCommand(id, adminId);
            var response = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(response);
        })
        .WithName("AdminSubmitMechanicAnalysisForReview")
        .WithSummary("Submit a mechanic analysis for admin review")
        .WithDescription(
            "Transitions the aggregate from Draft or Rejected to InReview. Resubmission from " +
            "Rejected resets pending/rejected claims. Suppression is orthogonal — a suppressed " +
            "analysis can still be resubmitted.");

        // POST /api/v1/admin/mechanic-analyses/{id}/approve
        // InReview → Published. Requires every claim to be Approved (409 otherwise).
        group.MapPost("/{id:guid}/approve", async (
            Guid id,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
            var reviewerId = session!.User!.Id;

            var command = new ApproveMechanicAnalysisCommand(id, reviewerId);
            var response = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(response);
        })
        .WithName("AdminApproveMechanicAnalysis")
        .WithSummary("Approve a mechanic analysis and publish it")
        .WithDescription(
            "Transitions the aggregate from InReview to Published. All claims must be in " +
            "Approved status; otherwise the endpoint returns 409 with a breakdown.");

        // GET /api/v1/admin/mechanic-analyses/{id}/claims (ISSUE-584)
        // Lists every claim of the analysis with citations, ordered by section then display order.
        group.MapGet("/{id:guid}/claims", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetMechanicAnalysisClaimsQuery(id);
            var claims = await mediator.Send(query, ct).ConfigureAwait(false);

            return claims is not null
                ? Results.Ok(claims)
                : Results.NotFound();
        })
        .WithName("AdminGetMechanicAnalysisClaims")
        .WithSummary("List every claim of a mechanic analysis with citations")
        .WithDescription(
            "Returns the full claims list for the given analysis. Includes claims that " +
            "were rejected so the reviewer can see the full picture. Suppressed analyses are " +
            "still readable here — suppression is orthogonal to the review workflow.");

        // POST /api/v1/admin/mechanic-analyses/{id}/claims/{claimId}/approve (ISSUE-584)
        // Per-claim approve. Parent must be InReview (409 otherwise).
        group.MapPost("/{id:guid}/claims/{claimId:guid}/approve", async (
            Guid id,
            Guid claimId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
            var reviewerId = session!.User!.Id;

            var command = new ApproveMechanicClaimCommand(id, claimId, reviewerId);
            var response = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(response);
        })
        .WithName("AdminApproveMechanicClaim")
        .WithSummary("Approve a single claim (ISSUE-584)")
        .WithDescription(
            "Transitions a single claim from Pending or Rejected to Approved. Idempotent on " +
            "claims already Approved. Parent analysis must be InReview; otherwise 409.");

        // POST /api/v1/admin/mechanic-analyses/{id}/claims/{claimId}/reject (ISSUE-584)
        // Per-claim reject with mandatory note (1–500 chars).
        group.MapPost("/{id:guid}/claims/{claimId:guid}/reject", async (
            Guid id,
            Guid claimId,
            RejectClaimRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
            var reviewerId = session!.User!.Id;

            var command = new RejectMechanicClaimCommand(id, claimId, reviewerId, request.Note);
            var response = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(response);
        })
        .WithName("AdminRejectMechanicClaim")
        .WithSummary("Reject a single claim with a note (ISSUE-584)")
        .WithDescription(
            "Transitions a single claim to Rejected. The note (1–500 chars) is mandatory and " +
            "is surfaced to the reviewer for follow-up. Parent analysis must be InReview.");

        // POST /api/v1/admin/mechanic-analyses/{id}/claims/bulk-approve (ISSUE-584)
        // Bulk-approve every Pending claim. Skips Approved (idempotent) and preserves Rejected.
        group.MapPost("/{id:guid}/claims/bulk-approve", async (
            Guid id,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
            var reviewerId = session!.User!.Id;

            var command = new BulkApproveMechanicClaimsCommand(id, reviewerId);
            var response = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(response);
        })
        .WithName("AdminBulkApproveMechanicClaims")
        .WithSummary("Bulk-approve every Pending claim on the analysis (ISSUE-584)")
        .WithDescription(
            "Approves every claim currently in Pending status. Already-Approved claims are " +
            "skipped silently. Rejected claims are preserved — the reviewer must explicitly " +
            "re-approve them. Returns ApprovedCount, SkippedRejectedCount, and the full " +
            "updated claims list. Parent analysis must be InReview.");

        // T5 kill-switch: orthogonal suppression allowed from any status, including Published.
        group.MapPost("/{id:guid}/suppress", async (
            Guid id,
            SuppressMechanicAnalysisRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
            var actorId = session!.User!.Id;

            var command = new SuppressMechanicAnalysisCommand(
                AnalysisId: id,
                ActorId: actorId,
                Reason: request.Reason,
                RequestSource: request.RequestSource,
                RequestedAt: request.RequestedAt);

            var response = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(response);
        })
        .WithName("AdminSuppressMechanicAnalysis")
        .WithSummary("Apply the T5 takedown kill-switch on a mechanic analysis")
        .WithDescription(
            "Applies suppression (hides the analysis from player-facing queries via the global " +
            "IsSuppressed query filter). Suppression is orthogonal to the lifecycle — allowed " +
            "from any status. Returns 409 when the aggregate is already suppressed.");
    }
}

/// <summary>
/// Request body for <c>POST /admin/mechanic-analyses</c>. The admin user id is sourced from
/// the validated session, never from the body.
/// </summary>
internal sealed record GenerateMechanicAnalysisRequest(
    Guid SharedGameId,
    Guid PdfDocumentId,
    decimal CostCapUsd,
    CostCapOverrideRequest? CostCapOverride = null);

/// <summary>
/// Optional planning-time cost cap override (B3=A). Mirrors
/// <see cref="CostCapOverrideInput"/> but lives in the Routing layer so the request contract
/// is not coupled to the internal command record.
/// </summary>
internal sealed record CostCapOverrideRequest(decimal NewCapUsd, string Reason);

/// <summary>
/// Request body for <c>POST /admin/mechanic-analyses/{id}/suppress</c>. Mirrors
/// <see cref="SuppressMechanicAnalysisCommand"/> but stays in the Routing layer to keep the
/// public HTTP contract decoupled from the internal command record. The actor id is sourced
/// from the validated session (never from the body).
/// </summary>
/// <param name="Reason">Legally significant justification for the takedown (20–500 chars).</param>
/// <param name="RequestSource">Origin of the takedown request (Email, Legal, Other).</param>
/// <param name="RequestedAt">Optional UTC timestamp of when the takedown notice was received.</param>
internal sealed record SuppressMechanicAnalysisRequest(
    string Reason,
    SuppressionRequestSource RequestSource,
    DateTime? RequestedAt = null);

/// <summary>
/// Request body for <c>POST /admin/mechanic-analyses/{id}/claims/{claimId}/reject</c>
/// (ISSUE-584). The reviewer id is sourced from the validated session.
/// </summary>
/// <param name="Note">Reviewer rejection note (1–500 chars). Mandatory — surfaces back
/// in the claims viewer so the reviewer can act on it before re-approving.</param>
internal sealed record RejectClaimRequest(string Note);
