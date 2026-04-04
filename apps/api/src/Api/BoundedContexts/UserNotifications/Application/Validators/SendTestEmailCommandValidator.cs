using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

internal sealed class SendTestEmailCommandValidator : AbstractValidator<SendTestEmailCommand>
{
    public SendTestEmailCommandValidator()
    {
        RuleFor(x => x.To)
            .NotEmpty()
            .WithMessage("To address is required")
            .EmailAddress()
            .WithMessage("To address must be a valid email");
    }
}
