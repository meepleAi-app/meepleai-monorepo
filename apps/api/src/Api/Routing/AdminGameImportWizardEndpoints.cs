using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin Game Import Wizard endpoints for step-by-step guided import workflow.
/// Issue #4157: Backend - Wizard Endpoints Routing
/// </summary>
internal static class AdminGameImportWizardEndpoints
{
    internal static void MapAdminGameImportWizardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/games/wizard")
            .WithTags("Admin - Game Import Wizard")
            .RequireAuthorization("AdminOnlyPolicy");

        // Step 1: Upload PDF for temporary storage (no quota checks, no processing)
        group.MapPost("/upload-pdf", HandleUploadPdf)
            .DisableAntiforgery() // Required for multipart/form-data file uploads
            .WithName("WizardUploadPdf")
            .WithSummary("Step 1: Upload PDF for metadata extraction (Admin only)")
            .WithDescription("Uploads PDF to temporary storage without quota checks or background processing. File auto-deletes after 24 hours.")
            .Produces<TempPdfUploadResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // Step 2: Extract game metadata from uploaded PDF
        group.MapPost("/extract-metadata", HandleExtractMetadata)
            .WithName("WizardExtractMetadata")
            .WithSummary("Step 2: Extract game metadata from PDF (Admin only)")
            .WithDescription("Uses SmolDocling + AI parsing to extract structured game metadata (title, year, players, etc.) from uploaded PDF.")
            .Produces<GameMetadataDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // Step 3: Enrich extracted metadata with BGG data
        group.MapPost("/enrich-from-bgg", HandleEnrichFromBgg)
            .WithName("WizardEnrichFromBgg")
            .WithSummary("Step 3: Enrich metadata with BGG data (Admin only)")
            .WithDescription("Merges PDF-extracted metadata with BoardGameGeek data, detects conflicts, and returns enriched result.")
            .Produces<EnrichedGameDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // Step 4: Confirm and import game into catalog (uses existing command)
        group.MapPost("/confirm-import", HandleConfirmImport)
            .WithName("WizardConfirmImport")
            .WithSummary("Step 4: Confirm and import game (Admin only)")
            .WithDescription("Creates SharedGame entity in Draft status from enriched metadata. Final step of wizard workflow.")
            .Produces<Guid>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);
    }

    /// <summary>
    /// Step 1: Upload PDF for temporary storage during wizard workflow.
    /// </summary>
    private static async Task<IResult> HandleUploadPdf(
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var userId = context.User.GetUserId();

        // Read multipart/form-data
        var form = await context.Request.ReadFormAsync(ct).ConfigureAwait(false);

        // Defensive check: form.Files could be null or empty
        if (form.Files == null || form.Files.Count == 0)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["file"] = "No file provided" } });
        }

        var file = form.Files.GetFile("file");

        // Null check BEFORE accessing file.Length to prevent NullReferenceException
        if (file == null)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["file"] = "No file provided" } });
        }

        if (file.Length == 0)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["file"] = "File is empty" } });
        }

        var command = new UploadPdfForGameExtractionCommand(file, userId);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("Wizard PDF upload failed: {Error}", result.ErrorMessage);
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        logger.LogInformation("Wizard PDF uploaded successfully: {FilePath}", result.FilePath);
        return Results.Ok(result);
    }

    /// <summary>
    /// Step 2: Extract game metadata from uploaded PDF using SmolDocling + AI parsing.
    /// </summary>
    private static async Task<IResult> HandleExtractMetadata(
        [FromBody] ExtractMetadataRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var userId = context.User.GetUserId();

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["filePath"] = "File path is required" } });
        }

        var query = new ExtractGameMetadataFromPdfQuery(request.FilePath, userId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        logger.LogInformation("Metadata extracted from PDF: {Title} (confidence: {Confidence})", result.Title, result.ConfidenceScore);
        return Results.Ok(result);
    }

    /// <summary>
    /// Step 3: Enrich extracted metadata with BGG data and detect conflicts.
    /// </summary>
    private static async Task<IResult> HandleEnrichFromBgg(
        [FromBody] EnrichFromBggRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var userId = context.User.GetUserId();

        if (request.ExtractedMetadata == null)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["extractedMetadata"] = "Extracted metadata is required" } });
        }

        if (request.BggId <= 0)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["bggId"] = "Valid BGG ID is required" } });
        }

        var command = new EnrichGameMetadataFromBggCommand(request.ExtractedMetadata, request.BggId, userId);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("Metadata enriched with BGG data: {BggId}, conflicts: {ConflictCount}", request.BggId, result.Conflicts?.Count ?? 0);
        return Results.Ok(result);
    }

    /// <summary>
    /// Step 4: Confirm import and create SharedGame entity from enriched metadata.
    /// Uses existing ImportGameFromBggCommand for final import step.
    /// </summary>
    private static async Task<IResult> HandleConfirmImport(
        [FromBody] ConfirmImportRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var userId = context.User.GetUserId();

        if (request.BggId <= 0)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["bggId"] = "Valid BGG ID is required" } });
        }

        // Reuse existing ImportGameFromBggCommand for final import
        var command = new ImportGameFromBggCommand(request.BggId, userId);
        var gameId = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("Game imported successfully via wizard: {GameId} (BGG ID: {BggId})", gameId, request.BggId);
        return Results.Created($"/api/v1/admin/shared-games/{gameId}", gameId);
    }
}

/// <summary>
/// Request model for Step 2: Extract Metadata endpoint.
/// </summary>
/// <param name="FilePath">Path to uploaded PDF from Step 1</param>
public record ExtractMetadataRequest(string FilePath);

/// <summary>
/// Request model for Step 3: Enrich from BGG endpoint.
/// </summary>
/// <param name="ExtractedMetadata">Game metadata from Step 2</param>
/// <param name="BggId">BoardGameGeek game ID to fetch enrichment data</param>
public record EnrichFromBggRequest(GameMetadataDto ExtractedMetadata, int BggId);

/// <summary>
/// Request model for Step 4: Confirm Import endpoint.
/// </summary>
/// <param name="BggId">BoardGameGeek game ID to import</param>
public record ConfirmImportRequest(int BggId);
