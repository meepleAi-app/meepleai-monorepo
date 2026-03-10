using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for DeleteOwnAccountCommand.
/// </summary>
internal sealed class DeleteOwnAccountCommandValidator : AbstractValidator<DeleteOwnAccountCommand>
{
    public DeleteOwnAccountCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required for account deletion.");
    }
}
