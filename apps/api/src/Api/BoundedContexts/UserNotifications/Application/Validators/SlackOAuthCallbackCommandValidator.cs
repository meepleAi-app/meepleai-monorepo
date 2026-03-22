using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

internal sealed class SlackOAuthCallbackCommandValidator : AbstractValidator<SlackOAuthCallbackCommand>
{
    public SlackOAuthCallbackCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Code is required")
            .MaximumLength(500)
            .WithMessage("Code must not exceed 500 characters");

        RuleFor(x => x.State)
            .NotEmpty()
            .WithMessage("State is required")
            .MaximumLength(500)
            .WithMessage("State must not exceed 500 characters");
    }
}
