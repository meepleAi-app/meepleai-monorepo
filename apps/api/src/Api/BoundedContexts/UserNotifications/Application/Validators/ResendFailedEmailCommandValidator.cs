using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

/// <summary>
/// Validator for ResendFailedEmailCommand.
/// Issue #4417: Email notification queue.
/// </summary>
internal sealed class ResendFailedEmailCommandValidator : AbstractValidator<ResendFailedEmailCommand>
{
    public ResendFailedEmailCommandValidator()
    {
        RuleFor(x => x.EmailId)
            .NotEmpty()
            .WithMessage("EmailId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
