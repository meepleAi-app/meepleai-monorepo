using Api.BoundedContexts.Authentication.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace Api.BoundedContexts.KbQuality.Application.Authentication;

/// <summary>
/// Plan amendment A2: extracts current admin identity from the request's <see cref="HttpContext"/>.
///
/// <para>The project does NOT use a generic <c>ICurrentUserService</c> abstraction; the established
/// pattern (see <c>AuditLoggingBehavior</c>, <c>TwoFactorEnforcementBehavior</c>) is to read
/// <c>SessionStatusDto</c> from <c>HttpContext.Items[nameof(SessionStatusDto)]</c> — populated by the
/// auth middleware — and use <c>Principal.EffectiveActor</c> for authorization/attribution checks.
/// This helper centralizes that lookup for the KbQuality bounded context so behaviors and handlers
/// extract identity consistently.</para>
///
/// <para>Falls back gracefully when no auth context is available (returns
/// <c>(Guid.Empty, Guid.Empty, false)</c>). Callers can detect anonymity via the
/// <c>UserId == Guid.Empty</c> check.</para>
///
/// <para>Tenant semantics: the project is single-tenant-per-user at the time of writing — there is no
/// dedicated <c>tenant_id</c> column on the <c>UserDto</c>/session DTO yet. We therefore fall back to
/// <c>UserId</c> as the budget partition key. If a multi-tenant claim is added later, prefer it over
/// the userId fallback here.</para>
/// </summary>
internal static class KbQualityCurrentUser
{
    /// <summary>
    /// Returns <c>(UserId, TenantId, IsAdmin)</c> for the current request. <c>TenantId</c>
    /// degrades to <c>UserId</c> until a multi-tenant claim exists. <c>IsAdmin</c> is true for
    /// users whose <see cref="UserDto.Role"/> equals <c>"admin"</c> or <c>"superadmin"</c>
    /// (case-insensitive match for safety, even though seed data normalises to lowercase).
    /// </summary>
    public static (Guid UserId, Guid TenantId, bool IsAdmin) FromHttpContext(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return (Guid.Empty, Guid.Empty, false);
        }

        if (!httpContext.Items.TryGetValue(nameof(SessionStatusDto), out var value)
            || value is not SessionStatusDto { IsValid: true, Principal: not null } session)
        {
            return (Guid.Empty, Guid.Empty, false);
        }

        // Authorization checks (admin/superadmin gating) read Principal.EffectiveActor — the
        // acting admin during impersonation, else the subject. Mirrors TwoFactorEnforcementBehavior.
        var actor = session.Principal!.EffectiveActor;
        var subject = session.Principal!.Subject;

        // Budget partition keys on the SUBJECT (the user whose quota is being spent), even
        // when an admin is impersonating: an admin override is captured via the IsAdmin flag,
        // but the spend hits the impersonated user's tenant counter. Mirrors the impersonation
        // ownership contract documented on Principal.
        var userId = subject.Id;
        var tenantId = subject.Id; // single-tenant fallback (see XML doc)

        var role = actor.Role ?? string.Empty;
        var isAdmin = role.Equals("admin", StringComparison.OrdinalIgnoreCase)
                   || role.Equals("superadmin", StringComparison.OrdinalIgnoreCase);

        return (userId, tenantId, isAdmin);
    }
}
