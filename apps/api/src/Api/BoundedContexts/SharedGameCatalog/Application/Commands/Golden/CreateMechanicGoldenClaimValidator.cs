using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;

/// <summary>
/// Validator for <see cref="CreateMechanicGoldenClaimCommand"/>. Enforces invariants that mirror the
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicGoldenClaim"/> aggregate
/// so invalid input is rejected before embedding / keyword extraction cost is incurred.
/// </summary>
internal sealed class CreateMechanicGoldenClaimValidator : AbstractValidator<CreateMechanicGoldenClaimCommand>
{
    public CreateMechanicGoldenClaimValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEmpty().WithMessage("SharedGameId is required");

        RuleFor(x => x.Section)
            .IsInEnum().WithMessage("Section must be a valid MechanicSection value");

        RuleFor(x => x.Statement)
            .NotEmpty().WithMessage("Statement is required")
            .MaximumLength(500).WithMessage("Statement cannot exceed 500 characters");

        RuleFor(x => x.ExpectedPage)
            .GreaterThanOrEqualTo(1).WithMessage("ExpectedPage must be >= 1");

        RuleFor(x => x.SourceQuote)
            .NotEmpty().WithMessage("SourceQuote is required")
            .MaximumLength(1000).WithMessage("SourceQuote cannot exceed 1000 characters");

        RuleFor(x => x.CuratorUserId)
            .NotEmpty().WithMessage("CuratorUserId is required");
    }
}
