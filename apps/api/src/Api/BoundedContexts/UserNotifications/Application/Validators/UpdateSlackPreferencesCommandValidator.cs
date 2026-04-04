using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

internal sealed class UpdateSlackPreferencesCommandValidator : AbstractValidator<UpdateSlackPreferencesCommand>
{
    public UpdateSlackPreferencesCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
