using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handles confirming a previously parsed score by delegating to RecordLiveSessionScoreCommand.
/// </summary>
internal sealed class ConfirmScoreCommandHandler : ICommandHandler<ConfirmScoreCommand>
{
    private readonly IMediator _mediator;

    public ConfirmScoreCommandHandler(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task Handle(ConfirmScoreCommand command, CancellationToken cancellationToken)
    {
        await _mediator.Send(new RecordLiveSessionScoreCommand(
            command.SessionId,
            command.PlayerId,
            command.Round,
            command.Dimension,
            command.Value), cancellationToken).ConfigureAwait(false);
    }
}
