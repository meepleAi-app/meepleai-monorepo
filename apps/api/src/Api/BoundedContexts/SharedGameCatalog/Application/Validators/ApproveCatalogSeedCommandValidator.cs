using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Validator for <see cref="ApproveCatalogSeedCommand"/>.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §6.
/// Issue #1903 — M4.4.
/// </summary>
internal sealed class ApproveCatalogSeedCommandValidator : AbstractValidator<ApproveCatalogSeedCommand>
{
    public ApproveCatalogSeedCommandValidator()
    {
        RuleFor(x => x.DraftId)
            .NotEqual(Guid.Empty)
            .WithMessage("DraftId must not be empty.");

        RuleFor(x => x.ApprovedByUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("ApprovedByUserId must not be empty.");
    }
}
