using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for DeleteApiKeyCommand.
/// Ensures key ID and user ID are provided.
/// </summary>
internal sealed class DeleteApiKeyCommandValidator : AbstractValidator<DeleteApiKeyCommand>
{
    public DeleteApiKeyCommandValidator()
    {
        RuleFor(x => x.KeyId)
            .NotEmpty()
            .WithMessage("API key ID is required")
            .MaximumLength(100)
            .WithMessage("API key ID must not exceed 100 characters");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required")
            .MaximumLength(100)
            .WithMessage("User ID must not exceed 100 characters");
    }
}
