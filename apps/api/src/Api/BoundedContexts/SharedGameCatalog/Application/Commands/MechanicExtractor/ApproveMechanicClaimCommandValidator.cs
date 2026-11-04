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

        RuleFor(c => c.Note)
            .MaximumLength(2000).WithMessage("Note must be 2000 characters or fewer.")
            .When(c => c.Note is not null);

        RuleFor(c => c.Structure!.Kind)
            .IsInEnum().WithMessage("Kind is not a valid value.")
            .When(c => c.Structure is not null);

        RuleFor(c => c.Structure!.Priority)
            .IsInEnum().WithMessage("Priority is not a valid value.")
            .When(c => c.Structure is not null);

        RuleFor(c => c.Structure!.Overrides)
            .Must(o => o is null || o.Distinct().Count() == o.Count)
            .WithMessage("Overrides must not contain duplicates.")
            .When(c => c.Structure is not null);
    }
}
