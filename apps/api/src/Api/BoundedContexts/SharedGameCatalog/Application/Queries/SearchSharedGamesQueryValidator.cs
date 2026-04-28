using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Validator for <see cref="SearchSharedGamesQuery"/>.
/// Issue #593 (Wave A.3a): introduced when adding the v2 chip filter params
/// (HasToolkit / HasAgent / IsTopRated). The pre-existing query had no validator,
/// so we cover the *full* surface here — pagination, sort whitelist, and
/// numeric ranges — to surface FE typos at the boundary instead of silently
/// degrading to "Title ASC" or returning empty pages on bad complexity ranges.
///
/// SortBy whitelist includes "Contrib" / "New" (Issue #593 Commit 1b) —
/// these sort by ContributorsCount / NewThisWeekCount aggregates from the
/// projection introduced in the same commit.
/// </summary>
internal sealed class SearchSharedGamesQueryValidator : AbstractValidator<SearchSharedGamesQuery>
{
    /// <summary>
    /// SortBy values accepted by the handler today. Adding here without a
    /// matching `switch` arm in the handler is a programming error — keep these
    /// two lists in sync.
    /// </summary>
    private static readonly string[] AllowedSortBy =
    {
        "Title",
        "YearPublished",
        "AverageRating",
        "CreatedAt",
        "ComplexityRating",
        // Issue #593 Commit 1b — sort by computed aggregates.
        "Contrib",
        "New"
    };

    public SearchSharedGamesQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("PageNumber must be greater than 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100");

        RuleFor(x => x.SortBy)
            .Must(s => AllowedSortBy.Contains(s, StringComparer.Ordinal))
            .WithMessage($"SortBy must be one of: {string.Join(", ", AllowedSortBy)}");

        // BGG complexity is a 1..5 weight (decimal) — values outside the range
        // are always FE bugs (e.g. passing percent 0..100 by mistake).
        RuleFor(x => x.MinComplexity)
            .InclusiveBetween(1m, 5m)
            .When(x => x.MinComplexity.HasValue)
            .WithMessage("MinComplexity must be between 1 and 5");

        RuleFor(x => x.MaxComplexity)
            .InclusiveBetween(1m, 5m)
            .When(x => x.MaxComplexity.HasValue)
            .WithMessage("MaxComplexity must be between 1 and 5");

        RuleFor(x => x)
            .Must(q => q.MinComplexity is null || q.MaxComplexity is null || q.MaxComplexity >= q.MinComplexity)
            .WithMessage("MaxComplexity must be greater than or equal to MinComplexity");

        RuleFor(x => x.MinPlayers)
            .GreaterThan(0)
            .When(x => x.MinPlayers.HasValue)
            .WithMessage("MinPlayers must be greater than 0");

        RuleFor(x => x.MaxPlayers)
            .GreaterThan(0)
            .When(x => x.MaxPlayers.HasValue)
            .WithMessage("MaxPlayers must be greater than 0");

        RuleFor(x => x)
            .Must(q => q.MinPlayers is null || q.MaxPlayers is null || q.MaxPlayers >= q.MinPlayers)
            .WithMessage("MaxPlayers must be greater than or equal to MinPlayers");

        RuleFor(x => x.MaxPlayingTime)
            .GreaterThan(0)
            .When(x => x.MaxPlayingTime.HasValue)
            .WithMessage("MaxPlayingTime must be greater than 0");
    }
}
