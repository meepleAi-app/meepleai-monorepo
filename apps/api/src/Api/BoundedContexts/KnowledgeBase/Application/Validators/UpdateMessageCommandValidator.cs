using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for UpdateMessageCommand.
/// </summary>
internal sealed class UpdateMessageCommandValidator : AbstractValidator<UpdateMessageCommand>
{
    public UpdateMessageCommandValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty()
            .WithMessage("ThreadId is required");

        RuleFor(x => x.MessageId)
            .NotEmpty()
            .WithMessage("MessageId is required");

        RuleFor(x => x.NewContent)
            .NotEmpty()
            .WithMessage("NewContent is required")
            .MaximumLength(2000)
            .WithMessage("NewContent cannot exceed 2000 characters");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
