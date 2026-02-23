using FluentValidation;

namespace Api.BoundedContexts.EntityRelationships.Application.Validators;

/// <summary>
/// Placeholder validator for EntityRelationships BC — commands validators added in Issue #5133+.
/// Required for assembly scanning in AddFluentValidation.
/// </summary>
internal sealed class CreateEntityLinkCommandPlaceholderValidator : AbstractValidator<CreateEntityLinkCommandPlaceholder>
{
    public CreateEntityLinkCommandPlaceholderValidator()
    {
        // Full validation implemented in Issue #5133 (CreateEntityLinkCommand)
    }
}

/// <summary>
/// Placeholder command record — replaced by full command in Issue #5133.
/// </summary>
internal sealed record CreateEntityLinkCommandPlaceholder
{
    // Replaced by CreateEntityLinkCommand in Issue #5133
    public string Placeholder { get; init; } = string.Empty;
}
