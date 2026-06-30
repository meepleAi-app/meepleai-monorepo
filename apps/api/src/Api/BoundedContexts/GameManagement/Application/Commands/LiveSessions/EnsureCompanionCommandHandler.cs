using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Handles <see cref="EnsureCompanionCommand"/>: lazily creates a <c>SessionTracking.Session</c>
/// companion for live sessions that pre-date SP0 (ADR-083), wiring <c>TrackingSessionId</c> so
/// that the SSE forwarder can broadcast domain events to the companion channel.
///
/// <para><strong>Guard semantics (no-op early exits):</strong></para>
/// <list type="bullet">
///   <item><c>TrackingSessionId != null</c> → already has a companion; return immediately.</item>
///   <item><c>GameId == null</c> → free-form session; cannot have a <c>GameSpecific</c> companion; return.</item>
/// </list>
///
/// <para><strong>Happy path:</strong></para>
/// <list type="number">
///   <item>Fetch session via <see cref="ILiveSessionRepository.GetByIdAsync"/>.</item>
///   <item>Call <see cref="ICompanionSessionService.CreateCompanionAsync"/> (add-only, no SaveChanges).</item>
///   <item><see cref="Domain.Entities.LiveGameSession.SetTrackingSessionId"/> → set the cross-BC bridge.</item>
///   <item><see cref="ILiveSessionRepository.UpdateAsync"/> → stage the change.</item>
///   <item><see cref="IUnitOfWork.SaveChangesAsync"/> → single atomic commit (ADR-060).</item>
/// </list>
///
/// <para><strong>Race safety (xmin optimistic concurrency, ADR-060):</strong></para>
/// On <see cref="DbUpdateConcurrencyException"/>: re-fetch the session. If <c>TrackingSessionId</c>
/// is now non-null another concurrent caller already won the race → return successfully (idempotent).
/// Otherwise rethrow so the caller can decide.
/// The losing caller's <c>CreateCompanionAsync</c> companion is discarded (its <c>AddAsync</c>
/// was not committed — rollback is natural).
///
/// Issue #2600 SP5-c Task 1.
/// </summary>
internal sealed class EnsureCompanionCommandHandler : ICommandHandler<EnsureCompanionCommand>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ICompanionSessionService _companionSessionService;
    private readonly IUnitOfWork _unitOfWork;

    public EnsureCompanionCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        ICompanionSessionService companionSessionService,
        IUnitOfWork unitOfWork)
    {
        _liveSessionRepository = liveSessionRepository
            ?? throw new ArgumentNullException(nameof(liveSessionRepository));
        _companionSessionService = companionSessionService
            ?? throw new ArgumentNullException(nameof(companionSessionService));
        _unitOfWork = unitOfWork
            ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(EnsureCompanionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _liveSessionRepository
            .GetByIdAsync(command.LiveSessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.LiveSessionId.ToString());

        // Guard: already has a companion OR free-form (no GameId) → no-op.
        if (session.TrackingSessionId != null || session.GameId == null)
            return;

        // Create the companion (add-only — no SaveChanges inside).
        var companionId = await _companionSessionService
            .CreateCompanionAsync(session.CreatedByUserId, session.GameId.Value, cancellationToken)
            .ConfigureAwait(false);

        session.SetTrackingSessionId(companionId);

        await _liveSessionRepository
            .UpdateAsync(session, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Race: another concurrent subscriber already created the companion.
            // Re-fetch to check.
            var refreshed = await _liveSessionRepository
                .GetByIdAsync(command.LiveSessionId, cancellationToken)
                .ConfigureAwait(false);

            if (refreshed?.TrackingSessionId != null)
            {
                // Another caller won the race and committed successfully.
                // The companion we added (via CreateCompanionAsync) was never committed
                // (SaveChanges failed → EF rolled back the transaction) — no orphan.
                // Return idempotently.
                return;
            }

            // Genuine concurrency conflict with something else → rethrow for the caller.
            throw;
        }
    }
}
