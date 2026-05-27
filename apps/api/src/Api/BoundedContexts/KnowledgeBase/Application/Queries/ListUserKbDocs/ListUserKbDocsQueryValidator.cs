using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.ListUserKbDocs;

/// <summary>
/// Validator for <see cref="ListUserKbDocsQuery"/> (BE-1 #1588).
/// </summary>
internal sealed class ListUserKbDocsQueryValidator : AbstractValidator<ListUserKbDocsQuery>
{
    private static readonly string[] AllowedSortKeys = ["recent"];
    private static readonly string[] AllowedStateKeys = ["ready", "all"];

    public ListUserKbDocsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("UserId must not be empty.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be >= 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x.SortBy)
            .Must(s => s is null || AllowedSortKeys.Contains(s, StringComparer.Ordinal))
            .WithMessage($"SortBy must be one of: {string.Join(", ", AllowedSortKeys)} (or null for default).");

        RuleFor(x => x.State)
            .Must(s => s is null || AllowedStateKeys.Contains(s, StringComparer.Ordinal))
            .WithMessage($"State must be one of: {string.Join(", ", AllowedStateKeys)} (or null for default).");
    }
}
