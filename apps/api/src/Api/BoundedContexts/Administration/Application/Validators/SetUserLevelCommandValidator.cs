using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for SetUserLevelCommand.
/// Issue #3141: Ensures valid user ID and non-negative level.
/// </summary>
internal sealed class SetUserLevelCommandValidator
    : AbstractValidator<SetUserLevelCommand>
{
    public SetUserLevelCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required")
            .NotEqual(Guid.Empty)
            .WithMessage("UserId cannot be empty GUID");

        RuleFor(x => x.Level)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Level must be >= 0")
            .LessThanOrEqualTo(100)
            .WithMessage("Level must be <= 100");
    }
}
