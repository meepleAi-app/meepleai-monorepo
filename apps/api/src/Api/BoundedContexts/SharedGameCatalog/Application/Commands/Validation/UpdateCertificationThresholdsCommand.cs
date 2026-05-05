using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Admin command to update the operator-configurable certification thresholds singleton
/// (ADR-051 Sprint 1 / Task 26).
/// </summary>
/// <remarks>
/// <para>
/// Mutates the <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.CertificationThresholdsConfig"/>
/// aggregate by replacing its <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects.CertificationThresholds"/>
/// value object. The handler does NOT trigger a mass recertification of existing analyses — operators
/// invoke that pipeline separately so threshold edits and recalc are auditable independently.
/// </para>
/// <para>
/// Validator surface invariants enforce the same numeric ranges as the value-object factory
/// (<c>CertificationThresholds.Create</c>): percentages in 0..100 and tolerance &gt;= 0. The factory
/// remains the source of truth and re-validates as defense-in-depth.
/// </para>
/// <para>
/// <see cref="UserId"/> is threaded explicitly through the command because this bounded context
/// does not inject an <c>ICurrentUserService</c>; sibling commands (e.g. <c>OverrideCertificationCommand</c>)
/// follow the same pattern. The endpoint layer resolves the admin principal and passes it in.
/// </para>
/// </remarks>
/// <param name="MinCoveragePct">Minimum mechanic-coverage percentage to certify. 0..100 inclusive.</param>
/// <param name="MaxPageTolerance">Maximum page-citation drift tolerated per claim. Must be &gt;= 0.</param>
/// <param name="MinBggMatchPct">Minimum BGG-match percentage to certify. 0..100 inclusive.</param>
/// <param name="MinOverallScore">Minimum aggregate overall score to certify. 0..100 inclusive.</param>
/// <param name="UserId">Admin user performing the update; persisted as <c>UpdatedByUserId</c> on the singleton.</param>
internal sealed record UpdateCertificationThresholdsCommand(
    decimal MinCoveragePct,
    int MaxPageTolerance,
    decimal MinBggMatchPct,
    decimal MinOverallScore,
    Guid UserId) : ICommand<Unit>;
