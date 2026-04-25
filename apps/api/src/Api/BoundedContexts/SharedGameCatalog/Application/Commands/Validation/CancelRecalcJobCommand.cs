using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Requests cancellation of an in-flight or queued
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicRecalcJob"/>
/// (ADR-051 M2.1, Sprint 2 / Task 9).
/// </summary>
/// <remarks>
/// <para>
/// Cancellation is modelled as a <i>flag</i>, not a status — see the aggregate state-machine
/// comment on
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicRecalcJob.RequestCancellation"/>.
/// The handler sets <c>CancellationRequested = true</c>; the
/// <see cref="Api.Infrastructure.BackgroundServices.MechanicRecalcBackgroundService"/> worker
/// short-circuits its processing loop on the next iteration and transitions the job to
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Enums.RecalcJobStatus.Completed"/>
/// (with <c>CancellationRequested == true</c> preserved as audit evidence).
/// </para>
/// <para>
/// Idempotent: calling cancel on an already-cancelled job is a no-op. Calling cancel on a job
/// in a terminal status (Completed/Failed) raises
/// <see cref="Api.Middleware.Exceptions.ConflictException"/> (HTTP 409).
/// </para>
/// </remarks>
internal sealed record CancelRecalcJobCommand(Guid JobId) : ICommand<Unit>;
