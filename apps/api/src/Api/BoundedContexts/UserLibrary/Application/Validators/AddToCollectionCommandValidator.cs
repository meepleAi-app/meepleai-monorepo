using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for AddToCollectionCommand.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal class AddToCollectionCommandValidator : AbstractValidator<AddToCollectionCommand>
{
    public AddToCollectionCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.EntityId)
            .NotEmpty()
            .WithMessage("Entity ID is required");

        RuleFor(x => x.EntityType)
            .IsInEnum()
            .WithMessage("Invalid entity type");

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .When(x => x.Notes != null)
            .WithMessage("Notes cannot exceed 2000 characters");
    }
}
