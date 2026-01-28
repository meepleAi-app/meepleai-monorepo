using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for SuspendUserCommand (Issue #2886).
/// Ensures UserId is valid before processing.
/// </summary>
internal sealed class SuspendUserCommandValidator : AbstractValidator<SuspendUserCommand>
{
    public SuspendUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required")
            .Must(BeValidGuid)
            .WithMessage("UserId must be a valid GUID format");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Suspension reason cannot exceed 500 characters");
    }

    private static bool BeValidGuid(string? userId)
    {
        return !string.IsNullOrWhiteSpace(userId) && Guid.TryParse(userId, out _);
    }
}
