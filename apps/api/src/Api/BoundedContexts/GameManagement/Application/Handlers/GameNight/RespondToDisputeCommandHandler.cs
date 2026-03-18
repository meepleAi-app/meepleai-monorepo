using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.GameNight;

/// <summary>
/// Handles <see cref="RespondToDisputeCommand"/>.
/// Loads the dispute, adds the respondent's counter-claim, and persists.
/// </summary>
internal sealed class RespondToDisputeCommandHandler
    : ICommandHandler<RespondToDisputeCommand>
{
    private readonly IRuleDisputeRepository _disputeRepository;

    public RespondToDisputeCommandHandler(IRuleDisputeRepository disputeRepository)
    {
        _disputeRepository = disputeRepository ?? throw new ArgumentNullException(nameof(disputeRepository));
    }

    public async Task Handle(
        RespondToDisputeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Get dispute
        var dispute = await _disputeRepository
            .GetByIdAsync(command.DisputeId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("RuleDispute", command.DisputeId.ToString());

        // 2. Add respondent claim
        dispute.AddRespondentClaim(command.RespondentPlayerId, command.RespondentClaim);

        // 3. Update
        await _disputeRepository
            .UpdateAsync(dispute, cancellationToken)
            .ConfigureAwait(false);
    }
}
