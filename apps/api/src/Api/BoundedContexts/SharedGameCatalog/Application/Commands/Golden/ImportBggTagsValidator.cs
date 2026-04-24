using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;

/// <summary>
/// Validator for <see cref="ImportBggTagsCommand"/>. Enforces the surface-level invariants on the
/// incoming batch: every tag must have a non-empty name (&lt;= 200 chars) and category (&lt;= 100
/// chars) so the persisted <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Entities.MechanicGoldenBggTag"/>
/// factory never rejects the row. An empty tag list is a valid no-op and is permitted.
/// </summary>
internal sealed class ImportBggTagsValidator : AbstractValidator<ImportBggTagsCommand>
{
    public ImportBggTagsValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEmpty().WithMessage("SharedGameId is required");

        RuleFor(x => x.Tags)
            .NotNull().WithMessage("Tags list is required (empty list is allowed)");

        RuleForEach(x => x.Tags).ChildRules(tag =>
        {
            tag.RuleFor(t => t.Name)
                .NotEmpty().WithMessage("Tag name is required")
                .MaximumLength(200).WithMessage("Tag name must be <= 200 characters");

            tag.RuleFor(t => t.Category)
                .NotEmpty().WithMessage("Tag category is required")
                .MaximumLength(100).WithMessage("Tag category must be <= 100 characters");
        });
    }
}
