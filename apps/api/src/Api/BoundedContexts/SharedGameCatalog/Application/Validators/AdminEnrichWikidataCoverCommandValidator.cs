using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Validator for <see cref="AdminEnrichWikidataCoverCommand"/>. Only enforces
/// the minimal shape invariants — every business outcome (qid-missing, license
/// not whitelisted, ...) is returned as part of the result DTO instead of
/// becoming an HTTP 400. Issue #1823 Wave 3 M12.
/// </summary>
internal sealed class AdminEnrichWikidataCoverCommandValidator
    : AbstractValidator<AdminEnrichWikidataCoverCommand>
{
    public AdminEnrichWikidataCoverCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEqual(Guid.Empty)
            .WithMessage("GameId must not be empty.");

        RuleFor(x => x.TriggeredByUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("TriggeredByUserId must not be empty.");
    }
}
