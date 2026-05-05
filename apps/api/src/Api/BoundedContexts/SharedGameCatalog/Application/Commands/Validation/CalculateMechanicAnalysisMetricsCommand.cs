using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Command to compute AI comprehension validation metrics for a <see cref="Guid"/>-identified
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicAnalysis"/>
/// (ADR-051 Sprint 1 / Task 23).
/// </summary>
/// <remarks>
/// <para>
/// The analysis must be in <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Enums.MechanicAnalysisStatus.Published"/>
/// — metrics are only meaningful once every claim has been admin-approved and the analysis
/// transitioned to the terminal post-approval state. Any other status (<c>Draft</c>, <c>InReview</c>,
/// <c>Rejected</c>) yields a <c>ConflictException</c>.
/// </para>
/// <para>
/// Returns the <see cref="Guid"/> primary key of the newly persisted
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicAnalysisMetrics"/>
/// snapshot so callers (e.g. endpoint handlers, background jobs) can link to the audit trail.
/// </para>
/// </remarks>
/// <param name="MechanicAnalysisId">The analysis aggregate whose claims are scored against the
/// golden set. Required — <see cref="Guid.Empty"/> is rejected by the validator.</param>
internal sealed record CalculateMechanicAnalysisMetricsCommand(
    Guid MechanicAnalysisId) : ICommand<Guid>;
