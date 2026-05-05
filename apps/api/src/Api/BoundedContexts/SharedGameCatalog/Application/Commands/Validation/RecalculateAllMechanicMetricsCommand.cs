using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Command to mass-recalculate AI comprehension validation metrics for every
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicAnalysis"/>
/// currently in the terminal post-approval state (ADR-051 Sprint 1 / Task 25).
/// </summary>
/// <remarks>
/// <para>
/// Sprint 1 ships this as a synchronous loop: the handler enumerates every
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Enums.MechanicAnalysisStatus.Published"/>
/// analysis (suppressed rows included — suppression is orthogonal) and dispatches one
/// <see cref="CalculateMechanicAnalysisMetricsCommand"/> per id through <see cref="MediatR.IMediator"/>.
/// Each per-id dispatch owns its own unit-of-work boundary, so partial progress is durable
/// even if a later id fails. Sprint 2 will move this to a Hangfire recurring job.
/// </para>
/// <para>
/// The command intentionally has no properties: it is a sentinel/dispatcher trigger. A validator
/// is still registered (empty rule set) for architectural consistency with the rest of the
/// command pipeline.
/// </para>
/// <para>
/// Returns the number of analyses for which metrics were <i>successfully</i> recomputed. Per-id
/// <see cref="Api.Middleware.Exceptions.NotFoundException"/> and
/// <see cref="Api.Middleware.Exceptions.ConflictException"/> failures (e.g. row deleted between
/// enumeration and dispatch, or transitioned out of <c>Published</c>) are logged and skipped — they
/// do not abort the batch. Unexpected exceptions propagate.
/// </para>
/// </remarks>
internal sealed record RecalculateAllMechanicMetricsCommand() : ICommand<int>;
