using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Lists the currently-active impersonation sessions for the superadmin kill-switch dashboard
/// (SP5 Admin Security S2 — T6 / D-S2-5). Optionally filtered to a single acting admin.
///
/// "Active" = <c>ImpersonatedByUserId IS NOT NULL AND RevokedAt IS NULL AND ImpersonatedUntil &gt; now</c>.
/// Backed by the partial index <c>ix_user_sessions_impersonated_by_user_id</c> (T1).
/// </summary>
internal record GetActiveImpersonationsQuery(
    Guid? FilterByAdminUserId = null
) : IQuery<IReadOnlyList<ImpersonationStatusDto>>;

/// <summary>Read model for one active impersonation session.</summary>
public record ImpersonationStatusDto(
    Guid SessionId,
    Guid AdminUserId,
    string AdminEmail,
    Guid TargetUserId,
    string TargetEmail,
    DateTime StartedAt,
    DateTime ImpersonatedUntil
);
