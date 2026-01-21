using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetPendingShareRequests;

/// <summary>
/// Validator for GetPendingShareRequestsQuery.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
internal sealed class GetPendingShareRequestsQueryValidator : AbstractValidator<GetPendingShareRequestsQuery>
{
    private static readonly ShareRequestStatus[] AllowedStatusFilters =
    [
        ShareRequestStatus.Pending,
        ShareRequestStatus.InReview,
        ShareRequestStatus.ChangesRequested
    ];

    public GetPendingShareRequestsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("PageNumber must be greater than 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100");

        When(x => x.StatusFilter.HasValue, () =>
        {
            RuleFor(x => x.StatusFilter!.Value)
                .Must(status => AllowedStatusFilters.Contains(status))
                .WithMessage("StatusFilter must be one of: Pending, InReview, ChangesRequested");
        });

        When(x => !string.IsNullOrWhiteSpace(x.SearchTerm), () =>
        {
            RuleFor(x => x.SearchTerm)
                .MaximumLength(200)
                .WithMessage("SearchTerm must not exceed 200 characters");
        });
    }
}
