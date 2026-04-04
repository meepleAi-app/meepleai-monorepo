using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.SubmitKbFeedback;

internal sealed class SubmitKbFeedbackCommandHandler : IRequestHandler<SubmitKbFeedbackCommand>
{
    private readonly IKbUserFeedbackRepository _repo;

    public SubmitKbFeedbackCommandHandler(IKbUserFeedbackRepository repo)
        => _repo = repo ?? throw new ArgumentNullException(nameof(repo));

    public async Task Handle(SubmitKbFeedbackCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var feedback = KbUserFeedback.Create(
            command.UserId,
            command.GameId,
            command.ChatSessionId,
            command.MessageId,
            command.Outcome,
            command.Comment);

        await _repo.AddAsync(feedback, cancellationToken).ConfigureAwait(false);
    }
}
