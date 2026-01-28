using Api.BoundedContexts.Authentication.Application.Commands.EmailVerification;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for VerifyEmailCommand.
/// ISSUE-3071: Email verification backend implementation.
/// </summary>
internal sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("Verification token is required")
            .MinimumLength(10)
            .WithMessage("Invalid verification token format")
            .MaximumLength(256)
            .WithMessage("Invalid verification token format");
    }
}
