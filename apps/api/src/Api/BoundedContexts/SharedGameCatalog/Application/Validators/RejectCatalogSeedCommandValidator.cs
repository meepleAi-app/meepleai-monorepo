using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Validator for <see cref="RejectCatalogSeedCommand"/>.
/// <c>Reason</c> may be empty (admin shortcut for spam/garbage); only its length
/// is capped to align with the entity's <c>ErrorMessage</c> column (500 chars).
/// Spec: 2026-06-04-admin-catalog-seed-design.md §6.
/// Issue #1903 — M4.5.
/// </summary>
internal sealed class RejectCatalogSeedCommandValidator : AbstractValidator<RejectCatalogSeedCommand>
{
    public RejectCatalogSeedCommandValidator()
    {
        RuleFor(x => x.DraftId)
            .NotEqual(Guid.Empty)
            .WithMessage("DraftId must not be empty.");

        RuleFor(x => x.RejectedByUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("RejectedByUserId must not be empty.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Reason must not exceed 500 characters.");
    }
}
