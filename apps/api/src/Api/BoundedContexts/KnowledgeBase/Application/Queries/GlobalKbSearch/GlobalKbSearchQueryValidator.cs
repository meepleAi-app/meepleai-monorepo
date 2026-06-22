using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GlobalKbSearch;

/// <summary>
/// Validates <see cref="GlobalKbSearchQuery"/> before the handler runs.
/// Registered via FluentValidation pipeline behaviour (same pattern as EstimateAgentCostQueryValidator).
/// Issue #1661: cross-game KB search (Task 4).
/// Issue #1686: facet validation (DocType allowlist, Language allowlist, list-length cap).
/// </summary>
internal sealed class GlobalKbSearchQueryValidator : AbstractValidator<GlobalKbSearchQuery>
{
    /// <summary>Hard cap on results per page (EC-7 — avoids excessive cross-game load).</summary>
    internal const int HardCapLimit = 50;

    /// <summary>Maximum number of elements in the DocType facet list (D-7).</summary>
    internal const int DocTypeListCap = 10;

    /// <summary>
    /// Allowlist for the <see cref="GlobalKbSearchQuery.DocType"/> facet (D-1).
    /// Mirrors <c>PdfDocumentEntity.DocumentType</c> defaults: base, expansion, errata, homerule.
    /// Comparison is case-insensitive — the HashSet was built with <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </summary>
    internal static readonly HashSet<string> AllowedDocTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "base", "expansion", "errata", "homerule"
        };

    /// <summary>
    /// Allowlist for the <see cref="GlobalKbSearchQuery.Language"/> facet (D-2).
    /// Mirrors <c>PdfDocumentEntity.Language</c> supported ISO 639-1 codes.
    /// Comparison is case-insensitive (D-8).
    /// </summary>
    internal static readonly HashSet<string> AllowedLanguages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "en", "it", "de", "fr", "es"
        };

    public GlobalKbSearchQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query must not be empty.")
            .MaximumLength(500)
            .WithMessage("Query must not exceed 500 characters.");

        RuleFor(x => x.Limit)
            .GreaterThan(0)
            .WithMessage("Limit must be greater than 0.")
            .LessThanOrEqualTo(HardCapLimit)
            .WithMessage($"Limit must not exceed {HardCapLimit}.");

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("UserId must be a valid GUID.");

        // Issue #1686 — DocType facet validation (D-1, D-7, D-8, D-12).
        // List-level cap (D-7): when non-null, max 10 entries.
        RuleFor(x => x.DocType)
            .Must(list => list == null || list.Count <= DocTypeListCap)
            .WithMessage($"DocType must contain at most {DocTypeListCap} entries.");

        // Per-element allowlist (D-1, D-8). RuleForEach runs only when list is non-null.
        RuleForEach(x => x.DocType)
            .NotEmpty()
            .WithMessage("DocType entries must not be empty or whitespace.")
            .Must(IsAllowedDocType)
            .WithMessage(
                $"DocType must be one of: {string.Join(", ", AllowedDocTypes)}.")
            .When(x => x.DocType != null);

        // Issue #1686 — Language facet validation (D-2, D-8, D-12).
        // When non-null, must be in the allowlist (case-insensitive). Null is "no filter" (D-3).
        RuleFor(x => x.Language)
            .Must(IsAllowedLanguage)
            .WithMessage(
                $"Language must be one of: {string.Join(", ", AllowedLanguages)}.")
            .When(x => x.Language != null);
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> (after trimming) is in the
    /// <see cref="AllowedDocTypes"/> allowlist. Case-insensitive (D-8).
    /// </summary>
    private static bool IsAllowedDocType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return AllowedDocTypes.Contains(value.Trim());
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> (after trimming) is in the
    /// <see cref="AllowedLanguages"/> allowlist. Case-insensitive (D-8).
    /// Empty / whitespace returns <c>false</c> (D-12 actionable error).
    /// </summary>
    private static bool IsAllowedLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return AllowedLanguages.Contains(value.Trim());
    }
}
