using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Queries.GetTopUserContributors;

/// <summary>
/// Validator for <see cref="GetTopUserContributorsQuery"/>.
/// Wave 3 Phase 4a, PR #732 §4.3.6 / Issue #805.
/// </summary>
internal sealed class GetTopUserContributorsQueryValidator
    : AbstractValidator<GetTopUserContributorsQuery>
{
    public GetTopUserContributorsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithMessage("Limit must be between 1 and 50.");
    }
}
