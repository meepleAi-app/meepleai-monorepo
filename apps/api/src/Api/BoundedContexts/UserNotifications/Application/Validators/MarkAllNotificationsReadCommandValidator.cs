using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

/// <summary>
/// Validator for MarkAllNotificationsReadCommand.
/// Ensures UserId is provided.
/// Issue #2153: Add input validation to notification commands
/// </summary>
public sealed class MarkAllNotificationsReadCommandValidator : AbstractValidator<MarkAllNotificationsReadCommand>
{
    public MarkAllNotificationsReadCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
