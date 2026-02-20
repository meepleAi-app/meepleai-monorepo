using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class SendSessionChatMessageCommandValidator : AbstractValidator<SendSessionChatMessageCommand>
{
    public SendSessionChatMessageCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required.");
        RuleFor(x => x.SenderId).NotEmpty().WithMessage("Sender ID is required.");
        RuleFor(x => x.Content).NotEmpty().MaximumLength(5000)
            .WithMessage("Message content must be 1-5000 characters.");
    }
}

public class SendSystemEventCommandValidator : AbstractValidator<SendSystemEventCommand>
{
    public SendSystemEventCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(1000);
    }
}

public class AskSessionAgentCommandValidator : AbstractValidator<AskSessionAgentCommand>
{
    public AskSessionAgentCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required.");
        RuleFor(x => x.SenderId).NotEmpty().WithMessage("Sender ID is required.");
        RuleFor(x => x.Question).NotEmpty().MaximumLength(2000)
            .WithMessage("Question must be 1-2000 characters.");
    }
}

public class DeleteChatMessageCommandValidator : AbstractValidator<DeleteChatMessageCommand>
{
    public DeleteChatMessageCommandValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.RequesterId).NotEmpty();
    }
}
