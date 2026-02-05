using Api.BoundedContexts.Authentication.Application.Commands.AccountLockout;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for UnlockAccountCommand.
/// Issue #3676: Account lockout after failed login attempts.
/// </summary>
internal sealed class UnlockAccountCommandValidator : AbstractValidator<UnlockAccountCommand>
{
    public UnlockAccountCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.AdminId)
            .NotEmpty()
            .WithMessage("AdminId is required");
    }
}
