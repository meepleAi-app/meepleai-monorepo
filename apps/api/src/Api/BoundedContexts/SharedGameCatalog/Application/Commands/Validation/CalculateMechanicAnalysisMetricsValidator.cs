using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Validator for <see cref="CalculateMechanicAnalysisMetricsCommand"/>. Enforces the surface
/// invariant that the target analysis id is a real (non-empty) <see cref="Guid"/>.
/// </summary>
/// <remarks>
/// Existence of the aggregate and its lifecycle state are enforced by the handler — those
/// checks require repository access and are not appropriate for a FluentValidation rule.
/// </remarks>
internal sealed class CalculateMechanicAnalysisMetricsValidator
    : AbstractValidator<CalculateMechanicAnalysisMetricsCommand>
{
    public CalculateMechanicAnalysisMetricsValidator()
    {
        RuleFor(x => x.MechanicAnalysisId)
            .NotEmpty().WithMessage("MechanicAnalysisId is required");
    }
}
