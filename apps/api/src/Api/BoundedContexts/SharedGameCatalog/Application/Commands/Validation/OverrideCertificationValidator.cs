using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Validator for <see cref="OverrideCertificationCommand"/>. Enforces surface invariants on the
/// target id, justification length, and actor id (ADR-051 Sprint 1 / Task 24).
/// </summary>
/// <remarks>
/// Aggregate state checks (prior metrics exist, not already certified) are deferred to the
/// handler because they require repository access. The 20..500-char length is also re-asserted
/// by <c>MechanicAnalysis.CertifyViaOverride</c> as defense-in-depth.
/// </remarks>
internal sealed class OverrideCertificationValidator
    : AbstractValidator<OverrideCertificationCommand>
{
    public OverrideCertificationValidator()
    {
        RuleFor(x => x.MechanicAnalysisId)
            .NotEmpty().WithMessage("MechanicAnalysisId is required");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required")
            .Length(20, 500).WithMessage("Reason must be between 20 and 500 characters");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");
    }
}
