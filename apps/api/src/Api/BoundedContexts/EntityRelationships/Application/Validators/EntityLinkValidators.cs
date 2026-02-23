using Api.BoundedContexts.EntityRelationships.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.EntityRelationships.Application.Validators;

/// <summary>
/// Validator for CreateEntityLinkCommand (Issue #5133).
///
/// Enforces:
/// - OwnerUserId must be non-empty
/// - Source and target entity IDs must be non-empty
/// - Metadata length ≤ EntityRelationshipsConstants.MetadataMaxLength (enforced also in aggregate)
/// Source != Target identity check is delegated to EntityLink.Create (domain rule).
/// </summary>
internal sealed class CreateEntityLinkCommandValidator : AbstractValidator<CreateEntityLinkCommand>
{
    public CreateEntityLinkCommandValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEmpty()
            .WithMessage("OwnerUserId is required.");

        RuleFor(x => x.SourceEntityId)
            .NotEmpty()
            .WithMessage("SourceEntityId must not be empty.");

        RuleFor(x => x.TargetEntityId)
            .NotEmpty()
            .WithMessage("TargetEntityId must not be empty.");

        // Prevent self-link at validation level (belt-and-suspenders alongside domain check)
        RuleFor(x => x)
            .Must(x => !(x.SourceEntityType == x.TargetEntityType && x.SourceEntityId == x.TargetEntityId))
            .WithName("TargetEntityId")
            .WithMessage("Source and target entity cannot be the same.");

        RuleFor(x => x.Scope)
            .IsInEnum()
            .WithMessage("Scope must be a valid EntityLinkScope value.");

        RuleFor(x => x.LinkType)
            .IsInEnum()
            .WithMessage("LinkType must be a valid EntityLinkType value.");

        RuleFor(x => x.SourceEntityType)
            .IsInEnum()
            .WithMessage("SourceEntityType must be a valid MeepleEntityType value.");

        RuleFor(x => x.TargetEntityType)
            .IsInEnum()
            .WithMessage("TargetEntityType must be a valid MeepleEntityType value.");
    }
}
