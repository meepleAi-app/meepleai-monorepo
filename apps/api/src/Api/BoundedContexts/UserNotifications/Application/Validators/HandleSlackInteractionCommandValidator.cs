using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

internal sealed class HandleSlackInteractionCommandValidator : AbstractValidator<HandleSlackInteractionCommand>
{
    public HandleSlackInteractionCommandValidator()
    {
        RuleFor(x => x.Payload)
            .NotEmpty()
            .WithMessage("Payload is required")
            .MaximumLength(100000)
            .WithMessage("Payload must not exceed 100000 characters");

        RuleFor(x => x.Timestamp)
            .NotEmpty()
            .WithMessage("Timestamp is required")
            .MaximumLength(50)
            .WithMessage("Timestamp must not exceed 50 characters");

        RuleFor(x => x.Signature)
            .NotEmpty()
            .WithMessage("Signature is required")
            .MaximumLength(200)
            .WithMessage("Signature must not exceed 200 characters");
    }
}
