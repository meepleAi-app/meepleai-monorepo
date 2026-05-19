using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
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
///   POST /api/v1/photo-batches                                                — submit a new photo batch
///   GET  /api/v1/photo-batches/{id}                                           — poll status / per-page results
///   GET  /api/v1/photo-batches/{id}/paragraphs/{pageNumber}                   — retrieve OCR text by physical page (G4 legacy)
///   GET  /api/v1/photo-batches/{id}/paragraphs/by-paragraph/{paragraphNumber} — retrieve OCR text by narrative paragraph (#747)
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

        // GET /photo-batches/{batchId}/paragraphs/{pageNumber} — retrieve OCR text for a page (G4)
        group.MapGet("/photo-batches/{batchId:guid}/paragraphs/{pageNumber:int}", HandleGetParagraphByPage)
            .RequireAuthenticatedUser()
            .Produces<ParagraphDto>(200)
            .Produces(400)
            .Produces(401)
            .Produces(404)
            .WithTags("PhotoIngestion")
            .WithSummary("Get OCR text for a specific page of a photo batch")
            .WithDescription(
                "Returns the extracted OCR text for a single page of a scanned rulebook photo batch. " +
                "When the numbered page text is unavailable a semantic RAG search is used as fallback. " +
                "Only the batch owner may call this endpoint. " +
                "Libro Game AI Assistant MVP Phase 3 — G4.");

        // GET /photo-batches/{batchId}/paragraphs/by-paragraph/{paragraphNumber} — retrieve OCR text by narrative paragraph (#747)
        group.MapGet("/photo-batches/{batchId:guid}/paragraphs/by-paragraph/{paragraphNumber:int}", HandleGetParagraphByParagraph)
            .RequireAuthenticatedUser()
            .Produces<ParagraphDto>(200)
            .Produces(400)
            .Produces(401)
            .Produces(404)
            .WithTags("PhotoIngestion")
            .WithSummary("Get OCR text by narrative paragraph number")
            .WithDescription(
                "Returns the extracted OCR text of the first photo whose `paragraph_numbers` array contains the requested narrative paragraph. " +
                "When no page carries the paragraph a semantic RAG search is used as fallback. " +
                "Only the batch owner may call this endpoint. " +
                "Issue #747 PR-B.");

        return group;
    }

    #region Handlers

    private static Task<IResult> HandleGetParagraphByPage(
        Guid batchId,
        int pageNumber,
        [FromQuery] string? hint,
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
        => DispatchParagraphQueryAsync(
            httpContext,
            mediator,
            userId => GetParagraphQuery.ByPage(batchId, pageNumber, userId, hint),
            cancellationToken);

    private static Task<IResult> HandleGetParagraphByParagraph(
        Guid batchId,
        int paragraphNumber,
        [FromQuery] string? hint,
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
        => DispatchParagraphQueryAsync(
            httpContext,
            mediator,
            userId => GetParagraphQuery.ByParagraph(batchId, paragraphNumber, userId, hint),
            cancellationToken);

    private static async Task<IResult> DispatchParagraphQueryAsync(
        HttpContext httpContext,
        IMediator mediator,
        Func<Guid, GetParagraphQuery> queryFactory,
        CancellationToken cancellationToken)
    {
        var userId = ExtractUserId(httpContext);
        if (userId == Guid.Empty)
            return Results.Unauthorized();

        try
        {
            var result = await mediator.Send(queryFactory(userId), cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (NotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }

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
