using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.Providers;

/// <summary>
/// Validator for <see cref="RotateProviderKeyCommand"/>. Issue #1859.
/// - <c>ProviderName</c>: NotEmpty + whitelist (case-insensitive against
///   <see cref="ProviderName.Allowed"/>).
/// - <c>ConfirmedProviderName</c>: must equal <c>ProviderName</c> (typo guard, FE double-entry).
/// - <c>NewApiKey</c>: NotEmpty + length 10..512 + no whitespace.
/// - <c>RequestingUserId</c>: NotEqual(Guid.Empty).
/// </summary>
internal sealed class RotateProviderKeyCommandValidator
    : AbstractValidator<RotateProviderKeyCommand>
{
    internal const int MinApiKeyLength = 10;
    internal const int MaxApiKeyLength = 512;

    public RotateProviderKeyCommandValidator()
    {
        RuleFor(x => x.ProviderName).Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage("Provider name is required")
            .Must(IsAllowedProviderName)
                .WithMessage(_ =>
                    $"Provider must be one of: {string.Join(", ", ProviderName.Allowed.OrderBy(p => p, StringComparer.Ordinal))}");

        RuleFor(x => x.ConfirmedProviderName)
            .Equal(x => x.ProviderName)
                .WithMessage("Provider name confirmation does not match the route parameter (typo guard)");

        RuleFor(x => x.NewApiKey).Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage("New API key is required")
            .MinimumLength(MinApiKeyLength)
                .WithMessage($"New API key must be at least {MinApiKeyLength} characters")
            .MaximumLength(MaxApiKeyLength)
                .WithMessage($"New API key must be at most {MaxApiKeyLength} characters")
            .Must(NotContainWhitespace)
                .WithMessage("New API key must not contain whitespace");

        RuleFor(x => x.RequestingUserId)
            .NotEqual(Guid.Empty)
                .WithMessage("Requesting user id is required");
    }

    private static bool IsAllowedProviderName(string providerName) =>
        !string.IsNullOrWhiteSpace(providerName)
        && ProviderName.Allowed.Contains(providerName);

    private static bool NotContainWhitespace(string apiKey) =>
        !string.IsNullOrEmpty(apiKey) && !apiKey.Any(char.IsWhiteSpace);
}
