using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for BulkPasswordResetCommand.
/// Hotfix: Code review finding - missing validator
/// </summary>
internal class BulkPasswordResetCommandValidator : AbstractValidator<BulkPasswordResetCommand>
{
    public BulkPasswordResetCommandValidator()
    {
        RuleFor(x => x.RequesterId)
            .NotEmpty()
            .WithMessage("RequesterId is required");

        RuleFor(x => x.UserIds)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one user ID is required")
            .Must(ids => ids.Count <= 1000)
            .WithMessage("Maximum 1000 users can be reset at once")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("All user IDs must be valid");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("NewPassword is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters")
            .MaximumLength(128)
            .WithMessage("Password cannot exceed 128 characters");
    }
}
