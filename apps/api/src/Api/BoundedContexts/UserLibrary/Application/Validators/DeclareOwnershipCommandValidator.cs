using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for DeclareOwnershipCommand.
/// </summary>
internal class DeclareOwnershipCommandValidator : AbstractValidator<DeclareOwnershipCommand>
{
    public DeclareOwnershipCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");
    }
}
