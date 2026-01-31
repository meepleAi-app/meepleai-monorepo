using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributionStats;

/// <summary>
/// Validator for GetUserContributionStatsQuery.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
internal sealed class GetUserContributionStatsQueryValidator : AbstractValidator<GetUserContributionStatsQuery>
{
    public GetUserContributionStatsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
