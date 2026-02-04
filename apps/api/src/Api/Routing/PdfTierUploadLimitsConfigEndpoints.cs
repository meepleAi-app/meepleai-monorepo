using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// PDF upload tier-based limits configuration endpoints (Admin only).
/// Issue #3333: PDF Upload Limits Configuration UI
/// </summary>
internal static class PdfTierUploadLimitsConfigEndpoints
{
    public static RouteGroupBuilder MapPdfTierUploadLimitsConfigEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/admin/config/pdf-tier-upload-limits", HandleGetPdfTierUploadLimits)
            .WithName("GetPdfTierUploadLimits")
            .WithTags("Admin", "PdfUpload", "Configuration")
            .WithSummary("Get current PDF upload tier limits")
            .WithDescription("Retrieves the daily and weekly upload limits for Free, Normal, and Premium tiers")
            .Produces<PdfTierUploadLimitsDto>();

        group.MapPut("/admin/config/pdf-tier-upload-limits", HandleUpdatePdfTierUploadLimits)
            .WithName("UpdatePdfTierUploadLimits")
            .WithTags("Admin", "PdfUpload", "Configuration")
            .WithSummary("Update PDF upload tier limits")
            .WithDescription("Updates the daily and weekly upload limits for all tiers. Requires admin role.")
            .Produces<PdfTierUploadLimitsDto>()
            .ProducesValidationProblem();

        return group;
    }

    private static async Task<IResult> HandleGetPdfTierUploadLimits(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        // Admin authorization check
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetPdfTierUploadLimitsQuery();
        var limits = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(limits);
    }

    private static async Task<IResult> HandleUpdatePdfTierUploadLimits(
        UpdatePdfTierUploadLimitsRequest request,
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
            "Admin {UserId} updating PDF tier upload limits: Free={FreeDL}/{FreeWL}, Normal={NormalDL}/{NormalWL}, Premium={PremiumDL}/{PremiumWL}",
            userId,
            request.FreeDailyLimit,
            request.FreeWeeklyLimit,
            request.NormalDailyLimit,
            request.NormalWeeklyLimit,
            request.PremiumDailyLimit,
            request.PremiumWeeklyLimit);

        var command = new UpdatePdfTierUploadLimitsCommand(
            FreeDailyLimit: request.FreeDailyLimit,
            FreeWeeklyLimit: request.FreeWeeklyLimit,
            NormalDailyLimit: request.NormalDailyLimit,
            NormalWeeklyLimit: request.NormalWeeklyLimit,
            PremiumDailyLimit: request.PremiumDailyLimit,
            PremiumWeeklyLimit: request.PremiumWeeklyLimit,
            UpdatedByUserId: userId
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Admin {UserId} successfully updated PDF tier upload limits",
            userId);

        return Results.Ok(result);
    }
}
