using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin configuration endpoints for system-wide settings.
/// Issue #3673: PDF Upload Limits Admin UI
/// </summary>
internal static class AdminConfigEndpoints
{
    public static RouteGroupBuilder MapAdminConfigEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/v1/admin/config/pdf-limits
        group.MapGet("/admin/config/pdf-limits", HandleGetPdfLimits)
            .WithName("GetPdfLimits")
            .WithTags("Admin", "Configuration")
            .WithSummary("Get all PDF tier upload limits")
            .WithDescription("Retrieves upload limits for Free, Normal, and Premium tiers")
            .Produces<IReadOnlyList<PdfLimitConfigDto>>();

        // PUT /api/v1/admin/config/pdf-limits/{tier}
        group.MapPut("/admin/config/pdf-limits/{tier}", HandleUpdatePdfLimits)
            .WithName("UpdatePdfLimits")
            .WithTags("Admin", "Configuration")
            .WithSummary("Update PDF limits for a specific tier")
            .WithDescription("Updates MaxPerDay, MaxPerWeek, and MaxPerGame for a single tier. Requires admin role.")
            .Produces<PdfLimitConfigDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem();

        return group;
    }

    /// <summary>
    /// Get PDF upload limits for all tiers.
    /// </summary>
    private static async Task<IResult> HandleGetPdfLimits(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        // Admin authorization check
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAllPdfLimitsQuery();
        var limits = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(limits);
    }

    /// <summary>
    /// Update PDF upload limits for a specific tier.
    /// </summary>
    private static async Task<IResult> HandleUpdatePdfLimits(
        string tier,
        UpdatePdfLimitsRequest request,
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
            "Admin {UserId} updating PDF limits for tier {Tier}: MaxPerDay={MaxPerDay}, MaxPerWeek={MaxPerWeek}, MaxPerGame={MaxPerGame}",
            userId, tier, request.MaxPerDay, request.MaxPerWeek, request.MaxPerGame);

        var command = new UpdatePdfLimitsCommand
        {
            Tier = tier,
            MaxPerDay = request.MaxPerDay,
            MaxPerWeek = request.MaxPerWeek,
            MaxPerGame = request.MaxPerGame,
            AdminUserId = userId
        };

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Admin {UserId} successfully updated PDF limits for tier {Tier}",
            userId, tier);

        return Results.Ok(result);
    }

    /// <summary>
    /// Request DTO for updating PDF limits.
    /// </summary>
    private sealed record UpdatePdfLimitsRequest(
        int MaxPerDay,
        int MaxPerWeek,
        int MaxPerGame
    );
}
