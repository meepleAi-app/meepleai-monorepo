using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Routing;

/// <summary>
/// PDF management endpoints.
/// Handles PDF upload, retrieval, deletion, indexing, and rule spec generation.
/// </summary>
public static class PdfEndpoints
{
    public static RouteGroupBuilder MapPdfEndpoints(this RouteGroupBuilder group)
    {
group.MapPost("/ingest/pdf", async (HttpContext context, IPdfValidationService pdfValidation, PdfStorageService pdfStorage, IFeatureFlagService featureFlags, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // CONFIG-05: Check if PDF upload feature is enabled
    if (!await featureFlags.IsEnabledAsync("Features.PdfUpload"))
    {
        return Results.Json(
            new { error = "feature_disabled", message = "PDF uploads are currently disabled", featureName = "Features.PdfUpload" },
            statusCode: 403);
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var form = await context.Request.ReadFormAsync(ct);
    var file = form.Files.GetFile("file");
    var gameId = form["gameId"].ToString();

    if (string.IsNullOrWhiteSpace(gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string> { ["file"] = "No file provided" } });
    }

    logger.LogInformation("User {UserId} uploading PDF for game {GameId}", session.User.Id, gameId);

    // PDF-09: Validate file size
    var sizeValidation = pdfValidation.ValidateFileSize(file.Length);
    if (!sizeValidation.IsValid)
    {
        logger.LogWarning("PDF validation failed for {FileName}: File size validation failed", file.FileName);
        return Results.BadRequest(new { error = "validation_failed", details = sizeValidation.Errors });
    }

    // PDF-09: Validate MIME type
    var mimeValidation = pdfValidation.ValidateMimeType(file.ContentType);
    if (!mimeValidation.IsValid)
    {
        logger.LogWarning("PDF validation failed for {FileName}: MIME type validation failed", file.FileName);
        return Results.BadRequest(new { error = "validation_failed", details = mimeValidation.Errors });
    }

    // PDF-09: Deep validation with PDF content
    using var stream = file.OpenReadStream();
    var validation = await pdfValidation.ValidateAsync(stream, file.FileName, ct);

    if (!validation.IsValid)
    {
        logger.LogWarning("PDF validation failed for {FileName}: {@Errors}", file.FileName, validation.Errors);
        return Results.BadRequest(new { error = "validation_failed", details = validation.Errors });
    }

    // Reset stream position for processing
    stream.Position = 0;

    var result = await pdfStorage.UploadPdfAsync(gameId, session.User.Id, file!, ct);

    if (!result.Success)
    {
        logger.LogWarning("PDF upload failed for game {GameId}: {Error}", gameId, result.Message);
        return Results.BadRequest(new { error = result.Message });
    }

    logger.LogInformation("PDF uploaded successfully: {PdfId}", result.Document!.Id);
    return Results.Json(new { documentId = result.Document.Id, fileName = result.Document.FileName });
});

group.MapGet("/games", async (HttpContext context, GameService gameService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession)
    {
        return Results.Unauthorized();
    }

    var games = await gameService.GetGamesAsync(ct);
    var response = games.Select(g => new GameResponse(g.Id, g.Name, g.CreatedAt)).ToList();
    return Results.Json(response);
});

group.MapPost("/games", async (CreateGameRequest? request, HttpContext context, GameService gameService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning(
            "User {UserId} with role {Role} attempted to create a game without permission",
            session.User.Id,
            session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request is null)
    {
        return Results.BadRequest(new { error = "Request body is required" });
    }

    try
    {
        var game = await gameService.CreateGameAsync(request.Name, request.GameId, ct);
        logger.LogInformation("Created game {GameId}", game.Id);
        return Results.Created($"/games/{game.Id}", new GameResponse(game.Id, game.Name, game.CreatedAt));
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning(ex, "Invalid game creation request");
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(ex, "Conflict creating game");
        return Results.Conflict(new { error = ex.Message });
    }
});

// AI-13: BoardGameGeek API endpoints
group.MapGet("/bgg/search", async (
    HttpContext context,
    [FromQuery] string? q,
    [FromQuery] bool exact,
    IBggApiService bggService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    // Authentication required
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // Validate query parameter
    if (string.IsNullOrWhiteSpace(q))
    {
        return Results.BadRequest(new { error = "Query parameter 'q' is required" });
    }

    try
    {
        var results = await bggService.SearchGamesAsync(q, exact, ct);
        logger.LogInformation("BGG search returned {Count} results for query: {Query}", results.Count, q);
        return Results.Json(new { results });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogError(ex, "BGG API unavailable for search query: {Query}", q);
        return Results.Json(new
        {
            error = "BoardGameGeek API is currently unavailable. Please try again later.",
            details = ex.Message
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (BggApiService)
        logger.LogError(ex, "Unexpected error during BGG search: {Query}", q);
        return Results.Json(new
        {
            error = "An unexpected error occurred while searching BoardGameGeek."
        }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

group.MapGet("/bgg/games/{bggId:int}", async (
    int bggId,
    HttpContext context,
    IBggApiService bggService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    // Authentication required
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // Validate BGG ID
    if (bggId <= 0)
    {
        return Results.BadRequest(new { error = "Invalid BGG ID. Must be a positive integer." });
    }

    try
    {
        var details = await bggService.GetGameDetailsAsync(bggId, ct);

        if (details == null)
        {
            logger.LogWarning("BGG game not found: {BggId}", bggId);
            return Results.NotFound(new { error = $"Game with BGG ID {bggId} not found" });
        }

        logger.LogInformation("BGG game details retrieved: {BggId}, {Name}", bggId, details.Name);
        return Results.Json(details);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogError(ex, "BGG API unavailable for game ID: {BggId}", bggId);
        return Results.Json(new
        {
            error = "BoardGameGeek API is currently unavailable. Please try again later.",
            details = ex.Message
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
        // Specific exception handling occurs in service layer (BggApiService)
        logger.LogError(ex, "Unexpected error retrieving BGG game details: {BggId}", bggId);
        return Results.Json(new
        {
            error = "An unexpected error occurred while retrieving game details."
        }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

group.MapGet("/games/{gameId}/pdfs", async (string gameId, HttpContext context, PdfStorageService pdfStorage, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var pdfs = await pdfStorage.GetPdfsByGameAsync(gameId, ct);
    return Results.Json(new { pdfs });
});

group.MapGet("/pdfs/{pdfId}/text", async (string pdfId, HttpContext context, MeepleAiDbContext db, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var pdf = await db.PdfDocuments
        .Where(p => p.Id == pdfId)
        .Select(p => new
        {
            p.Id,
            p.FileName,
            p.ExtractedText,
            p.ProcessingStatus,
            p.ProcessedAt,
            p.PageCount,
            p.CharacterCount,
            p.ProcessingError
        })
        .FirstOrDefaultAsync(ct);

    if (pdf == null)
    {
        return Results.NotFound(new { error = "PDF not found" });
    }

    return Results.Json(pdf);
});

// SEC-02: Delete PDF with Row-Level Security
group.MapDelete("/pdf/{pdfId}", async (string pdfId, HttpContext context, PdfStorageService pdfStorage, AuditService auditService, MeepleAiDbContext db, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // Load PDF to check ownership
    var pdf = await db.PdfDocuments
        .Where(p => p.Id == pdfId)
        .Select(p => new { p.Id, p.UploadedByUserId, p.GameId })
        .FirstOrDefaultAsync(ct);

    if (pdf == null)
    {
        return Results.NotFound(new { error = "PDF not found" });
    }

    // RLS: Check permissions
    bool isAdmin = string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    bool isOwner = pdf.UploadedByUserId == session.User.Id;

    if (!isAdmin && !isOwner)
    {
        // Audit log access denial
        await auditService.LogAsync(
            session.User.Id,
            "ACCESS_DENIED",
            "PdfDocument",
            pdfId,
            "Denied",
            $"User attempted to delete PDF owned by another user. User role: {session.User.Role}, Owner: {pdf.UploadedByUserId}. RLS scope: own resources only.",
            null,
            null,
            ct);

        logger.LogWarning("User {UserId} with role {Role} denied access to delete PDF {PdfId} (owner: {OwnerId})",
            session.User.Id, session.User.Role, pdfId, pdf.UploadedByUserId);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // Delete PDF
    var result = await pdfStorage.DeletePdfAsync(pdfId, ct);

    if (!result.Success)
    {
        logger.LogError("Failed to delete PDF {PdfId}: {Error}", pdfId, result.Message);
        return Results.BadRequest(new { error = result.Message });
    }

    logger.LogInformation("User {UserId} deleted PDF {PdfId}", session.User.Id, pdfId);

    // Audit log successful deletion
    await auditService.LogAsync(
        session.User.Id,
        "DELETE",
        "PdfDocument",
        pdfId,
        "Success",
        $"PDF deleted successfully by user with role: {session.User.Role}",
        null,
        null,
        ct);

    return Results.NoContent();
});

// PDF-08: Get PDF processing progress
group.MapGet("/pdfs/{pdfId}/progress", async (string pdfId, HttpContext context, MeepleAiDbContext db, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var pdf = await db.PdfDocuments
        .Where(p => p.Id == pdfId)
        .Select(p => new
        {
            p.Id,
            p.UploadedByUserId,
            p.ProcessingProgressJson
        })
        .FirstOrDefaultAsync(ct);

    if (pdf == null)
    {
        return Results.NotFound(new { error = "PDF not found" });
    }

    // Authorization: User can only view their own PDFs unless admin
    if (pdf.UploadedByUserId != session.User.Id &&
        !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.Forbid();
    }

    // Deserialize progress from JSON
    ProcessingProgress? progress = null;
    if (!string.IsNullOrEmpty(pdf.ProcessingProgressJson))
    {
        try
        {
            progress = System.Text.Json.JsonSerializer.Deserialize<ProcessingProgress>(pdf.ProcessingProgressJson);
        }
        catch (Exception ex)
        {
            // Resilience pattern: JSON deserialization failure shouldn't break PDF retrieval
            // Fail-open to return PDF metadata even if progress field is corrupted
            // Log error but return null progress instead of failing
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "Failed to deserialize progress for PDF {PdfId}", pdfId);
        }
    }

    return Results.Ok(progress);
})
.RequireAuthorization()
.WithName("GetPdfProcessingProgress");

// PDF-08: Cancel PDF processing
group.MapDelete("/pdfs/{pdfId}/processing", async (string pdfId, HttpContext context, MeepleAiDbContext db, IBackgroundTaskService backgroundTaskService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var pdf = await db.PdfDocuments
        .Where(p => p.Id == pdfId)
        .Select(p => new { p.Id, p.UploadedByUserId, p.ProcessingStatus })
        .FirstOrDefaultAsync(ct);

    if (pdf == null)
    {
        return Results.NotFound(new { error = "PDF not found" });
    }

    // Authorization: User can only cancel their own PDFs unless admin
    if (pdf.UploadedByUserId != session.User.Id &&
        !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.Forbid();
    }

    // Check if processing is active
    if (pdf.ProcessingStatus == "completed" || pdf.ProcessingStatus == "failed")
    {
        return Results.BadRequest(new { error = "Processing already completed or failed" });
    }

    // Cancel the background task
    var cancelled = backgroundTaskService.CancelTask(pdfId);

    if (!cancelled)
    {
        logger.LogWarning("Failed to cancel processing for PDF {PdfId} - task not found", pdfId);
        return Results.BadRequest(new { error = "Processing task not found or already completed" });
    }

    logger.LogInformation("User {UserId} cancelled processing for PDF {PdfId}", session.User.Id, pdfId);

    return Results.Ok(new { message = "Processing cancellation requested" });
})
.RequireAuthorization()
.WithName("CancelPdfProcessing");

group.MapPost("/ingest/pdf/{pdfId}/rulespec", async (string pdfId, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("User {UserId} generating RuleSpec from PDF {PdfId}", session.User.Id, pdfId);
        var ruleSpec = await ruleSpecService.GenerateRuleSpecFromPdfAsync(pdfId, ct);
        return Results.Json(ruleSpec);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(ex, "Unable to generate RuleSpec for PDF {PdfId}", pdfId);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// AI-01: Index PDF for semantic search
group.MapPost("/ingest/pdf/{pdfId}/index", async (string pdfId, HttpContext context, PdfIndexingService indexingService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to index PDF without permission", session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("User {UserId} indexing PDF {PdfId}", session.User.Id, pdfId);

    var result = await indexingService.IndexPdfAsync(pdfId, ct);

    if (!result.Success)
    {
        logger.LogWarning("PDF indexing failed for {PdfId}: {Error}", pdfId, result.ErrorMessage);

        return result.ErrorCode switch
        {
            PdfIndexingErrorCode.PdfNotFound => Results.NotFound(new { error = result.ErrorMessage }),
            PdfIndexingErrorCode.TextExtractionRequired => Results.BadRequest(new { error = result.ErrorMessage }),
            _ => Results.BadRequest(new { error = result.ErrorMessage })
        };
    }

    logger.LogInformation("PDF {PdfId} indexed successfully: {ChunkCount} chunks", pdfId, result.ChunkCount);

    return Results.Json(new
    {
        success = true,
        vectorDocumentId = result.VectorDocumentId,
        chunkCount = result.ChunkCount,
        indexedAt = result.IndexedAt
    });
});

        return group;
    }
}
