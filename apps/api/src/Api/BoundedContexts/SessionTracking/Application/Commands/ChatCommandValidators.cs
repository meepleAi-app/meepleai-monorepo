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
    private const int MaxImages = 4;
    private const long MaxImageBytes = 10_000_000; // 10 MB per image (pre-processing)

    public AskSessionAgentCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required.");
        RuleFor(x => x.SenderId).NotEmpty().WithMessage("Sender ID is required.");
        RuleFor(x => x.Question)
            .MaximumLength(2000).WithMessage("Question must be at most 2000 characters.");
        // #3390 Slice 3: the turn text may be empty ONLY when an image is attached — that is the
        // vision-derived-query path (the handler derives the retrieval query from the board state).
        // A text-only turn still requires a non-empty question.
        RuleFor(x => x)
            .Must(cmd => !string.IsNullOrWhiteSpace(cmd.Question) || cmd.Images is { Count: > 0 })
            .WithMessage("A question is required unless an image is attached.");
        RuleFor(x => x.Images)
            .Must(imgs => imgs is null || imgs.Count <= MaxImages)
            .WithMessage($"Maximum {MaxImages} images per request.");
        RuleForEach(x => x.Images)
            .ChildRules(img =>
            {
                img.RuleFor(i => i.Data)
                    .NotEmpty().WithMessage("Image data must not be empty.")
                    .Must(d => d.Length <= MaxImageBytes)
                    .WithMessage($"Each image must be under {MaxImageBytes / 1_000_000} MB.");
                img.RuleFor(i => i.MediaType)
                    .NotEmpty().WithMessage("Image media type is required.");
            })
            .When(x => x.Images is { Count: > 0 });
    }
}

public class DeleteChatMessageCommandValidator : AbstractValidator<DeleteChatMessageCommand>
{
    public DeleteChatMessageCommandValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.RequesterUserId).NotEmpty();
    }
}
