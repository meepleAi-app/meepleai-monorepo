using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Extensions;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// REST endpoints for photo batch ingestion (Libro Game AI Assistant MVP Phase 1).
/// Exposes upload and status-polling for scanned rulebook photo batches.
///
/// Endpoints:
///   POST /api/v1/photo-batches       — submit a new photo batch
///   GET  /api/v1/photo-batches/{id}  — poll status / per-page results
/// </summary>
internal static class PhotoIngestionEndpoints
{
    public static RouteGroupBuilder MapPhotoIngestionEndpoints(this RouteGroupBuilder group)
    {
        // POST /photo-batches — upload a new batch
        group.MapPost("/photo-batches", HandleUploadBatch)
            .RequireAuthenticatedUser()
            .Produces<UploadPhotoBatchResult>(200)
            .Produces(400)
            .Produces(401)
            .WithTags("PhotoIngestion")
            .WithSummary("Submit a photo batch for OCR processing")
            .WithDescription("Accepts one or more base64-encoded page images, persists them to blob storage, and enqueues OCR processing.");

        // GET /photo-batches/{batchId} — poll status
        group.MapGet("/photo-batches/{batchId:guid}", HandleGetBatchStatus)
            .RequireAuthenticatedUser()
            .Produces<PhotoBatchStatusDto>(200)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .WithTags("PhotoIngestion")
            .WithSummary("Get photo batch processing status")
            .WithDescription("Returns the current status of a photo batch including per-page OCR results and thumbnail URLs. Only the batch owner may call this endpoint.");

        return group;
    }

    #region Handlers

    private static async Task<IResult> HandleUploadBatch(
        [FromBody] UploadPhotoBatchRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var userId = ExtractUserId(httpContext);
        if (userId == Guid.Empty)
            return Results.Unauthorized();

        var command = new UploadPhotoBatchCommand(
            UserId: userId,
            GameId: request.GameId,
            SourceLanguage: request.SourceLanguage,
            Photos: request.Photos);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetBatchStatus(
        Guid batchId,
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var userId = ExtractUserId(httpContext);
        if (userId == Guid.Empty)
            return Results.Unauthorized();

        try
        {
            var query = new GetPhotoBatchStatusQuery(userId, batchId);
            var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (NotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (ForbiddenException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden");
        }
    }

    #endregion

    /// <summary>
    /// Extracts the authenticated user's ID using MeepleAI session-based auth pattern.
    /// Falls back to claims principal for API-key-authenticated requests.
    /// Returns <see cref="Guid.Empty"/> if unauthenticated.
    /// </summary>
    private static Guid ExtractUserId(HttpContext httpContext)
    {
        var (authenticated, session, _) = httpContext.TryGetAuthenticatedUser();
        if (authenticated && session?.User != null)
            return session.User.Id;

        // Fallback: API key auth path (ClaimsPrincipal)
        var claimsUserId = httpContext.User.GetUserId();
        return claimsUserId;
    }
}

/// <summary>
/// Request body for <c>POST /api/v1/photo-batches</c>.
/// Internal because <see cref="PhotoUploadDto"/> is internal — same assembly, Minimal API binding works.
/// </summary>
internal sealed record UploadPhotoBatchRequest(
    Guid GameId,
    string SourceLanguage,
    PhotoUploadDto[] Photos
);
