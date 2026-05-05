using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// FluentValidation rules for <see cref="ApproveMechanicClaimCommand"/>. Domain invariants
/// (parent analysis must be <c>InReview</c>, claim must belong to the aggregate) are enforced
/// by the aggregate and surface as 409 / 404 from the handler.
/// </summary>
internal sealed class ApproveMechanicClaimCommandValidator
    : AbstractValidator<ApproveMechanicClaimCommand>
{
    public ApproveMechanicClaimCommandValidator()
    {
        RuleFor(c => c.AnalysisId)
            .NotEmpty().WithMessage("AnalysisId is required.");

        RuleFor(c => c.ClaimId)
            .NotEmpty().WithMessage("ClaimId is required.");

        RuleFor(c => c.ReviewerId)
            .NotEmpty().WithMessage("ReviewerId is required.");
    }
}
