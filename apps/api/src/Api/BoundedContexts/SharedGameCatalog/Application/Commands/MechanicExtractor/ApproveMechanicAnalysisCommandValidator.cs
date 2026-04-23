using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// FluentValidation rules for <see cref="ApproveMechanicAnalysisCommand"/>. Domain-level
/// invariants (source state <c>InReview</c>, all claims Approved) are enforced by the aggregate
/// and surface as <c>ConflictException</c> (409) from the handler.
/// </summary>
internal sealed class ApproveMechanicAnalysisCommandValidator
    : AbstractValidator<ApproveMechanicAnalysisCommand>
{
    public ApproveMechanicAnalysisCommandValidator()
    {
        RuleFor(c => c.AnalysisId)
            .NotEmpty().WithMessage("AnalysisId is required.");

        RuleFor(c => c.ReviewerId)
            .NotEmpty().WithMessage("ReviewerId is required.");
    }
}
