using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for CreateApiKeyCommand.
/// Ensures key name, scopes, and optional expiration are valid.
/// </summary>
internal sealed class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.KeyName)
            .NotEmpty()
            .WithMessage("Key name is required")
            .MaximumLength(100)
            .WithMessage("Key name must not exceed 100 characters")
            .Matches(@"^[a-zA-Z0-9\s\-_\.]+$")
            .WithMessage("Key name can only contain letters, numbers, spaces, hyphens, underscores, and periods");

        RuleFor(x => x.Scopes)
            .NotEmpty()
            .WithMessage("Scopes are required")
            .MaximumLength(1000)
            .WithMessage("Scopes must not exceed 1000 characters");

        RuleFor(x => x.ExpiresAt)
            .Must(date => !date.HasValue || date.Value > DateTime.UtcNow)
            .WithMessage("Expiration date must be in the future");

        RuleFor(x => x.Metadata)
            .MaximumLength(4000)
            .WithMessage("Metadata must not exceed 4000 characters")
            .When(x => x.Metadata != null);
    }
}
