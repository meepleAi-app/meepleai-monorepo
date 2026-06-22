using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Scoring;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Asse A semantic alignment #1896 (T10, DEC-1): handler for polymorphic mid-game
/// score update. Loads the session, delegates to <c>Session.SetScores</c> (T9 — raises
/// <see cref="Api.BoundedContexts.SessionTracking.Domain.Events.SessionScoresUpdatedEvent"/>),
/// persists via UoW, then computes the winner (when single) via the matching strategy.
///
/// <para>JSON schema validation already enforced upstream by
/// <see cref="UpdateSessionScoresCommandValidator"/> in the MediatR FluentValidation
/// behavior pipeline — this handler trusts the payload's structural validity.</para>
/// </summary>
public class UpdateSessionScoresCommandHandler
    : IRequestHandler<UpdateSessionScoresCommand, UpdateSessionScoresResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ScoringStrategyFactory _factory;

    public UpdateSessionScoresCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ScoringStrategyFactory factory)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<UpdateSessionScoresResult> Handle(
        UpdateSessionScoresCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        // IDOR guard (security review high-priority): only the session owner or a registered
        // participant (User-linked) can mutate polymorphic scores. Mirrors the pattern used by
        // ExportSessionHandlers + AdvanceTurnCommandHandler.
        if (session.UserId != request.RequestedBy &&
            !session.Participants.Any(p => p.UserId == request.RequestedBy))
        {
            throw new ForbiddenException(
                $"User {request.RequestedBy} is not authorized to update scores for session {request.SessionId}.");
        }

        // Domain method enforces non-empty ScoreData + raises SessionScoresUpdatedEvent.
        // Validation of JSON schema vs ScoringType has already been performed by
        // UpdateSessionScoresCommandValidator in the FluentValidation pipeline.
        session.SetScores(request.ScoringType, request.ScoreData);

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Compute winner via the matching strategy (null when undetermined: ties /
        // cooperative all-win or all-lose / empty payload — strategy-specific).
        var strategy = _factory.GetStrategy(request.ScoringType);
        var winnerId = strategy.ComputeWinnerPlayerId(request.ScoreData);

        return new UpdateSessionScoresResult(session.Id, request.ScoringType, winnerId);
    }
}
