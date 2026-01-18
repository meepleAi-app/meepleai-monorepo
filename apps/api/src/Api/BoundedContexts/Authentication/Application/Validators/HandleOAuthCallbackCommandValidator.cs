using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for HandleOAuthCallbackCommand.
/// Ensures OAuth provider is supported and required parameters are present.
/// Issue #2600: Fix OAuth Callback Tests - validation for provider, code, and state.
/// </summary>
internal sealed class HandleOAuthCallbackCommandValidator : AbstractValidator<HandleOAuthCallbackCommand>
{
    private static readonly string[] SupportedProviders = { "google", "github", "discord" };

    public HandleOAuthCallbackCommandValidator()
    {
        RuleFor(x => x.Provider)
            .NotEmpty()
            .WithMessage("OAuth provider is required")
            .Must(BeASupportedProvider)
            .WithMessage(x => $"Unsupported OAuth provider: {x.Provider}. Supported providers: google, github, discord");

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Authorization code is required");

        RuleFor(x => x.State)
            .NotEmpty()
            .WithMessage("State parameter is required");
    }

    private static bool BeASupportedProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return false;
        }

        return SupportedProviders.Contains(provider, StringComparer.OrdinalIgnoreCase);
    }
}
