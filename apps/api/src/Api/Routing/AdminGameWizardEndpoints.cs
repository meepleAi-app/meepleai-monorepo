using Api.BoundedContexts.Administration.Application.Commands.GameWizard;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Extensions;
using Api.Infrastructure;
using Api.Infrastructure.Serialization;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Routing;

/// <summary>
/// Admin Game+PDF+Agent Wizard endpoints.
/// Provides a streamlined flow: BGG Search → Create Game → Upload PDF → Launch Processing → Monitor → Test Agent.
/// </summary>
internal static class AdminGameWizardEndpoints
{
    internal static RouteGroupBuilder MapAdminGameWizardEndpoints(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/games/wizard")
            .WithTags("Admin - Game Wizard")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        // POST /api/v1/admin/games/wizard/create - Create game from BGG data
        group.MapPost("/create", HandleCreateGame)
            .WithName("WizardCreateGame")
            .WithSummary("Create a game from BGG data (Admin)")
            .WithDescription("Imports a game from BoardGameGeek into the shared catalog. Returns the created game ID for subsequent wizard steps.")
            .Produces<CreateGameFromWizardResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // POST /api/v1/admin/games/wizard/{gameId}/launch-processing - Launch PDF processing with admin priority
        group.MapPost("/{gameId:guid}/launch-processing", HandleLaunchProcessing)
            .WithName("WizardLaunchProcessing")
            .WithSummary("Launch PDF processing with admin priority (Admin)")
            .WithDescription("Sets admin priority on the PDF and triggers the extract + index pipeline.")
            .Produces<LaunchProcessingResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/games/wizard/{gameId}/progress/stream - SSE stream for wizard progress
        group.MapGet("/{gameId:guid}/progress/stream", StreamWizardProgress)
            .WithName("WizardProgressStream")
            .WithSummary("SSE stream for wizard-level processing progress (Admin)")
            .WithDescription("Real-time progress updates aggregating PDF processing state and agent existence. Polls every 1.5s, auto-closes after completion.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> HandleCreateGame(
        [FromBody] CreateGameFromWizardRequest request,
        HttpContext context,
        [FromServices] IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var userId = context.User.GetUserId();

        var command = new CreateGameFromWizardCommand(
            BggId: request.BggId,
            CreatedByUserId: userId);

        try
        {
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Wizard: Game created {GameId} from BGG {BggId}",
                result.SharedGameId, result.BggId);

            return Results.Created(
                $"/api/v1/admin/shared-games/{result.SharedGameId}",
                result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Duplicate Game",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    private static async Task<IResult> HandleLaunchProcessing(
        Guid gameId,
        [FromBody] LaunchProcessingRequest request,
        HttpContext context,
        [FromServices] IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var userId = context.User.GetUserId();

        var command = new LaunchAdminPdfProcessingCommand(
            GameId: gameId,
            PdfDocumentId: request.PdfDocumentId,
            LaunchedByUserId: userId);

        try
        {
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Wizard: Processing launched for PDF {PdfId} (Game {GameId}) with Admin priority",
                result.PdfDocumentId, result.GameId);

            return Results.Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound(new ProblemDetails
            {
                Title = "PDF Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// SSE endpoint for wizard-level progress tracking.
    /// Polls DB every 1.5s, aggregates PDF state + agent existence.
    /// Auto-closes after 5 idle polls when processing is complete.
    /// </summary>
    private static async Task StreamWizardProgress(
        Guid gameId,
        HttpContext httpContext,
        [FromServices] MeepleAiDbContext dbContext,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // Set SSE headers
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

        var consecutiveIdlePolls = 0;
        const int maxIdlePolls = 5;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Query the latest PDF for this game
                var pdfInfo = await dbContext.PdfDocuments
                    .Where(p => p.GameId == gameId)
                    .OrderByDescending(p => p.UploadedAt)
                    .Select(p => new
                    {
                        p.ProcessingState,
                        p.ProcessingError,
                        p.ProcessingPriority,
                        p.FileName
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                var pdfState = pdfInfo?.ProcessingState ?? "Pending";
                var isComplete = string.Equals(pdfState, "Ready", StringComparison.Ordinal);
                var isFailed = string.Equals(pdfState, "Failed", StringComparison.Ordinal);

                var progressEvent = new WizardProgressEvent
                {
                    CurrentStep = pdfState,
                    PdfState = pdfState,
                    AgentExists = false, // Will be populated by Phase 4 auto-agent creation
                    OverallPercent = MapStateToPercent(pdfState, false),
                    Message = BuildProgressMessage(pdfState, false, pdfInfo?.FileName),
                    IsComplete = isComplete,
                    ErrorMessage = isFailed ? pdfInfo?.ProcessingError : null,
                    Priority = pdfInfo?.ProcessingPriority ?? "Normal",
                    Timestamp = DateTime.UtcNow
                };

                // Send SSE event
                var eventName = isComplete ? "complete" : isFailed ? "error" : "progress";
                await httpContext.Response.WriteAsync(
                    $"event: {eventName}\ndata: {System.Text.Json.JsonSerializer.Serialize(progressEvent, SseJsonOptions.Default)}\n\n",
                    cancellationToken).ConfigureAwait(false);

                await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

                // Auto-close: if complete or failed, count idle polls
                if (isComplete || isFailed)
                {
                    consecutiveIdlePolls++;
                    if (consecutiveIdlePolls >= maxIdlePolls)
                    {
                        break;
                    }
                }
                else
                {
                    consecutiveIdlePolls = 0;
                }

                // Poll every 1.5 seconds
                await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected - expected
        }
    }

    /// <summary>
    /// Maps PDF processing state to overall wizard progress percentage.
    /// Pending=0%, Uploading=10%, Extracting=25%, Chunking=45%, Embedding=65%, Indexing=80%, Ready=90%, AgentCreated=100%
    /// </summary>
    private static int MapStateToPercent(string pdfState, bool agentExists)
    {
        var basePercent = pdfState switch
        {
            "Pending" => 0,
            "Uploading" => 10,
            "Extracting" => 25,
            "Chunking" => 45,
            "Embedding" => 65,
            "Indexing" => 80,
            "Ready" => agentExists ? 100 : 90,
            "Failed" => 0,
            _ => 0
        };

        return basePercent;
    }

    private static string BuildProgressMessage(string pdfState, bool agentExists, string? fileName)
    {
        return pdfState switch
        {
            "Pending" => "Waiting to start processing...",
            "Uploading" => $"Uploading {fileName ?? "PDF"}...",
            "Extracting" => "Extracting text and tables from PDF...",
            "Chunking" => "Splitting content into semantic chunks...",
            "Embedding" => "Generating vector embeddings...",
            "Indexing" => "Indexing embeddings in vector database...",
            "Ready" when agentExists => "Processing complete! Agent is ready.",
            "Ready" => "PDF processed. Creating AI agent...",
            "Failed" => "Processing failed. Check error details.",
            _ => "Processing..."
        };
    }
}

/// <summary>
/// Request model for wizard game creation.
/// </summary>
/// <param name="BggId">BoardGameGeek game ID to import</param>
public record CreateGameFromWizardRequest(int BggId);

/// <summary>
/// Request model for launching PDF processing.
/// </summary>
/// <param name="PdfDocumentId">ID of the uploaded PDF document</param>
public record LaunchProcessingRequest(Guid PdfDocumentId);
