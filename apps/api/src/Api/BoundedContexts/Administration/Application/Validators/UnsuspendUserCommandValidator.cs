using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for UnsuspendUserCommand (Issue #2886).
/// Ensures UserId is valid before processing.
/// </summary>
internal sealed class UnsuspendUserCommandValidator : AbstractValidator<UnsuspendUserCommand>
{
    public UnsuspendUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required")
            .Must(BeValidGuid)
            .WithMessage("UserId must be a valid GUID format");
    }

    private static bool BeValidGuid(string? userId)
    {
        return !string.IsNullOrWhiteSpace(userId) && Guid.TryParse(userId, out _);
    }
}
