using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// FluentValidation rules for <see cref="GenerateMechanicAnalysisCommand"/>. These checks cover
/// pre-conditions that are cheap to verify without hitting the DB or LLM. Cross-entity checks
/// (game exists, PDF linked to game, idempotency) are delegated to the handler where the
/// repository is available and exceptions can be mapped to proper HTTP status codes.
/// </summary>
internal sealed class GenerateMechanicAnalysisCommandValidator
    : AbstractValidator<GenerateMechanicAnalysisCommand>
{
    /// <summary>
    /// Sanity ceiling for the submitted cap. Prevents accidental typos like <c>"100.0"</c>
    /// instead of <c>"1.00"</c> from pre-authorizing catastrophic spend. Admins who genuinely
    /// need a higher cap should set <c>CostCapUsd</c> lower and supply a <c>CostCapOverride</c>
    /// to make the intent auditable.
    /// </summary>
    private const decimal MaxCostCapUsd = 10.0m;

    public GenerateMechanicAnalysisCommandValidator()
    {
        RuleFor(c => c.SharedGameId)
            .NotEmpty().WithMessage("SharedGameId is required.");

        RuleFor(c => c.PdfDocumentId)
            .NotEmpty().WithMessage("PdfDocumentId is required.");

        RuleFor(c => c.RequestedBy)
            .NotEmpty().WithMessage("RequestedBy is required.");

        RuleFor(c => c.CostCapUsd)
            .GreaterThan(0m).WithMessage("CostCapUsd must be strictly positive (ADR-051 T8).")
            .LessThanOrEqualTo(MaxCostCapUsd)
                .WithMessage($"CostCapUsd must be ≤ {MaxCostCapUsd:F2} USD. Higher caps require an explicit override with justification.");

        When(c => c.CostCapOverride is not null, () =>
        {
            RuleFor(c => c.CostCapOverride!.NewCapUsd)
                .GreaterThan(0m).WithMessage("CostCapOverride.NewCapUsd must be strictly positive.")
                .LessThanOrEqualTo(MaxCostCapUsd * 2)
                    .WithMessage($"CostCapOverride.NewCapUsd must be ≤ {MaxCostCapUsd * 2:F2} USD.");

            RuleFor(c => c.CostCapOverride!.Reason)
                .NotEmpty().WithMessage("CostCapOverride.Reason is required when overriding the cap.")
                .MinimumLength(20).WithMessage("CostCapOverride.Reason must be at least 20 characters to force deliberate justification.")
                .MaximumLength(500).WithMessage("CostCapOverride.Reason must not exceed 500 characters.");

            RuleFor(c => c)
                .Must(c => c.CostCapOverride!.NewCapUsd > c.CostCapUsd)
                    .WithMessage("CostCapOverride.NewCapUsd must be greater than the submitted CostCapUsd.")
                    .WithName("CostCapOverride.NewCapUsd");
        });
    }
}
