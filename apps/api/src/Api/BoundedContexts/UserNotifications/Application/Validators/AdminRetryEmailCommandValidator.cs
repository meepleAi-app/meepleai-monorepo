using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

internal sealed class AdminRetryEmailCommandValidator : AbstractValidator<AdminRetryEmailCommand>
{
    public AdminRetryEmailCommandValidator()
    {
        RuleFor(x => x.EmailId)
            .NotEmpty()
            .WithMessage("EmailId is required");
    }
}
