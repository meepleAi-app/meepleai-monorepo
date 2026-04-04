using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for ImpersonateUserCommand.
/// Ensures target user and admin IDs are valid and different.
/// </summary>
internal sealed class ImpersonateUserCommandValidator : AbstractValidator<ImpersonateUserCommand>
{
    public ImpersonateUserCommandValidator()
    {
        RuleFor(x => x.TargetUserId)
            .NotEmpty()
            .WithMessage("TargetUserId is required");

        RuleFor(x => x.AdminUserId)
            .NotEmpty()
            .WithMessage("AdminUserId is required");

        RuleFor(x => x)
            .Must(x => x.TargetUserId != x.AdminUserId)
            .WithMessage("Cannot impersonate yourself");
    }
}
