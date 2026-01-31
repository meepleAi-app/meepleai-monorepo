using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// PDF upload limits configuration endpoints (Admin only).
/// Issue #3072: PDF Upload Limits - Admin API
/// </summary>
internal static class PdfUploadLimitsConfigEndpoints
{
    public static RouteGroupBuilder MapPdfUploadLimitsConfigEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/admin/system/pdf-upload-limits", HandleGetPdfUploadLimits)
            .WithName("GetPdfUploadLimits")
            .WithTags("Admin", "PdfUpload", "Configuration")
            .WithSummary("Get current PDF upload limits")
            .WithDescription("Retrieves the maximum file size, pages per document, documents per game, and allowed MIME types")
            .Produces<PdfUploadLimitsDto>();

        group.MapPut("/admin/system/pdf-upload-limits", HandleUpdatePdfUploadLimits)
            .WithName("UpdatePdfUploadLimits")
            .WithTags("Admin", "PdfUpload", "Configuration")
            .WithSummary("Update PDF upload limits")
            .WithDescription("Updates the PDF upload configuration. Requires admin role.")
            .Produces<PdfUploadLimitsDto>()
            .ProducesValidationProblem();

        return group;
    }

    private static async Task<IResult> HandleGetPdfUploadLimits(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        // Admin authorization check
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetPdfUploadLimitsQuery();
        var limits = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(limits);
    }

    private static async Task<IResult> HandleUpdatePdfUploadLimits(
        UpdatePdfUploadLimitsRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Admin authorization check with user ID extraction
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;

        logger.LogInformation(
            "Admin {UserId} updating PDF upload limits: MaxFileSize={MaxFileSize}, MaxPages={MaxPages}, MaxDocs={MaxDocs}, MimeTypes={MimeTypes}",
            userId,
            request.MaxFileSizeBytes,
            request.MaxPagesPerDocument,
            request.MaxDocumentsPerGame,
            string.Join(",", request.AllowedMimeTypes));

        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: request.MaxFileSizeBytes,
            MaxPagesPerDocument: request.MaxPagesPerDocument,
            MaxDocumentsPerGame: request.MaxDocumentsPerGame,
            AllowedMimeTypes: request.AllowedMimeTypes,
            UpdatedByUserId: userId
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Admin {UserId} successfully updated PDF upload limits",
            userId);

        return Results.Ok(result);
    }
}
