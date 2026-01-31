using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for RemoveGameFromLibraryCommand.
/// </summary>
internal sealed class RemoveGameFromLibraryCommandValidator : AbstractValidator<RemoveGameFromLibraryCommand>
{
    public RemoveGameFromLibraryCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");
    }
}
