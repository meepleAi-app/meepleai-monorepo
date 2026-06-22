using FluentValidation;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;

/// <summary>
/// Validator for <see cref="GetRecommendedToolkitsQuery"/>.
/// Wave 3 Phase 4a, PR #732 §4.3.4 / Issue #805.
/// </summary>
internal sealed class GetRecommendedToolkitsQueryValidator
    : AbstractValidator<GetRecommendedToolkitsQuery>
{
    public GetRecommendedToolkitsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithMessage("Limit must be between 1 and 50.");
    }
}
