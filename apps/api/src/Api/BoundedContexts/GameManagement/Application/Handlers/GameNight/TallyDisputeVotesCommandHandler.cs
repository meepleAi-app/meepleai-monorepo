using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.GameNight;

/// <summary>
/// Handles <see cref="TallyDisputeVotesCommand"/>.
/// 1. Loads the dispute
/// 2. Tallies votes to determine final outcome
/// 3. Sets override rule if verdict was overridden
/// 4. Appends legacy entry to session for backward compatibility
/// 5. Persists the updated dispute and session
/// </summary>
internal sealed class TallyDisputeVotesCommandHandler
    : ICommandHandler<TallyDisputeVotesCommand>
{
    private readonly IRuleDisputeRepository _disputeRepository;
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TallyDisputeVotesCommandHandler(
        IRuleDisputeRepository disputeRepository,
        ILiveSessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _disputeRepository = disputeRepository ?? throw new ArgumentNullException(nameof(disputeRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(
        TallyDisputeVotesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Get dispute
        var dispute = await _disputeRepository
            .GetByIdAsync(command.DisputeId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("RuleDispute", command.DisputeId.ToString());

        // 2. Tally votes — sets FinalOutcome
        dispute.TallyVotes();

        // 3. If overridden and override rule provided, set it BEFORE raising event
        if (dispute.FinalOutcome == Domain.Enums.DisputeOutcome.VerdictOverridden
            && !string.IsNullOrWhiteSpace(command.OverrideRule))
        {
            dispute.SetOverrideRule(command.OverrideRule);
        }

        // 4. Raise resolved event (captures OverrideRule after it has been set)
        dispute.RaiseResolvedEvent();

        // 5. Backward compatibility: append legacy entry to session
        var session = await _sessionRepository
            .GetByIdAsync(dispute.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session is not null)
        {
            session.AddDispute(dispute.ToLegacyEntry());

            await _sessionRepository
                .UpdateAsync(session, cancellationToken)
                .ConfigureAwait(false);
        }

        // 6. Update dispute and persist
        await _disputeRepository
            .UpdateAsync(dispute, cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
