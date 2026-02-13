using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for BulkAddToCollectionCommand.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
internal class BulkAddToCollectionCommandValidator : AbstractValidator<BulkAddToCollectionCommand>
{
    public BulkAddToCollectionCommandValidator()
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
            .WithMessage("Maximum 50 entities can be added at once")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("All entity IDs must be valid");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.Notes))
            .WithMessage("Notes cannot exceed 1000 characters");
    }
}
