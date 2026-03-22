using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for CreateApiKeyManagementCommand.
/// Ensures user ID is provided and request object is not null.
/// </summary>
internal sealed class CreateApiKeyManagementCommandValidator : AbstractValidator<CreateApiKeyManagementCommand>
{
    public CreateApiKeyManagementCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required")
            .MaximumLength(100)
            .WithMessage("User ID must not exceed 100 characters");

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("API key request details are required");
    }
}
