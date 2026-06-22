using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for <c>ImpersonationEndCommand</c> (SP5 Admin Security S2 — T4).
/// </summary>
internal sealed class ImpersonationEndCommandValidator : AbstractValidator<ImpersonationEndCommand>
{
    public ImpersonationEndCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.RequestingUserId)
            .NotEmpty()
            .WithMessage("RequestingUserId is required");
    }
}
