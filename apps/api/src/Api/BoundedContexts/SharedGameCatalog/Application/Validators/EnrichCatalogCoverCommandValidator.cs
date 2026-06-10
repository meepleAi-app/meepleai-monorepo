using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Validator for <see cref="EnrichCatalogCoverCommand"/>. Only enforces the
/// minimal command-shape invariants. Business rules (QID present / license
/// whitelisted / freshness) live on the handler so they map to
/// <see cref="EnrichCatalogCoverResult.Skipped"/> outcomes rather than HTTP 400.
/// Issue #1823 Phase B M8.
/// </summary>
internal sealed class EnrichCatalogCoverCommandValidator : AbstractValidator<EnrichCatalogCoverCommand>
{
    public EnrichCatalogCoverCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEqual(Guid.Empty)
            .WithMessage("GameId must not be empty.");
    }
}
