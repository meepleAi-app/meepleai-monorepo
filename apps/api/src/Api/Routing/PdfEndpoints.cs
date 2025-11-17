using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.Extensions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PdfIndexingErrorCode = Api.BoundedContexts.DocumentProcessing.Application.DTOs.PdfIndexingErrorCode;

namespace Api.Routing;

/// <summary>
/// PDF management endpoints.
/// Handles PDF upload, retrieval, deletion, indexing, and rule spec generation.
/// </summary>
public static class PdfEndpoints
{
    public static RouteGroupBuilder MapPdfEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/ingest/pdf", async (HttpContext context, IPdfValidator pdfValidator, IMediator mediator, IFeatureFlagService featureFlags, ILogger<Program> logger, CancellationToken ct) =>
        {
            // CONFIG-05: Check if PDF upload feature is enabled (return 403 before auth to reflect feature gating)
            if (!await featureFlags.IsEnabledAsync("Features.PdfUpload"))
            {
                return Results.Json(
                    new { error = "feature_disabled", message = "PDF uploads are currently disabled", featureName = "Features.PdfUpload" },
                    statusCode: 403);
            }

            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

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

            if (!Guid.TryParse(session.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            logger.LogInformation("User {UserId} uploading PDF for game {GameId}", userId, gameId);

            // PDF-09: Comprehensive validation (DDD adapter)
            // Validates: magic bytes, file size, MIME type, page count, PDF version
            using var stream = file.OpenReadStream();
            var validation = await pdfValidator.ValidateAsync(stream, file.FileName, ct);

            if (!validation.IsValid)
            {
                logger.LogWarning("PDF validation failed for {FileName}: {@Errors}", file.FileName, validation.Errors);
                return Results.BadRequest(new { error = "validation_failed", details = validation.Errors });
            }

            // Reset stream position for processing
            stream.Position = 0;

            var result = await mediator.Send(new UploadPdfCommand(gameId, userId, file!), ct);

            if (!result.Success)
            {
                logger.LogWarning("PDF upload failed for game {GameId}: {Error}", gameId, result.Message);
                return Results.BadRequest(new { error = result.Message });
            }

            if (result.Document == null)
            {
                logger.LogError("PDF upload succeeded but Document is null for game {GameId}", gameId);
                return Results.Problem("Upload succeeded but document is missing", statusCode: 500);
            }

            logger.LogInformation("PDF uploaded successfully: {PdfId}", result.Document.Id);
            return Results.Json(new { documentId = result.Document.Id, fileName = result.Document.FileName });
        });

        // Note: Game listing is handled in GameEndpoints to avoid route duplication

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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Validate query parameter
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { error = "Query parameter 'q' is required" });
            }

            var results = await bggService.SearchGamesAsync(q, exact, ct);
            logger.LogInformation("BGG search returned {Count} results for query: {Query}", results.Count, q);
            return Results.Json(new { results });
        });

        group.MapGet("/bgg/games/{bggId:int}", async (
            int bggId,
            HttpContext context,
            IBggApiService bggService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Authentication required
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Validate BGG ID
            if (bggId <= 0)
            {
                return Results.BadRequest(new { error = "Invalid BGG ID. Must be a positive integer." });
            }

            var details = await bggService.GetGameDetailsAsync(bggId, ct);

            if (details == null)
            {
                logger.LogWarning("BGG game not found: {BggId}", bggId);
                return Results.NotFound(new { error = $"Game with BGG ID {bggId} not found" });
            }

            logger.LogInformation("BGG game details retrieved: {BggId}, {Name}", bggId, details.Name);
            return Results.Json(details);
        });

        group.MapGet("/games/{gameId:guid}/pdfs", async (Guid gameId, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var pdfs = await mediator.Send(new GetPdfDocumentsByGameQuery(gameId), ct);
            return Results.Json(new { pdfs });
        });

        group.MapGet("/pdfs/{pdfId:guid}/text", async (Guid pdfId, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Use CQRS Query to get PDF text
            var pdf = await mediator.Send(new GetPdfTextQuery(pdfId), ct);

            if (pdf == null)
            {
                return Results.NotFound(new { error = "PDF not found" });
            }

            return Results.Json(pdf);
        });

        // SEC-02: Delete PDF with Row-Level Security
        group.MapDelete("/pdf/{pdfId:guid}", async (Guid pdfId, HttpContext context, AuditService auditService, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Use CQRS Query to check ownership
            var pdf = await mediator.Send(new GetPdfOwnershipQuery(pdfId), ct);

            if (pdf == null)
            {
                return Results.NotFound(new { error = "PDF not found" });
            }

            if (!Guid.TryParse(session.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            // RLS: Check permissions
            bool isAdmin = string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
            bool isOwner = pdf.UploadedByUserId == userId;

            if (!isAdmin && !isOwner)
            {
                // Audit log access denial
                await auditService.LogAsync(
                    session.User.Id,
                    "ACCESS_DENIED",
                    "PdfDocument",
                    pdfId.ToString(),
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
            var result = await mediator.Send(new DeletePdfCommand(pdfId.ToString()), ct);

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
                pdfId.ToString(),
                "Success",
                $"PDF deleted successfully by user with role: {session.User.Role}",
                null,
                null,
                ct);

            return Results.NoContent();
        });

        // PDF-08: Get PDF processing progress
        group.MapGet("/pdfs/{pdfId:guid}/progress", async (Guid pdfId, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Use CQRS Query to get PDF progress
            var pdf = await mediator.Send(new GetPdfProgressQuery(pdfId), ct);

            if (pdf == null)
            {
                return Results.NotFound(new { error = "PDF not found" });
            }

            if (!Guid.TryParse(session.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            // Authorization: User can only view their own PDFs unless admin
            if (pdf.UploadedByUserId != userId &&
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
#pragma warning disable CA1031 // Do not catch general exception types
                // Justification: Resilience pattern - JSON deserialization failure shouldn't break PDF retrieval
                // Fail-open to return PDF metadata even if progress field is corrupted
                catch (Exception ex)
                {
                    // Resilience pattern: JSON deserialization failure shouldn't break PDF retrieval
                    // Fail-open to return PDF metadata even if progress field is corrupted
                    // Log error but return null progress instead of failing
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning(ex, "Failed to deserialize progress for PDF {PdfId}", pdfId);
                }
#pragma warning restore CA1031
            }

            return Results.Ok(progress);
        })
        .RequireAuthorization()
        .WithName("GetPdfProcessingProgress");

        // PDF-08: Cancel PDF processing
        group.MapDelete("/pdfs/{pdfId:guid}/processing", async (Guid pdfId, HttpContext context, IMediator mediator, IBackgroundTaskService backgroundTaskService, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Use CQRS Query to check ownership and status
            var pdf = await mediator.Send(new GetPdfOwnershipQuery(pdfId), ct);

            if (pdf == null)
            {
                return Results.NotFound(new { error = "PDF not found" });
            }

            if (!Guid.TryParse(session.User.Id, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            // Authorization: User can only cancel their own PDFs unless admin
            if (pdf.UploadedByUserId != userId &&
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
            var cancelled = backgroundTaskService.CancelTask(pdfId.ToString());

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

        group.MapPost("/ingest/pdf/{pdfId:guid}/rulespec", async (Guid pdfId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            logger.LogInformation("User {UserId} generating RuleSpec from PDF {PdfId}", session.User.Id, pdfId);
            var command = new GenerateRuleSpecFromPdfCommand(pdfId);
            var ruleSpecDto = await mediator.Send(command, ct);

            // Convert DTO to Model for backward compatibility
            var atoms = ruleSpecDto.Atoms.Select(a => new RuleAtom(a.Id, a.Text, a.Section, a.Page, a.Line)).ToList();
            var ruleSpec = new RuleSpec(ruleSpecDto.GameId.ToString(), ruleSpecDto.Version, ruleSpecDto.CreatedAt, atoms);

            return Results.Json(ruleSpec);
        });

        // AI-01: Index PDF for semantic search
        group.MapPost("/ingest/pdf/{pdfId:guid}/index", async (Guid pdfId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("User {UserId} with role {Role} attempted to index PDF without permission", session.User.Id, session.User.Role);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            logger.LogInformation("User {UserId} indexing PDF {PdfId}", session.User.Id, pdfId);

            var result = await mediator.Send(new IndexPdfCommand(pdfId.ToString()), ct);

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