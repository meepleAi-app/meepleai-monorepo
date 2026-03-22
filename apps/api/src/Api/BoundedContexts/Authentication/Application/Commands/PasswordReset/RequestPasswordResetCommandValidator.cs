using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.PasswordReset;

/// <summary>
/// Validates RequestPasswordResetCommand.
/// Ensures email is provided and properly formatted.
/// </summary>
internal sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email must be a valid email address")
            .MaximumLength(255)
            .WithMessage("Email must not exceed 255 characters");
    }
}
