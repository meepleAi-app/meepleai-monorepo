using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Validator for advanced activity timeline query.
/// Issue #4315: Validates filters and pagination.
/// </summary>
internal class GetActivityTimelineQueryValidator : AbstractValidator<GetActivityTimelineQuery>
{
    public GetActivityTimelineQueryValidator()
    {
        RuleFor(q => q.UserId).NotEmpty();
        RuleFor(q => q.Take).InclusiveBetween(1, 100);
        When(q => q.DateFrom.HasValue && q.DateTo.HasValue, () =>
        {
            RuleFor(q => q.DateTo).GreaterThan(q => q.DateFrom);
        });
    }
}
