using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// FluentValidation rules for <see cref="BulkApproveMechanicClaimsCommand"/>.
/// </summary>
internal sealed class BulkApproveMechanicClaimsCommandValidator
    : AbstractValidator<BulkApproveMechanicClaimsCommand>
{
    public BulkApproveMechanicClaimsCommandValidator()
    {
        RuleFor(c => c.AnalysisId)
            .NotEmpty().WithMessage("AnalysisId is required.");

        RuleFor(c => c.ReviewerId)
            .NotEmpty().WithMessage("ReviewerId is required.");
    }
}
