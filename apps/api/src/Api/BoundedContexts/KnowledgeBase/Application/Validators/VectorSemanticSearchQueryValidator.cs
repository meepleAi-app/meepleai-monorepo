using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for VectorSemanticSearchQuery.
/// Ensures the query string is provided and the result limit is within acceptable bounds.
/// Task 4: Qdrant → pgvector migration.
/// </summary>
internal sealed class VectorSemanticSearchQueryValidator : AbstractValidator<VectorSemanticSearchQuery>
{
    public VectorSemanticSearchQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query must not be empty.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Limit must be between 1 and 100.");
    }
}
