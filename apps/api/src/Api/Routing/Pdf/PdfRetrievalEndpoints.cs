using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Extensions;
using Api.Infrastructure.Entities;
using Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// PDF retrieval, lifecycle, and admin list endpoints.
/// Covers: list, text, download, delete, visibility, language, disclaimer, RAG active, reclassify, admin list.
/// </summary>
internal static class PdfRetrievalEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapRetrievalEndpoints(group);
        MapLifecycleEndpoints(group);
        MapAdminPdfListEndpoint(group); // Admin: List all PDFs with status
    }

    private static void MapRetrievalEndpoints(RouteGroupBuilder group)
    {
        MapPdfListEndpoint(group);
        MapPdfTextEndpoint(group);
        MapPdfDownloadEndpoint(group);
    }

    private static void MapPdfListEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/games/{gameId:guid}/pdfs", HandleListPdfs)
        .RequireSession(); // Issue #1446: Automatic session validation
    }

    private static void MapPdfTextEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/pdfs/{pdfId:guid}/text", HandleGetPdfText)
        .RequireSession(); // Issue #1446: Automatic session validation
    }

    private static void MapPdfDownloadEndpoint(RouteGroupBuilder group)
    {
        // BGAI-074: Download/view PDF file
        group.MapGet("/pdfs/{pdfId:guid}/download", HandleDownloadPdf)
        .RequireSession()
        .RequireAuthorization()
        .WithName("DownloadPdf");
    }

    private static void MapLifecycleEndpoints(RouteGroupBuilder group)
    {
        MapPdfDeletionEndpoints(group);
        MapPdfVisibilityEndpoints(group);
        MapPdfLanguageEndpoints(group);
    }

    private static void MapPdfLanguageEndpoints(RouteGroupBuilder group)
    {
        // E5-2: Override detected language of a PDF document
        group.MapPut("/pdfs/{pdfId:guid}/language", HandleOverridePdfLanguage)
        .RequireSession()
        .RequireAuthorization()
        .WithName("OverridePdfLanguage");
    }

    private static void MapPdfDeletionEndpoints(RouteGroupBuilder group)
    {
        // SEC-02: Delete PDF with Row-Level Security
        group.MapDelete("/pdf/{pdfId:guid}", HandleDeletePdf)
        .RequireSession();
    }

    private static void MapPdfVisibilityEndpoints(RouteGroupBuilder group)
    {
        // Admin Wizard: Set PDF visibility in public library
        group.MapPatch("/pdfs/{pdfId:guid}/visibility", HandleSetPdfVisibility)
        .RequireSession()
        .RequireAuthorization()
        .WithName("SetPdfVisibility");

        // Issue #5446: Accept copyright disclaimer for a PDF
        group.MapPost("/documents/{pdfId:guid}/accept-disclaimer", HandleAcceptCopyrightDisclaimer)
        .RequireSession()
        .RequireAuthorization()
        .WithName("AcceptCopyrightDisclaimer");

        // Issue #5446: Toggle RAG active flag for a PDF
        group.MapPatch("/documents/{pdfId:guid}/active", HandleSetActiveForRag)
        .RequireSession()
        .RequireAuthorization()
        .WithName("SetActiveForRag");

        // Issue #5447: Reclassify document (category, base document, version label)
        group.MapPatch("/documents/{pdfId:guid}/classify", HandleReclassifyDocument)
        .RequireSession()
        .RequireAuthorization()
        .WithName("ReclassifyDocument");
    }

    private static void MapAdminPdfListEndpoint(RouteGroupBuilder group)
    {
        // Admin: List all PDFs with processing status
        group.MapGet("/admin/pdfs", HandleGetAllPdfs)
            .RequireSession()
            .WithName("GetAllPdfs")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get all PDF documents (admin-only)";
                operation.Description = "Returns list of all PDFs with processing status, game association, and chunk counts.";
                return operation;
            });
    }

    private static async Task<IResult> HandleListPdfs(Guid gameId, IMediator mediator, CancellationToken ct)
    {
        var pdfs = await mediator.Send(new GetPdfDocumentsByGameQuery(gameId), ct).ConfigureAwait(false);
        return Results.Json(new { pdfs });
    }

    private static async Task<IResult> HandleGetPdfText(Guid pdfId, IMediator mediator, CancellationToken ct)
    {
        var pdf = await mediator.Send(new GetPdfTextQuery(pdfId), ct).ConfigureAwait(false);

        if (pdf == null)
        {
            return Results.NotFound(new { error = "PDF not found" });
        }

        return Results.Json(pdf);
    }

    private static async Task<IResult> HandleDownloadPdf(Guid pdfId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;
        bool isAdmin = string.Equals(session!.User!.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

        // DDD Migration Phase 4: Use DownloadPdfQuery via IMediator
        var query = new DownloadPdfQuery(
            PdfId: pdfId,
            UserId: userId,
            IsAdmin: isAdmin
        );

        PdfDownloadResult? result;
        try
        {
            result = await mediator.Send(query, ct).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Access denied for user {UserId} to download PDF {PdfId}", userId, pdfId);
            return Results.Forbid();
        }

        if (result == null)
        {
            logger.LogWarning("PDF {PdfId} not found for download", pdfId);
            return Results.NotFound(new { error = "PDF not found or file not in storage" });
        }

        logger.LogInformation("User {UserId} downloading PDF {PdfId}", userId, pdfId);

        return Results.Stream(
            result.FileStream,
            contentType: result.ContentType,
            fileDownloadName: result.FileName,
            enableRangeProcessing: true);
    }

    private static async Task<IResult> HandleDeletePdf(
        Guid pdfId,
        HttpContext context,
        AuditService auditService,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var pdf = await mediator.Send(new GetPdfOwnershipQuery(pdfId), ct).ConfigureAwait(false);

        if (pdf == null)
        {
            return Results.NotFound(new { error = "PDF not found" });
        }

        if (!CheckPdfAuthorization(session.User!, pdf))
        {
            await LogPdfAccessDeniedAsync(auditService, session.User!.Id.ToString(), "DELETE", pdfId.ToString(), session.User!.Role ?? "Unknown", pdf.UploadedByUserId, ct).ConfigureAwait(false);

            logger.LogWarning("User {UserId} with role {Role} denied access to delete PDF {PdfId} (owner: {OwnerId})",
                session.User!.Id, session.User!.Role, pdfId, pdf.UploadedByUserId);

            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = await mediator.Send(new DeletePdfCommand(pdfId.ToString()), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogError("Failed to delete PDF {PdfId}: {Error}", pdfId, result.Message);
            return Results.BadRequest(new { error = result.Message });
        }

        logger.LogInformation("User {UserId} deleted PDF {PdfId}", session.User!.Id, pdfId);

        await auditService.LogAsync(
            session.User!.Id.ToString(),
            "DELETE",
            "PdfDocument",
            pdfId.ToString(),
            "Success",
            $"PDF deleted successfully by user with role: {session.User!.Role}",
            null,
            null,
            ct).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleSetPdfVisibility(
        Guid pdfId,
        HttpContext context,
        [FromBody] SetPdfVisibilityRequest request,
        IMediator mediator,
        AuditService auditService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        var pdf = await mediator.Send(new GetPdfOwnershipQuery(pdfId), ct).ConfigureAwait(false);

        if (pdf == null)
        {
            return Results.NotFound(new { error = "PDF not found" });
        }

        if (!CheckPdfAuthorization(session.User!, pdf))
        {
            await LogPdfAccessDeniedAsync(auditService, userId.ToString(), "change visibility of", pdfId.ToString(), session.User!.Role ?? "Unknown", pdf.UploadedByUserId, ct).ConfigureAwait(false);

            logger.LogWarning("User {UserId} denied access to change visibility of PDF {PdfId} (owner: {OwnerId})",
                userId, pdfId, pdf.UploadedByUserId);

            return Results.Forbid();
        }

        var result = await mediator.Send(new SetPdfVisibilityCommand(pdfId, request.IsPublic), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("Failed to set visibility for PDF {PdfId}: {Error}", pdfId, result.Message);
            return Results.BadRequest(new { error = result.Message });
        }

        logger.LogInformation("User {UserId} set PDF {PdfId} visibility to {IsPublic}", userId, pdfId, request.IsPublic);

        await auditService.LogAsync(
            userId.ToString(),
            "UPDATE_VISIBILITY",
            "PdfDocument",
            pdfId.ToString(),
            "Success",
            $"PDF visibility changed to {(request.IsPublic ? "public" : "private")} by user with role: {session!.User!.Role}",
            null,
            null,
            ct).ConfigureAwait(false);

        return Results.Ok(new { success = true, message = result.Message, isPublic = request.IsPublic });
    }

    /// <summary>
    /// Issue #5446: Accept copyright disclaimer for a PDF document.
    /// </summary>
    private static async Task<IResult> HandleAcceptCopyrightDisclaimer(
        Guid pdfId,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
        var userId = session!.User!.Id;

        var result = await mediator.Send(new AcceptCopyrightDisclaimerCommand(userId, pdfId), ct).ConfigureAwait(false);

        return Results.Ok(new { success = result.Success, message = result.Message });
    }

    /// <summary>
    /// Issue #5446: Toggle RAG active flag for a PDF document.
    /// </summary>
    private static async Task<IResult> HandleSetActiveForRag(
        Guid pdfId,
        [FromBody] SetActiveForRagRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new SetActiveForRagCommand(pdfId, request.IsActive), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return Results.NotFound(new { error = result.Message });
        }

        return Results.Ok(new { success = true, message = result.Message, isActive = request.IsActive });
    }

    private static async Task<IResult> HandleReclassifyDocument(
        Guid pdfId,
        [FromBody] ReclassifyDocumentRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ReclassifyDocumentCommand(pdfId, request.Category, request.BaseDocumentId, request.VersionLabel), ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return Results.NotFound(new { error = result.Message });
        }

        return Results.Ok(new { success = true, message = result.Message, pdfId = result.PdfId });
    }

    private static async Task<IResult> HandleGetAllPdfs(
        HttpContext context,
        IMediator mediator,
        [FromQuery] string? status,
        [FromQuery] string? state,
        [FromQuery] long? minSizeBytes,
        [FromQuery] long? maxSizeBytes,
        [FromQuery] DateTime? uploadedAfter,
        [FromQuery] DateTime? uploadedBefore,
        [FromQuery] Guid? gameId,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder,
        [FromQuery] int pageSize = 50,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAllPdfsQuery(
            Status: status,
            State: state,
            MinSizeBytes: minSizeBytes,
            MaxSizeBytes: maxSizeBytes,
            UploadedAfter: uploadedAfter,
            UploadedBefore: uploadedBefore,
            GameId: gameId,
            SortBy: sortBy,
            SortOrder: sortOrder,
            PageSize: pageSize,
            Page: page
        );
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    /// <summary>
    /// E5-2: Override detected language of a PDF document.
    /// </summary>
    private static async Task<IResult> HandleOverridePdfLanguage(
        Guid pdfId,
        [FromBody] OverridePdfLanguageRequest request,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        await mediator.Send(new OverridePdfLanguageCommand(pdfId, request.LanguageCode), ct).ConfigureAwait(false);

        logger.LogInformation("PDF {PdfId} language override set to {LanguageCode}", pdfId, request.LanguageCode ?? "(cleared)");

        return Results.Ok(new { success = true, languageCode = request.LanguageCode });
    }

    // Helpers
    private static bool CheckPdfAuthorization(Api.BoundedContexts.Authentication.Application.DTOs.UserDto user, PdfOwnershipResult pdf)
    {
        bool isAdmin = string.Equals(user.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
        bool isOwner = pdf.UploadedByUserId == user.Id;
        return isAdmin || isOwner;
    }

    private static Task LogPdfAccessDeniedAsync(AuditService auditService, string userId, string action, string pdfId, string role, Guid ownerId, CancellationToken ct)
    {
        return auditService.LogAsync(
                userId,
                "ACCESS_DENIED",
                "PdfDocument",
                pdfId,
                "Denied",
                $"User attempted to {action} PDF owned by another user. User role: {role}, Owner: {ownerId}. RLS scope: own resources only.",
                null,
                null,
                ct);
    }
}
