using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// FluentValidation rules for <see cref="RequeueMechanicAnalysisForPromptVersionCommand"/>.
/// </summary>
internal sealed class RequeueMechanicAnalysisForPromptVersionCommandValidator
    : AbstractValidator<RequeueMechanicAnalysisForPromptVersionCommand>
{
    private const decimal MaxCostCapUsd = 10.0m;

    public RequeueMechanicAnalysisForPromptVersionCommandValidator()
    {
        RuleFor(c => c.SharedGameId).NotEmpty().WithMessage("SharedGameId is required.");
        RuleFor(c => c.ActorId).NotEmpty().WithMessage("ActorId is required.");
        RuleFor(c => c.CostCapUsd)
            .GreaterThan(0m).WithMessage("CostCapUsd must be greater than zero.")
            .LessThanOrEqualTo(MaxCostCapUsd).WithMessage($"CostCapUsd must not exceed {MaxCostCapUsd} USD.");
    }
}
