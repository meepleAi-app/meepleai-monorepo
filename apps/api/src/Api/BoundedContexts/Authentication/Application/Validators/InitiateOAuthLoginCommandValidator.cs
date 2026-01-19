using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for InitiateOAuthLoginCommand.
/// Ensures OAuth provider is supported and properly formatted.
/// Issue #2646: Code review fix - move validation from handler to validator.
/// </summary>
internal sealed class InitiateOAuthLoginCommandValidator : AbstractValidator<InitiateOAuthLoginCommand>
{
    private static readonly string[] SupportedProviders = { "google", "github", "discord" };

    public InitiateOAuthLoginCommandValidator()
    {
        RuleFor(x => x.Provider)
            .NotEmpty()
            .WithMessage("OAuth provider must be specified")
            .Must(BeASupportedProvider)
            .WithMessage(x => $"Unsupported OAuth provider: {x.Provider}. Supported providers: google, github, discord");
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
