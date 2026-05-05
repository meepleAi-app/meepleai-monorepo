using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;

/// <summary>
/// Validator for <see cref="DeactivateMechanicGoldenClaimCommand"/>. The command carries a single
/// identifier so the only invariant to enforce is that <see cref="DeactivateMechanicGoldenClaimCommand.ClaimId"/>
/// is non-empty; deeper state invariants (already-deactivated, not-found) are resolved by the
/// handler against the aggregate.
/// </summary>
internal sealed class DeactivateMechanicGoldenClaimValidator : AbstractValidator<DeactivateMechanicGoldenClaimCommand>
{
    public DeactivateMechanicGoldenClaimValidator()
    {
        RuleFor(x => x.ClaimId)
            .NotEmpty().WithMessage("ClaimId is required");
    }
}
