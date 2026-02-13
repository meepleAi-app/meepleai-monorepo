using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for RemoveFromCollectionCommand.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal class RemoveFromCollectionCommandValidator : AbstractValidator<RemoveFromCollectionCommand>
{
    public RemoveFromCollectionCommandValidator()
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
    }
}
