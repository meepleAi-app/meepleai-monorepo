using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Validates Verify2FACommand.
/// Ensures session token and verification code are provided.
/// </summary>
internal sealed class Verify2FACommandValidator : AbstractValidator<Verify2FACommand>
{
    public Verify2FACommandValidator()
    {
        RuleFor(x => x.SessionToken)
            .NotEmpty()
            .WithMessage("Session token is required")
            .MaximumLength(500)
            .WithMessage("Session token must not exceed 500 characters");

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Verification code is required")
            .MaximumLength(20)
            .WithMessage("Verification code must not exceed 20 characters")
            .Matches(@"^[a-zA-Z0-9\-]+$")
            .WithMessage("Verification code can only contain alphanumeric characters and hyphens");
    }
}
