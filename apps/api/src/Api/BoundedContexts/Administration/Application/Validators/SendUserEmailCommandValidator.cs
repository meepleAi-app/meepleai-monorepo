using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for SendUserEmailCommand.
/// Ensures user ID, subject, body, and requester ID are valid.
/// </summary>
internal sealed class SendUserEmailCommandValidator : AbstractValidator<SendUserEmailCommand>
{
    public SendUserEmailCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Subject)
            .NotEmpty()
            .WithMessage("Subject is required")
            .MaximumLength(200)
            .WithMessage("Subject must not exceed 200 characters");

        RuleFor(x => x.Body)
            .NotEmpty()
            .WithMessage("Body is required")
            .MaximumLength(10000)
            .WithMessage("Body must not exceed 10000 characters");

        RuleFor(x => x.RequesterId)
            .NotEmpty()
            .WithMessage("RequesterId is required");
    }
}
