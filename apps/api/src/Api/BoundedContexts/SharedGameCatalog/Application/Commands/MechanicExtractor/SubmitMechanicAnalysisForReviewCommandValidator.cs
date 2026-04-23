using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// FluentValidation rules for <see cref="SubmitMechanicAnalysisForReviewCommand"/>. The state
/// machine invariants (allowed source state, at-least-one-claim) are enforced by the aggregate
/// and surface as <c>ConflictException</c> (409) from the handler.
/// </summary>
internal sealed class SubmitMechanicAnalysisForReviewCommandValidator
    : AbstractValidator<SubmitMechanicAnalysisForReviewCommand>
{
    public SubmitMechanicAnalysisForReviewCommandValidator()
    {
        RuleFor(c => c.AnalysisId)
            .NotEmpty().WithMessage("AnalysisId is required.");

        RuleFor(c => c.ActorId)
            .NotEmpty().WithMessage("ActorId is required.");
    }
}
