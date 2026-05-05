using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Validator for <see cref="UpdateCertificationThresholdsCommand"/>. Enforces surface invariants
/// matching the <c>CertificationThresholds.Create</c> value-object factory bounds
/// (ADR-051 Sprint 1 / Task 26).
/// </summary>
/// <remarks>
/// The aggregate / value-object factory re-validates the same bounds as defense-in-depth, but
/// rejecting at the surface keeps endpoint responses as 400 ValidationProblem rather than 500.
/// </remarks>
internal sealed class UpdateCertificationThresholdsValidator
    : AbstractValidator<UpdateCertificationThresholdsCommand>
{
    public UpdateCertificationThresholdsValidator()
    {
        RuleFor(x => x.MinCoveragePct)
            .InclusiveBetween(0m, 100m).WithMessage("MinCoveragePct must be between 0 and 100");

        RuleFor(x => x.MaxPageTolerance)
            .GreaterThanOrEqualTo(0).WithMessage("MaxPageTolerance must be >= 0");

        RuleFor(x => x.MinBggMatchPct)
            .InclusiveBetween(0m, 100m).WithMessage("MinBggMatchPct must be between 0 and 100");

        RuleFor(x => x.MinOverallScore)
            .InclusiveBetween(0m, 100m).WithMessage("MinOverallScore must be between 0 and 100");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");
    }
}
