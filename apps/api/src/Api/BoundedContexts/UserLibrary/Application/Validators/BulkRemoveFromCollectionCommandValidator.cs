using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for BulkRemoveFromCollectionCommand.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
internal class BulkRemoveFromCollectionCommandValidator : AbstractValidator<BulkRemoveFromCollectionCommand>
{
    public BulkRemoveFromCollectionCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.EntityType)
            .IsInEnum()
            .WithMessage("Invalid entity type");

        RuleFor(x => x.EntityIds)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one entity ID is required")
            .Must(ids => ids.Count <= 50)
            .WithMessage("Maximum 50 entities can be removed at once")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("All entity IDs must be valid");
    }
}
