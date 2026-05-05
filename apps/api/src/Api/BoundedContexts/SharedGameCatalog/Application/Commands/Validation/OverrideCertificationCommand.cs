using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Admin escalation command to certify a <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicAnalysis"/>
/// whose computed metrics did NOT meet the configured thresholds (ADR-051 Sprint 1 / Task 24).
/// </summary>
/// <remarks>
/// <para>
/// The override path is a deliberate admin escalation: the operator accepts accountability by
/// supplying a 20..500-char justification that is persisted on the aggregate and surfaced in the
/// audit trail / downstream <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Events.MechanicAnalysisCertifiedEvent"/>
/// with <c>WasOverride=true</c>.
/// </para>
/// <para>
/// The analysis must already carry prior metrics (<c>LastMetricsId</c> non-null) — overriding with
/// no metrics baseline is meaningless. Re-overriding an already <c>Certified</c> analysis is
/// rejected as a conflict. Both pre-checks and the aggregate's own guards enforce this.
/// </para>
/// <para>
/// <see cref="UserId"/> is threaded explicitly through the command because this bounded context
/// does not inject an <c>ICurrentUserService</c>; sibling commands (e.g. <c>ImportGameFromBggCommand</c>)
/// use the same pattern. The endpoint layer resolves the admin principal and passes it in.
/// </para>
/// </remarks>
/// <param name="MechanicAnalysisId">Target analysis aggregate. <see cref="Guid.Empty"/> is rejected by the validator.</param>
/// <param name="Reason">Justification persisted as <c>CertificationOverrideReason</c>. Must be 20..500 chars after trimming.</param>
/// <param name="UserId">Admin user performing the override; persisted as <c>CertifiedByUserId</c> and surfaced on the certified event.</param>
internal sealed record OverrideCertificationCommand(
    Guid MechanicAnalysisId,
    string Reason,
    Guid UserId) : ICommand<Unit>;
