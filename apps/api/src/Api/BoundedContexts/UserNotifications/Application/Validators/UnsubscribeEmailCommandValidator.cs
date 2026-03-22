using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

internal sealed class UnsubscribeEmailCommandValidator : AbstractValidator<UnsubscribeEmailCommand>
{
    public UnsubscribeEmailCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.NotificationType)
            .NotEmpty()
            .WithMessage("NotificationType is required")
            .MaximumLength(100)
            .WithMessage("NotificationType must not exceed 100 characters");

        RuleFor(x => x.Source)
            .NotEmpty()
            .WithMessage("Source is required")
            .MaximumLength(100)
            .WithMessage("Source must not exceed 100 characters");
    }
}
