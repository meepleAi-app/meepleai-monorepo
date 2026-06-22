using FluentValidation;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitRatings;

/// <summary>
/// Validator for <see cref="GetToolkitRatingsQuery"/>.
/// Wave 3 Phase 4b, PR #732 §5.3.3 / Issue #805.
/// </summary>
internal sealed class GetToolkitRatingsQueryValidator
    : AbstractValidator<GetToolkitRatingsQuery>
{
    public GetToolkitRatingsQueryValidator()
    {
        RuleFor(x => x.ToolkitId)
            .NotEqual(Guid.Empty)
            .WithMessage("ToolkitId is required.");

        RuleFor(x => x.ViewerId)
            .NotEqual(Guid.Empty)
            .WithMessage("ViewerId is required.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithMessage("Limit must be between 1 and 50.");
    }
}
