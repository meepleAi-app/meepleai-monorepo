using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.OAuth;

/// <summary>
/// Validates LinkOAuthAccountCommand.
/// Ensures user ID, provider, provider user ID, and access token are provided.
/// </summary>
internal sealed class LinkOAuthAccountCommandValidator : AbstractValidator<LinkOAuthAccountCommand>
{
    private static readonly string[] ValidProviders = { "google", "github", "discord", "microsoft" };

    public LinkOAuthAccountCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.Provider)
            .NotEmpty()
            .WithMessage("OAuth provider is required")
            .MaximumLength(50)
            .WithMessage("Provider name must not exceed 50 characters")
            .Must(provider => ValidProviders.Contains(provider.ToLowerInvariant(), StringComparer.Ordinal))
            .WithMessage($"Provider must be one of: {string.Join(", ", ValidProviders)}")
            .When(x => !string.IsNullOrEmpty(x.Provider));

        RuleFor(x => x.ProviderUserId)
            .NotEmpty()
            .WithMessage("Provider user ID is required")
            .MaximumLength(500)
            .WithMessage("Provider user ID must not exceed 500 characters");

        RuleFor(x => x.AccessTokenEncrypted)
            .NotEmpty()
            .WithMessage("Access token is required")
            .MaximumLength(4096)
            .WithMessage("Access token must not exceed 4096 characters");

        RuleFor(x => x.RefreshTokenEncrypted)
            .MaximumLength(4096)
            .WithMessage("Refresh token must not exceed 4096 characters")
            .When(x => x.RefreshTokenEncrypted != null);

        RuleFor(x => x.TokenExpiresAt)
            .Must(date => !date.HasValue || date.Value > DateTime.UtcNow)
            .WithMessage("Token expiration must be in the future");
    }
}
