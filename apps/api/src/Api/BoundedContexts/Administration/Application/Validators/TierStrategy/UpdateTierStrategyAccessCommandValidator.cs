using Api.BoundedContexts.Administration.Application.Commands.TierStrategy;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators.TierStrategy;

/// <summary>
/// Validator for UpdateTierStrategyAccessCommand.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
internal class UpdateTierStrategyAccessCommandValidator : AbstractValidator<UpdateTierStrategyAccessCommand>
{
    public UpdateTierStrategyAccessCommandValidator()
    {
        RuleFor(x => x.Tier)
            .NotEmpty()
            .WithMessage("Tier is required")
            .Must(tier => Enum.TryParse<LlmUserTier>(tier, ignoreCase: true, out _))
            .WithMessage(x => $"Invalid tier: {x.Tier}. Valid tiers are: {string.Join(", ", Enum.GetNames<LlmUserTier>())}");

        RuleFor(x => x.Strategy)
            .NotEmpty()
            .WithMessage("Strategy is required")
            .Must(strategy => RagStrategyExtensions.TryParse(strategy, out _))
            .WithMessage("Invalid strategy. Valid strategies are: FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM");
    }
}
