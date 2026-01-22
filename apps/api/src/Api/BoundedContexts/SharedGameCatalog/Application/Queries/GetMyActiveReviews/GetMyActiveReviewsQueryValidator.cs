using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetMyActiveReviews;

/// <summary>
/// Validator for GetMyActiveReviewsQuery.
/// </summary>
internal sealed class GetMyActiveReviewsQueryValidator : AbstractValidator<GetMyActiveReviewsQuery>
{
    public GetMyActiveReviewsQueryValidator()
    {
        RuleFor(x => x.AdminId)
            .NotEmpty().WithMessage("AdminId is required");
    }
}
