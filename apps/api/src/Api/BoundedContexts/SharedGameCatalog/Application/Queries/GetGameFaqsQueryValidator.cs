using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Validator for GetGameFaqsQuery.
/// Issue #2681: Public FAQs endpoints
/// </summary>
internal sealed class GetGameFaqsQueryValidator : AbstractValidator<GetGameFaqsQuery>
{
    public GetGameFaqsQueryValidator()
    {
        RuleFor(x => x.GameId)
            .NotEqual(Guid.Empty).WithMessage("GameId is required");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100).WithMessage("Limit must be between 1 and 100");

        RuleFor(x => x.Offset)
            .GreaterThanOrEqualTo(0).WithMessage("Offset cannot be negative");
    }
}
