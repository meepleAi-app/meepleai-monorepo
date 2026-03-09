using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Session tier limits configuration endpoints (Admin only) and user quota endpoints.
/// Issue #3070: Session limits backend implementation.
/// </summary>
internal static class SessionLimitsConfigEndpoints
{
    public static RouteGroupBuilder MapSessionLimitsConfigEndpoints(this RouteGroupBuilder group)
    {
        // Admin endpoints for session limits configuration
        group.MapGet("/admin/system/session-limits", HandleGetSessionLimits)
            .WithName("GetSessionLimits")
            .WithTags("Admin", "SessionLimits", "Configuration")
            .WithSummary("Get current session tier limits")
            .WithDescription("Retrieves the maximum active session counts for Free, Normal, and Premium tiers")
            .Produces<SessionLimitsDto>();

        group.MapPut("/admin/system/session-limits", HandleUpdateSessionLimits)
            .WithName("UpdateSessionLimits")
            .WithTags("Admin", "SessionLimits", "Configuration")
            .WithSummary("Update session tier limits")
            .WithDescription("Updates the maximum active session counts for all tiers. Requires admin role.")
            .Produces<SessionLimitsDto>()
            .ProducesValidationProblem();

        // User endpoint for checking their own session quota
        group.MapGet("/users/{id:guid}/session-quota", HandleGetUserSessionQuota)
            .WithName("GetUserSessionQuota")
            .WithTags("Users", "SessionLimits")
            .WithSummary("Get user's session quota status")
            .WithDescription("Retrieves the current session quota for a user, including active sessions and limits")
            .Produces<SessionQuotaDto>()
            .Produces(404);

        return group;
    }

    private static async Task<IResult> HandleGetSessionLimits(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        // Admin authorization check
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetSessionLimitsQuery();
        var limits = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(limits);
    }

    private static async Task<IResult> HandleUpdateSessionLimits(
        UpdateSessionLimitsRequest request,
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
            "Admin {UserId} updating session limits: Free={Free}, Normal={Normal}, Premium={Premium}",
            userId,
            request.FreeTierLimit,
            request.NormalTierLimit,
            request.PremiumTierLimit);

        var command = new UpdateSessionLimitsCommand(
            FreeTierLimit: request.FreeTierLimit,
            NormalTierLimit: request.NormalTierLimit,
            PremiumTierLimit: request.PremiumTierLimit,
            UpdatedByUserId: userId
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Admin {UserId} successfully updated session limits",
            userId);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetUserSessionQuota(
        Guid id,
        HttpContext context,
        ISessionQuotaService quotaService,
        IMediator mediator,
        CancellationToken ct)
    {
        // Session required - user can check their own quota, admin can check any user's quota
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        var requestingUserId = session!.User!.Id;
        var requestingRole = Role.Parse(session.User!.Role);

        // Users can only check their own quota, admins can check any user
        if (id != requestingUserId && !requestingRole.IsAdmin())
        {
            return Results.Forbid();
        }

        // For checking another user's quota, we need their tier
        // For now, use the requesting user's info if checking own quota
        // If admin checking another user, we'd need to fetch that user's info
        // This is a simplified implementation - admin checking others would require user lookup
        if (id != requestingUserId)
        {
            // Admin checking another user - return basic info only
            // Full implementation would require user repository lookup
            return Results.NotFound("User quota lookup for other users not yet implemented");
        }

        var userTier = UserTier.Parse(session.User!.Tier);

        var quotaInfo = await quotaService.GetQuotaInfoAsync(
            id,
            userTier,
            requestingRole,
            ct).ConfigureAwait(false);

        var dto = new SessionQuotaDto(
            CurrentSessions: quotaInfo.ActiveSessions,
            MaxSessions: quotaInfo.MaxSessions,
            RemainingSlots: quotaInfo.RemainingSlots,
            CanCreateNew: quotaInfo.IsUnlimited || quotaInfo.RemainingSlots > 0,
            IsUnlimited: quotaInfo.IsUnlimited,
            UserTier: userTier.Value
        );

        return Results.Ok(dto);
    }
}
