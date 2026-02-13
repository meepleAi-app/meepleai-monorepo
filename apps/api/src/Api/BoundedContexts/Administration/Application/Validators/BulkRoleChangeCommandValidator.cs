using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for BulkRoleChangeCommand.
/// Hotfix: Code review finding - missing validator
/// </summary>
internal class BulkRoleChangeCommandValidator : AbstractValidator<BulkRoleChangeCommand>
{
    public BulkRoleChangeCommandValidator()
    {
        RuleFor(x => x.RequesterId)
            .NotEmpty()
            .WithMessage("RequesterId is required");

        RuleFor(x => x.UserIds)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one user ID is required")
            .Must(ids => ids.Count <= 1000)
            .WithMessage("Maximum 1000 users can be updated at once")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("All user IDs must be valid");

        RuleFor(x => x.NewRole)
            .IsInEnum()
            .WithMessage("Invalid role specified");
    }
}
