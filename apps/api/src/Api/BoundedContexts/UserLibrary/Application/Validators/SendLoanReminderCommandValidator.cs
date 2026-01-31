using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for SendLoanReminderCommand.
/// </summary>
internal class SendLoanReminderCommandValidator : AbstractValidator<SendLoanReminderCommand>
{
    public SendLoanReminderCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.CustomMessage)
            .MaximumLength(500)
            .When(x => x.CustomMessage != null)
            .WithMessage("CustomMessage cannot exceed 500 characters");
    }
}
