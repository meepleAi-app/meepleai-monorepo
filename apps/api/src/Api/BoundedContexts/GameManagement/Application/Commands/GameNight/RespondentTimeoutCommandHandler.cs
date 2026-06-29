using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// Handles <see cref="RespondentTimeoutCommand"/>.
/// If no respondent has been set, the dispute proceeds with the initiator's claim only.
/// This is a no-op if the respondent has already been set.
/// </summary>
internal sealed class RespondentTimeoutCommandHandler
    : ICommandHandler<RespondentTimeoutCommand>
{
    private readonly IRuleDisputeRepository _disputeRepository;

    public RespondentTimeoutCommandHandler(IRuleDisputeRepository disputeRepository)
    {
        _disputeRepository = disputeRepository ?? throw new ArgumentNullException(nameof(disputeRepository));
    }

    public async Task Handle(
        RespondentTimeoutCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Get dispute — throw if not found
        var dispute = await _disputeRepository
            .GetByIdAsync(command.DisputeId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("RuleDispute", command.DisputeId.ToString());

        // Cross-session authz (#2573): the dispute must belong to the route session.
        // 404 (not 403) to avoid leaking the existence of disputes in other sessions.
        if (dispute.SessionId != command.SessionId)
            throw new NotFoundException("RuleDispute", command.DisputeId.ToString());

        // 2. No-op if respondent already set (or not).
        //    Dispute proceeds with initiator claim only.
    }
}
