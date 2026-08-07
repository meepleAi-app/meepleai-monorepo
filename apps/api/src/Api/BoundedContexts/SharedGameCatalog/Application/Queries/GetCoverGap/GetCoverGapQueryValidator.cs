using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCoverGap;

/// <summary>
/// Boundary validation per <see cref="GetCoverGapQuery"/> — un 422 via la pipeline
/// FluentValidation, non un 500, su input fuori range o causa sconosciuta.
/// </summary>
internal sealed class GetCoverGapQueryValidator : AbstractValidator<GetCoverGapQuery>
{
    public GetCoverGapQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be >= 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100");

        RuleFor(x => x.Cause)
            .Must(c => c is null || CoverGapCauses.All.Contains(c, StringComparer.Ordinal))
            .WithMessage($"Cause must be one of: {string.Join(", ", CoverGapCauses.All)}");
    }
}
