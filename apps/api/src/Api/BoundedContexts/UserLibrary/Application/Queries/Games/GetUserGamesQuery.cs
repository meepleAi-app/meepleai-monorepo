using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.Games;

/// <summary>
/// Query to get user's game collection with filters and pagination (Issue #4580)
/// Epic #4575: Gaming Hub Dashboard - Phase 1
/// </summary>
internal record GetUserGamesQuery : IRequest<PagedResult<UserGameDto>>
{
    public string? Category { get; init; }
    public string Sort { get; init; } = "alphabetical";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Validator for GetUserGamesQuery (Issue #4580)
/// </summary>
internal class GetUserGamesQueryValidator : AbstractValidator<GetUserGamesQuery>
{
    private static readonly string[] ValidCategories = { "all", "strategy", "family", "party", "solo", "cooperative" };
    private static readonly string[] ValidSorts = { "alphabetical", "lastPlayed", "rating", "playCount" };

    public GetUserGamesQueryValidator()
    {
        RuleFor(x => x.Category)
            .Must(c => c == null || ValidCategories.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Category must be one of: {string.Join(", ", ValidCategories)}");

        RuleFor(x => x.Sort)
            .Must(s => ValidSorts.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Sort must be one of: {string.Join(", ", ValidSorts)}");

        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("Page must be greater than 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100");
    }
}

/// <summary>
/// DTO for user game in collection (Issue #4580)
/// Optimized for MeepleCard display
/// </summary>
internal record UserGameDto(
    Guid Id,
    string Title,
    string? Publisher,
    string? ImageUrl,
    string? ThumbnailUrl,
    decimal? AverageRating,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTimeMinutes,
    decimal? ComplexityRating,
    int? PlayCount,
    DateTime? LastPlayed,
    bool IsOwned,
    bool InWishlist);

/// <summary>
/// Paged result wrapper for collections (Issue #4580)
/// </summary>
internal record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
