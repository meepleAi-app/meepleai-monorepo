using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetPopularAgents;

/// <summary>
/// Validator for <see cref="GetPopularAgentsQuery"/>.
/// Wave 3 Phase 1, PR #732 §4.3.3 / Issue #805.
/// </summary>
internal sealed class GetPopularAgentsQueryValidator : AbstractValidator<GetPopularAgentsQuery>
{
    public GetPopularAgentsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithMessage("Limit must be between 1 and 50.");
    }
}
