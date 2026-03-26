using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Extensions;
using Api.Middleware.Exceptions;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// PDF Wizard and RAG upload endpoints for SharedGameCatalog (Issue #4139).
/// </summary>
internal static class SharedGameCatalogWizardEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        // Step 1: Upload PDF for temporary storage (no processing)
        group.MapPost("/admin/shared-games/wizard/upload-pdf", HandleWizardUploadPdf)
            .DisableAntiforgery() // Required for multipart/form-data file uploads
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("SharedGamesWizardUploadPdf")
            .WithSummary("Upload PDF for wizard (Admin/Editor)")
            .WithDescription("Uploads PDF to temporary storage for metadata extraction. File path returned for subsequent wizard steps.")
            .Produces<TempPdfUploadResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // Step 2: Get PDF preview with extracted metadata and BGG suggestions
        group.MapGet("/admin/shared-games/wizard/pdf/preview", HandleWizardPreview)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("WizardGetPdfPreview")
            .WithSummary("Get PDF preview with metadata (Admin/Editor)")
            .WithDescription("Extracts game metadata from uploaded PDF, fetches BGG match suggestions (top 5), and checks for duplicate games by title.")
            .Produces<PdfGamePreviewDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // Step 3a: Search BGG games by title
        group.MapGet("/admin/shared-games/wizard/bgg/search", HandleWizardBggSearch)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("WizardSearchBgg")
            .WithSummary("Search BGG games (Admin/Editor)")
            .WithDescription("Searches BoardGameGeek API for games matching the query. Returns top matching results for manual BGG ID selection.")
            .Produces<List<BggSearchResultDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        // Step 3b: Get BGG game details by ID
        group.MapGet("/admin/shared-games/wizard/bgg/{bggId:int}", HandleWizardBggDetails)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("WizardGetBggDetails")
            .WithSummary("Get BGG game details (Admin/Editor)")
            .WithDescription("Fetches detailed game information from BoardGameGeek API for preview and data merge.")
            .Produces<BggGameDetailsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // Step 4: Create SharedGame from PDF metadata (final wizard step)
        group.MapPost("/admin/shared-games/wizard/create", HandleWizardCreateGame)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("SharedGameWizardCreateGame")
            .WithSummary("Create game from PDF wizard (Admin/Editor)")
            .WithDescription("Creates SharedGame from extracted metadata with optional BGG enrichment. Admin users publish immediately, Editor users create draft requiring approval.")
            .Produces<CreateGameFromPdfResult>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);

        // RAG Wizard: Single upload + process
        group.MapPost("/admin/shared-games/{id:guid}/rag", HandleAddRagToSharedGame)
            .DisableAntiforgery() // Required for multipart/form-data file uploads
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("AddRagToSharedGame")
            .WithSummary("Upload PDF and start RAG processing for a shared game (Admin/Editor)")
            .WithDescription("Uploads a PDF, links it as a document to the shared game, and enqueues for RAG processing. Admin uploads are auto-approved; Editor uploads require separate approval.")
            .Produces<AddRagToSharedGameResult>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // RAG Wizard: Batch upload + process
        group.MapPost("/admin/shared-games/batch-rag", HandleBatchAddRagToSharedGame)
            .DisableAntiforgery() // Required for multipart/form-data file uploads
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("BatchAddRagToSharedGame")
            .WithSummary("Batch upload PDFs and start RAG processing for multiple shared games (Admin/Editor)")
            .WithDescription("Uploads PDFs for multiple shared games in a single request. Supports partial success — individual failures don't stop the batch.")
            .Produces<BatchAddRagToSharedGameResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }

    // ========================================
    // PDF WIZARD HANDLERS (Issue #4139)
    // ========================================

    /// <summary>
    /// Handler for wizard PDF upload endpoint.
    /// Uploads PDF to temporary storage without processing.
    /// </summary>
    private static async Task<IResult> HandleWizardUploadPdf(
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

        // Null check BEFORE accessing file.Length
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

        logger.LogInformation("Wizard PDF uploaded successfully: FileId={FileId}, FilePath={FilePath}", result.FileId, result.FilePath);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handler for wizard PDF preview endpoint.
    /// Extracts metadata, fetches BGG suggestions, and checks duplicates.
    /// </summary>
    private static async Task<IResult> HandleWizardPreview(
        [FromQuery] string filePath,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var userId = context.User.GetUserId();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["filePath"] = "File path is required" } });
        }

        logger.LogInformation("Generating wizard preview for FilePath={FilePath}, UserId={UserId}", filePath, userId);

        var query = new GetPdfPreviewForWizardQuery(filePath, userId);
        var preview = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(preview);
    }

    /// <summary>
    /// Handler for wizard BGG search endpoint.
    /// Searches BoardGameGeek API for games matching query.
    /// </summary>
    private static async Task<IResult> HandleWizardBggSearch(
        [FromQuery] string query,
        [FromQuery] bool exact,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["query"] = "Search query is required" } });
        }

        logger.LogInformation("Wizard BGG search: Query={Query}, Exact={Exact}", query, exact);

        try
        {
            var searchQuery = new SearchBggGamesQuery(query, exact);
            var results = await mediator.Send(searchQuery, ct).ConfigureAwait(false);
            return Results.Ok(results);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit"))
        {
            logger.LogWarning(ex, "BGG API rate limit exceeded for wizard search");
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }
    }

    /// <summary>
    /// Handler for wizard BGG game details endpoint.
    /// Fetches detailed game information from BoardGameGeek API.
    /// </summary>
    private static async Task<IResult> HandleWizardBggDetails(
        int bggId,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (bggId <= 0)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["bggId"] = "BGG ID must be a positive integer" } });
        }

        logger.LogInformation("Fetching wizard BGG details: BggId={BggId}", bggId);

        var query = new GetBggGameDetailsQuery(bggId);
        var details = await mediator.Send(query, ct).ConfigureAwait(false);

        if (details == null)
        {
            logger.LogWarning("BGG game not found: BggId={BggId}", bggId);
            return Results.NotFound(new { error = "BGG game not found", bggId });
        }

        return Results.Ok(details);
    }

    /// <summary>
    /// Handler for wizard game creation endpoint (final step).
    /// Creates SharedGame from extracted metadata with optional BGG enrichment.
    /// </summary>
    private static async Task<IResult> HandleWizardCreateGame(
        CreateGameFromPdfRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var userId = context.User.GetUserId();

        // Determine if user is Admin (auto-publish) or Editor (requires approval)
        var isAdmin = context.User.IsInRole("Admin");
        var requiresApproval = !isAdmin; // Editors require approval, Admins auto-publish

        logger.LogInformation(
            "Wizard create game: PdfId={PdfId}, Title='{Title}', BggId={BggId}, UserId={UserId}, RequiresApproval={RequiresApproval}",
            request.PdfDocumentId, request.ExtractedTitle, request.SelectedBggId, userId, requiresApproval);

        var command = new CreateSharedGameFromPdfCommand(
            request.PdfDocumentId,
            userId,
            request.ExtractedTitle,
            request.MinPlayers,
            request.MaxPlayers,
            request.PlayingTimeMinutes,
            request.MinAge,
            request.SelectedBggId,
            requiresApproval);

        try
        {
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Wizard game created successfully: GameId={GameId}, Status={Status}, BggEnriched={BggEnriched}",
                result.GameId, result.ApprovalStatus, result.BggEnrichmentApplied);

            return Results.Created($"/api/v1/admin/shared-games/{result.GameId}", result);
        }
        catch (ConflictException ex)
        {
            logger.LogWarning(ex, "Wizard create game conflict: {Message}", ex.Message);
            return Results.Conflict(new { error = ex.Message });
        }
    }

    // ========================================
    // RAG WIZARD HANDLERS
    // ========================================

    /// <summary>
    /// Handler for single RAG upload + process endpoint.
    /// Uploads PDF, links to SharedGame, and enqueues for RAG processing.
    /// </summary>
    private static async Task<IResult> HandleAddRagToSharedGame(
        Guid id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var form = await context.Request.ReadFormAsync(ct).ConfigureAwait(false);

        var file = form.Files.GetFile("file");
        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["file"] = "No file provided or file is empty" } });
        }

        var documentTypeStr = form["documentType"].ToString();
        if (string.IsNullOrWhiteSpace(documentTypeStr) || !Enum.TryParse<SharedGameDocumentType>(documentTypeStr, ignoreCase: true, out var documentType))
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["documentType"] = "Invalid or missing document type" } });
        }

        var version = form["version"].ToString();
        if (string.IsNullOrWhiteSpace(version))
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["version"] = "Version is required" } });
        }

        var tagsStr = form["tags"].ToString();
        List<string>? tags = string.IsNullOrWhiteSpace(tagsStr)
            ? null
            : tagsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var isAdmin = context.User.IsInRole("Admin");

        var command = new AddRagToSharedGameCommand(
            id,
            file,
            documentType,
            version,
            tags,
            session!.User!.Id,
            isAdmin);

        logger.LogInformation(
            "RAG wizard: SharedGameId={SharedGameId}, File={FileName}, DocType={DocType}, IsAdmin={IsAdmin}",
            id, file.FileName, documentType, isAdmin);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation(
            "RAG wizard completed: PdfId={PdfId}, DocId={DocId}, AutoApproved={AutoApproved}",
            result.PdfDocumentId, result.SharedGameDocumentId, result.AutoApproved);

        return Results.Accepted($"/api/v1/admin/shared-games/{id}/rag", result);
    }

    /// <summary>
    /// Handler for batch RAG upload + process endpoint.
    /// Uploads PDFs for multiple shared games in a single request.
    /// </summary>
    private static async Task<IResult> HandleBatchAddRagToSharedGame(
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var form = await context.Request.ReadFormAsync(ct).ConfigureAwait(false);

        var sharedGameIdStrings = form["sharedGameIds"].ToList();
        var files = form.Files.GetFiles("files").ToList();
        var documentTypes = form["documentTypes"].ToList();
        var versions = form["versions"].ToList();

        if (sharedGameIdStrings.Count == 0 || files.Count == 0)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["items"] = "At least one shared game ID and file are required" } });
        }

        if (sharedGameIdStrings.Count != files.Count || sharedGameIdStrings.Count != documentTypes.Count || sharedGameIdStrings.Count != versions.Count)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["items"] = "sharedGameIds, files, documentTypes, and versions must have the same count" } });
        }

        var isAdmin = context.User.IsInRole("Admin");
        var userId = session!.User!.Id;
        var items = new List<AddRagToSharedGameCommand>();

        for (var i = 0; i < sharedGameIdStrings.Count; i++)
        {
            if (!Guid.TryParse(sharedGameIdStrings[i], out var sharedGameId))
            {
                return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { [$"sharedGameIds[{i}]"] = "Invalid GUID" } });
            }

            if (!Enum.TryParse<SharedGameDocumentType>(documentTypes[i], ignoreCase: true, out var docType))
            {
                return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { [$"documentTypes[{i}]"] = "Invalid document type" } });
            }

            items.Add(new AddRagToSharedGameCommand(
                sharedGameId,
                files[i],
                docType,
                versions[i] ?? "1.0",
                null,
                userId,
                isAdmin));
        }

        var batchCommand = new BatchAddRagToSharedGameCommand(items);

        logger.LogInformation(
            "Batch RAG wizard: {Count} items, IsAdmin={IsAdmin}, UserId={UserId}",
            items.Count, isAdmin, userId);

        var result = await mediator.Send(batchCommand, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Batch RAG wizard completed: Success={Success}, Failed={Failed}",
            result.SuccessCount, result.FailureCount);

        return Results.Ok(result);
    }
}
