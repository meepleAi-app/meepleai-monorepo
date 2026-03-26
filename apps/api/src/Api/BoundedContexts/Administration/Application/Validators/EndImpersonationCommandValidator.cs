using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for EndImpersonationCommand.
/// Ensures session ID and admin ID are valid.
/// </summary>
internal sealed class EndImpersonationCommandValidator : AbstractValidator<EndImpersonationCommand>
{
    public EndImpersonationCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.AdminUserId)
            .NotEmpty()
            .WithMessage("AdminUserId is required");
    }
}
