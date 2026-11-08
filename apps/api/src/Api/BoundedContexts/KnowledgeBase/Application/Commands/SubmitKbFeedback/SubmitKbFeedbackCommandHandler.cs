using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.SubmitKbFeedback;

internal sealed class SubmitKbFeedbackCommandHandler : IRequestHandler<SubmitKbFeedbackCommand>
{
    private readonly IKbUserFeedbackRepository _repo;
    private readonly IChatThreadRepository _threads;

    public SubmitKbFeedbackCommandHandler(IKbUserFeedbackRepository repo, IChatThreadRepository threads)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _threads = threads ?? throw new ArgumentNullException(nameof(threads));
    }

    public async Task Handle(SubmitKbFeedbackCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // #4168 (ADR-095 D2): il feedback valuta una risposta salvata, e da quella prende la versione
        // del profilo e la lingua. Un thread di un altro utente o di un altro gioco vale come assente,
        // come un messaggio che nel thread non c'è o che non è una risposta: prima si registrava
        // qualunque id, e la riga non si poteva ricondurre a nulla.
        var thread = await _threads.GetByIdAsync(command.ChatSessionId, cancellationToken).ConfigureAwait(false);
        if (thread is null || thread.UserId != command.UserId || thread.GameId != command.GameId)
        {
            throw new NotFoundException("ChatThread", command.ChatSessionId.ToString());
        }

        var answer = thread.Messages.FirstOrDefault(m => m.Id == command.MessageId && m.IsAssistantMessage && !m.IsDeleted)
            ?? throw new NotFoundException("ChatMessage", command.MessageId.ToString());

        var feedback = KbUserFeedback.Create(
            command.UserId,
            command.GameId,
            command.ChatSessionId,
            command.MessageId,
            command.Outcome,
            command.Comment,
            answer.ProfileVersionId,
            answer.Language);

        await _repo.AddAsync(feedback, cancellationToken).ConfigureAwait(false);
    }
}
