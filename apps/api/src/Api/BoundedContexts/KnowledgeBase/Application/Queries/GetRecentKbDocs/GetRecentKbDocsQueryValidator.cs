using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocs;

/// <summary>
/// Validator for <see cref="GetRecentKbDocsQuery"/>.
/// Wave 3 Phase 1, PR #732 §4.3.5 / Issue #805.
/// </summary>
internal sealed class GetRecentKbDocsQueryValidator : AbstractValidator<GetRecentKbDocsQuery>
{
    public GetRecentKbDocsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithMessage("Limit must be between 1 and 50.");
    }
}
