using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.OAuth;

/// <summary>
/// Validates UnlinkOAuthAccountCommand.
/// Ensures user ID and provider are provided.
/// </summary>
internal sealed class UnlinkOAuthAccountCommandValidator : AbstractValidator<UnlinkOAuthAccountCommand>
{
    public UnlinkOAuthAccountCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.Provider)
            .NotEmpty()
            .WithMessage("OAuth provider is required")
            .MaximumLength(50)
            .WithMessage("Provider name must not exceed 50 characters");
    }
}
