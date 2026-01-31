using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserShareRequests;

/// <summary>
/// Validator for GetUserShareRequestsQuery.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
internal sealed class GetUserShareRequestsQueryValidator : AbstractValidator<GetUserShareRequestsQuery>
{
    public GetUserShareRequestsQueryValidator()
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
