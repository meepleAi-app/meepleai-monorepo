using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Extensions;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - contains request DTOs
namespace Api.Routing.Admin;

/// <summary>
/// Consolidated impersonation API for SP5 Admin Security S2 (T6). Replaces the scattered legacy
/// endpoints (issues #2890 / #3349) on AdminUserActivityEndpoints / AdminOperationsEndpoints.
///
/// All endpoints use IMediator.Send (CQRS — CLAUDE.md) and are superadmin-gated. The legacy
/// routes are kept as 308 Permanent Redirects for one release cycle (removal tracked as a
/// follow-up issue).
/// </summary>
internal static class AdminImpersonationEndpoints
{
    public static RouteGroupBuilder MapAdminImpersonationEndpoints(this RouteGroupBuilder group)
    {
        var impersonationGroup = group.MapGroup("/admin/impersonation")
            .WithTags("Admin", "Impersonation");

        // START — superadmin begins impersonating a non-privileged user.
        impersonationGroup.MapPost("/start", async (
            HttpContext context,
            IMediator mediator,
            ImpersonationStartRequest request,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var command = new ImpersonationStartCommand(
                TargetUserId: request.TargetUserId,
                RequestingUserId: session!.Principal!.EffectiveActor.Id,
                Reason: request.Reason,
                DurationMinutes: request.DurationMinutes ?? 15);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/admin/impersonation/active", result);
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithSummary("Start an impersonation session (superadmin only)")
        .Produces<ImpersonationStartResponseDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // END — the acting admin ends their own impersonation session.
        impersonationGroup.MapPost("/end", async (
            HttpContext context,
            IMediator mediator,
            ImpersonationEndRequest request,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var command = new ImpersonationEndCommand(
                SessionId: request.SessionId,
                RequestingUserId: session!.Principal!.EffectiveActor.Id);

            var success = await mediator.Send(command, ct).ConfigureAwait(false);
            return success
                ? Results.Ok(new { success = true, message = "Impersonation ended" })
                : Results.BadRequest(new { success = false, message = "Failed to end impersonation" });
        })
        .RequireAuthorization("RequireAdmin")
        .WithSummary("End an active impersonation session (admin)")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        // REVOKE — superadmin kill-switch for another admin's impersonation session.
        impersonationGroup.MapPost("/revoke", async (
            HttpContext context,
            IMediator mediator,
            ImpersonationRevokeRequest request,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var command = new RevokeImpersonationCommand(
                SessionId: request.SessionId,
                RequestingUserId: session!.Principal!.EffectiveActor.Id);

            var success = await mediator.Send(command, ct).ConfigureAwait(false);
            return success
                ? Results.Ok(new { success = true, message = "Impersonation revoked" })
                : Results.BadRequest(new { success = false, message = "Failed to revoke impersonation" });
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithSummary("Revoke another admin's impersonation session (superadmin kill-switch)")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict);

        // ACTIVE — superadmin lists all active impersonation sessions (oversight dashboard).
        impersonationGroup.MapGet("/active", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetActiveImpersonationsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithSummary("List all active impersonation sessions (superadmin)")
        .Produces<IReadOnlyList<ImpersonationStatusDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // ACTIVE BY ADMIN — superadmin filters active impersonations to a single acting admin.
        impersonationGroup.MapGet("/active/{adminUserId:guid}", async (
            Guid adminUserId,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetActiveImpersonationsQuery(adminUserId), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithSummary("List active impersonation sessions for a specific admin (superadmin)")
        .Produces<IReadOnlyList<ImpersonationStatusDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        return group;
    }
}

internal record ImpersonationStartRequest(Guid TargetUserId, string Reason, int? DurationMinutes = null);
internal record ImpersonationEndRequest(Guid SessionId);
internal record ImpersonationRevokeRequest(Guid SessionId);
