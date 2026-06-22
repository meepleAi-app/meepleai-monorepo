using System.Text.RegularExpressions;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Validator for <see cref="EnqueueCatalogSeedCommand"/>.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §6.
/// Issue #1903 — M4.2.
/// </summary>
internal sealed class EnqueueCatalogSeedCommandValidator : AbstractValidator<EnqueueCatalogSeedCommand>
{
    private static readonly Regex WikidataQidRegex = new(
        "^Q[0-9]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public EnqueueCatalogSeedCommandValidator()
    {
        // At least one of the three identifying inputs must be supplied.
        RuleFor(x => x)
            .Must(x =>
                x.BggId.HasValue
                || !string.IsNullOrWhiteSpace(x.WikidataQid)
                || !string.IsNullOrWhiteSpace(x.SearchTermInput))
            .WithMessage("At least one of BggId, WikidataQid, or SearchTermInput must be provided.");

        RuleFor(x => x.BggId!.Value)
            .GreaterThan(0)
            .WithMessage("BggId must be a positive integer.")
            .When(x => x.BggId.HasValue);

        RuleFor(x => x.WikidataQid!)
            .Matches(WikidataQidRegex)
            .WithMessage("WikidataQid must match pattern '^Q[0-9]+$' (e.g. 'Q42').")
            .When(x => !string.IsNullOrWhiteSpace(x.WikidataQid));

        RuleFor(x => x.SearchTermInput!)
            .MaximumLength(255)
            .WithMessage("SearchTermInput must not exceed 255 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.SearchTermInput));

        RuleFor(x => x.CreatedByUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("CreatedByUserId must not be empty.");
    }
}
