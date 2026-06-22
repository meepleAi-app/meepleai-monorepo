using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing.Admin;

/// <summary>
/// Admin 2FA compliance endpoints for the SP5 Admin Security S3 strict-2FA cutover (T6, D-S3-5).
///
/// Provides the pre-flip sweep: list every privileged account (Admin/SuperAdmin) that has NOT
/// enrolled in 2FA, so ops can drive enrollment to 100% before flipping <c>TwoFactorStrictMode</c>
/// in an environment (otherwise the flip would lock those admins out — the mass-lockout risk
/// neutralized by the default=OFF cutover strategy, D-S3-1).
///
/// Superadmin-gated and IMediator-only (CQRS — CLAUDE.md), mirroring the S2 impersonation endpoints.
/// </summary>
internal static class AdminTwoFactorComplianceEndpoints
{
    public static RouteGroupBuilder MapAdminTwoFactorComplianceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/admin/users/no-2fa", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetAdminsWithoutTwoFactorQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithName("GetAdminsWithoutTwoFactor")
        .WithTags("Admin", "TwoFactor")
        .WithSummary("List admin/superadmin accounts without 2FA enrolled (compliance sweep, superadmin only)")
        .Produces<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        return group;
    }
}
