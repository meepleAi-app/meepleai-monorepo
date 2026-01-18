using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for UpdateLibraryEntryCommand.
/// </summary>
internal sealed class UpdateLibraryEntryCommandValidator : AbstractValidator<UpdateLibraryEntryCommand>
{
    public UpdateLibraryEntryCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes cannot exceed 500 characters")
            .When(x => x.Notes != null);
    }
}
