using System.Globalization;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Validator for GetFilteredSharedGamesQuery.
/// Issue #3533: Admin API Endpoints - Approval Queue Management
/// </summary>
internal sealed class GetFilteredSharedGamesQueryValidator : AbstractValidator<GetFilteredSharedGamesQuery>
{
    private static readonly string[] AllowedSortFields = ["title", "createdat", "modifiedat", "status", "yearpublished"];

    public GetFilteredSharedGamesQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("PageNumber must be greater than 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100");

        When(x => !string.IsNullOrWhiteSpace(x.Search), () =>
        {
            RuleFor(x => x.Search)
                .MaximumLength(200)
                .WithMessage("Search term must not exceed 200 characters");
        });

        When(x => !string.IsNullOrWhiteSpace(x.SortBy), () =>
        {
            RuleFor(x => x.SortBy)
                .Must(sortBy =>
                {
                    if (string.IsNullOrWhiteSpace(sortBy))
                        return true;

                    var parts = sortBy.Split(':');
                    var fieldName = parts[0].Trim().ToLower(CultureInfo.InvariantCulture);

                    if (!AllowedSortFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase))
                        return false;

                    if (parts.Length > 1)
                    {
                        var direction = parts[1].Trim().ToLower(CultureInfo.InvariantCulture);
                        return string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
                    }

                    return true;
                })
                .WithMessage("SortBy must be one of: title, createdAt, modifiedAt, status, yearPublished (optionally with :asc or :desc)");
        });
    }
}
