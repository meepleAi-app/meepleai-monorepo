using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for RevokeLibraryShareLinkCommand.
/// </summary>
internal sealed class RevokeLibraryShareLinkCommandValidator : AbstractValidator<RevokeLibraryShareLinkCommand>
{
    public RevokeLibraryShareLinkCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.ShareToken)
            .NotEmpty()
            .WithMessage("ShareToken is required")
            .Length(32)
            .WithMessage("ShareToken must be exactly 32 characters");
    }
}
