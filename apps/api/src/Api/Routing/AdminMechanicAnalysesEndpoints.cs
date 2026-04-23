using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for the AI-generated Mechanic Analysis workflow (ISSUE-524 / M1.2, ADR-051).
/// Complements the older Variant C draft workflow by exposing an asynchronous pipeline:
/// <list type="bullet">
///   <item><c>POST /admin/mechanic-analyses</c> — kicks off the LLM pipeline (202 Accepted).</item>
///   <item><c>GET  /admin/mechanic-analyses/{id}/status</c> — polls pipeline progress.</item>
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
