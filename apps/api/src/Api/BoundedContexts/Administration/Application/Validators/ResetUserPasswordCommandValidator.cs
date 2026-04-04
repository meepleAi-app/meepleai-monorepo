using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for ResetUserPasswordCommand.
/// Ensures user ID is a valid GUID and password meets minimum requirements.
/// </summary>
internal sealed class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required")
            .Must(BeValidGuid)
            .WithMessage("UserId must be a valid GUID format");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("NewPassword is required")
            .MinimumLength(8)
            .WithMessage("NewPassword must be at least 8 characters");
    }

    private static bool BeValidGuid(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value, out _);
    }
}
