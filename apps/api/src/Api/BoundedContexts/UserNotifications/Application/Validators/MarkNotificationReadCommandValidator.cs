using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

/// <summary>
/// Validator for MarkNotificationReadCommand.
/// Ensures NotificationId and UserId are provided.
/// Issue #2153: Add input validation to notification commands
/// </summary>
internal sealed class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(x => x.NotificationId)
            .NotEmpty()
            .WithMessage("NotificationId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
