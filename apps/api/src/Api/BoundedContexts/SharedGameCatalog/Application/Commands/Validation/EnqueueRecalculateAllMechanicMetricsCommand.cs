using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Command to <i>enqueue</i> an asynchronous mass recalculation of AI comprehension validation
/// metrics for every Published <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicAnalysis"/>
/// (ADR-051 M2.1, Sprint 2 / Task 9).
/// </summary>
/// <remarks>
/// <para>
/// This is the Sprint 2 async counterpart to the Sprint 1 synchronous
/// <see cref="RecalculateAllMechanicMetricsCommand"/> dispatcher. Instead of looping inline, the
/// handler creates a <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicRecalcJob"/>
/// in <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Enums.RecalcJobStatus.Pending"/> status
/// and persists it. The <see cref="Api.Infrastructure.BackgroundServices.MechanicRecalcBackgroundService"/>
/// worker (Task 8) claims the job atomically via <c>SELECT ... FOR UPDATE SKIP LOCKED</c> and
/// drives it to completion out-of-band.
/// </para>
/// <para>
/// Returns the <see cref="Guid"/> of the newly created job so the caller (admin endpoint, Task 10)
/// can return <c>202 Accepted</c> with a <c>Location</c> header pointing to the status endpoint
/// (<c>GetRecalcJobStatusQuery</c>, Task 9 Step 2).
/// </para>
/// <para>
/// <see cref="TriggeredByUserId"/> identifies the admin who requested the recalc and is persisted
/// on the job for audit purposes. The validator enforces non-empty; the aggregate's
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicRecalcJob.Enqueue"/>
/// factory adds defense-in-depth.
/// </para>
/// </remarks>
internal sealed record EnqueueRecalculateAllMechanicMetricsCommand(Guid TriggeredByUserId) : ICommand<Guid>;
