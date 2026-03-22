using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for LoginWithApiKeyCommand.
/// Ensures API key is provided with appropriate length constraints.
/// </summary>
internal sealed class LoginWithApiKeyCommandValidator : AbstractValidator<LoginWithApiKeyCommand>
{
    public LoginWithApiKeyCommandValidator()
    {
        RuleFor(x => x.ApiKey)
            .NotEmpty()
            .WithMessage("API key is required")
            .MaximumLength(500)
            .WithMessage("API key must not exceed 500 characters");
    }
}
