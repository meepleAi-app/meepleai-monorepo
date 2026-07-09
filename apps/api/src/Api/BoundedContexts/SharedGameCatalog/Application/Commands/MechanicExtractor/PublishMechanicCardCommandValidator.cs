using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// FluentValidation rules for <see cref="PublishMechanicCardCommand"/>. State-machine invariants
/// (analysis Published, not already published, active-card conflict) are enforced by the handler and
/// surface as <c>ConflictException</c> (409).
/// </summary>
internal sealed class PublishMechanicCardCommandValidator
    : AbstractValidator<PublishMechanicCardCommand>
{
    public PublishMechanicCardCommandValidator()
    {
        RuleFor(c => c.AnalysisId)
            .NotEmpty().WithMessage("AnalysisId is required.");

        RuleFor(c => c.PublisherId)
            .NotEmpty().WithMessage("PublisherId is required.");

        // Title cap is 200 (not 100) to allow expanded titles (spec AC-4).
        When(c => c.Title is not null, () =>
        {
            RuleFor(c => c.Title)
                .Must(t => t!.Trim().Length >= 5 && t.Trim().Length <= 200)
                .WithMessage("Title must be at least 5 characters and at most 200 characters.");
        });
    }
}
