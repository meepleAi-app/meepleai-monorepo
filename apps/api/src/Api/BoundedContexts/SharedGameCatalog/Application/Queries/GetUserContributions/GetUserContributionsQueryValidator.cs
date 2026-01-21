using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributions;

/// <summary>
/// Validator for GetUserContributionsQuery.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
internal sealed class GetUserContributionsQueryValidator : AbstractValidator<GetUserContributionsQuery>
{
    public GetUserContributionsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("PageNumber must be greater than 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100");
    }
}
