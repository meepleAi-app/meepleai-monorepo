using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// FluentValidation rules for <see cref="UpdateMechanicClaimStructureCommand"/>. Graph invariants
/// (same-analysis targets, no cycles) are enforced by the aggregate and surface as 400 from the handler.
/// </summary>
internal sealed class UpdateMechanicClaimStructureCommandValidator
    : AbstractValidator<UpdateMechanicClaimStructureCommand>
{
    public UpdateMechanicClaimStructureCommandValidator()
    {
        RuleFor(c => c.AnalysisId)
            .NotEmpty().WithMessage("AnalysisId is required.");

        RuleFor(c => c.ClaimId)
            .NotEmpty().WithMessage("ClaimId is required.");

        RuleFor(c => c.ReviewerId)
            .NotEmpty().WithMessage("ReviewerId is required.");

        RuleFor(c => c.Structure)
            .NotNull().WithMessage("Structure is required.");

        RuleFor(c => c.Structure.Kind)
            .IsInEnum().WithMessage("Kind is not a valid value.")
            .When(c => c.Structure is not null);

        RuleFor(c => c.Structure.Priority)
            .IsInEnum().WithMessage("Priority is not a valid value.")
            .When(c => c.Structure is not null);

        RuleFor(c => c.Structure.Overrides)
            .Must(o => o is null || o.Distinct().Count() == o.Count)
            .WithMessage("Overrides must not contain duplicates.")
            .When(c => c.Structure is not null);
    }
}
