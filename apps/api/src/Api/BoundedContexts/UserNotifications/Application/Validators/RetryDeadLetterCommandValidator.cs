using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

internal sealed class RetryDeadLetterCommandValidator : AbstractValidator<RetryDeadLetterCommand>
{
    public RetryDeadLetterCommandValidator()
    {
        RuleFor(x => x.ItemId)
            .NotEmpty()
            .WithMessage("ItemId is required");
    }
}
