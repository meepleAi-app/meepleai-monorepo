using Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;

/// <summary>
/// Validator for GetPrivateGamesListQuery.
/// </summary>
internal sealed class GetPrivateGamesListQueryValidator : AbstractValidator<GetPrivateGamesListQuery>
{
    private static readonly string[] AllowedSortFields = ["title", "createdAt", "updatedAt"];
    private static readonly string[] AllowedSortDirections = ["asc", "desc"];

    public GetPrivateGamesListQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be >= 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("PageSize must be between 1 and 50");

        RuleFor(x => x.SortBy)
            .Must(v => AllowedSortFields.Contains(v, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortBy must be one of: title, createdAt, updatedAt");

        RuleFor(x => x.SortDirection)
            .Must(v => AllowedSortDirections.Contains(v, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortDirection must be asc or desc");
    }
}
