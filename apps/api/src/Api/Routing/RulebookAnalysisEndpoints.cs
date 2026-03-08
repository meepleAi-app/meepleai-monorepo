using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Endpoints for rulebook analysis operations.
/// Issue #2402: Rulebook Analysis Service
/// </summary>
internal static class RulebookAnalysisEndpoints
{
    public static RouteGroupBuilder MapRulebookAnalysisEndpoints(this RouteGroupBuilder group)
    {
        // POST /api/v1/documents/{documentId}/analyze
        group.MapPost("/documents/{documentId:guid}/analyze", HandleAnalyzeRulebook)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("AnalyzeRulebook")
            .WithSummary("Analyze a rulebook PDF to extract structured game information")
            .WithDescription("Triggers AI analysis of a rulebook PDF to extract game mechanics, victory conditions, resources, phases, and common questions. Returns the analysis result with confidence scores.")
            .Produces<AnalyzeRulebookResultDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/documents/{documentId}/analysis
        group.MapGet("/documents/{documentId:guid}/analysis", HandleGetActiveAnalysis)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("GetActiveRulebookAnalysis")
            .WithSummary("Get the active rulebook analysis for a PDF document")
            .WithDescription("Returns the currently active analysis for the specified PDF document. Returns 404 if no analysis exists.")
            .Produces<RulebookAnalysisDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/documents/{documentId}/analysis/status/{taskId}
        group.MapGet("/documents/{documentId:guid}/analysis/status/{taskId}", HandleGetAnalysisStatus)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("GetBackgroundAnalysisStatus")
            .WithSummary("Get status of background rulebook analysis")
            .WithDescription("Returns the current status and progress of a background analysis task. Includes result when completed.")
            .Produces<BackgroundAnalysisStatusDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> HandleAnalyzeRulebook(
        IMediator mediator,
        HttpContext context,
        Guid documentId,
        Guid sharedGameId, // from query string
        CancellationToken ct)
    {
        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Results.BadRequest("User ID not found in claims");
        }

        var command = new AnalyzeRulebookCommand(documentId, sharedGameId, userId);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        // Issue #2454: Return 202 Accepted for background tasks, 200 OK for synchronous
        return result.IsBackgroundTask
            ? Results.AcceptedAtRoute("GetBackgroundAnalysisStatus",
                new { documentId, taskId = result.TaskId, sharedGameId },
                result)
            : Results.Ok(result);
    }

    private static async Task<IResult> HandleGetAnalysisStatus(
        IMediator mediator,
        Guid documentId,
        string taskId,
        Guid sharedGameId, // from query string
        CancellationToken ct)
    {
        var query = new GetBackgroundAnalysisStatusQuery(taskId, sharedGameId, documentId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetActiveAnalysis(
        IMediator mediator,
        Guid documentId,
        Guid sharedGameId, // from query string
        CancellationToken ct)
    {
        var query = new GetActiveRulebookAnalysisQuery(sharedGameId, documentId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return result is not null
            ? Results.Ok(result)
            : Results.NotFound($"No active analysis found for PDF document {documentId}");
    }

}

/// <summary>
/// Extension method to get user ID from claims.
/// </summary>
internal static class HttpContextExtensions
{
    public static Guid GetUserId(this System.Security.Claims.ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
