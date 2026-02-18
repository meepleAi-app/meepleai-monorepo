using Api.BoundedContexts.Administration.Application.Commands.GameWizard;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

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
