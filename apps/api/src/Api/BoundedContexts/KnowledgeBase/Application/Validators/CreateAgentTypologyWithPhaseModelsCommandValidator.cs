using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for CreateAgentTypologyWithPhaseModelsCommand.
/// Supports all RAG strategies: FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM.
/// Issue #3245: Admin Phase-Model Configuration API.
/// </summary>
internal sealed class CreateAgentTypologyWithPhaseModelsCommandValidator
    : AbstractValidator<CreateAgentTypologyWithPhaseModelsCommand>
{
    private static readonly string[] ValidStrategies =
    {
        "FAST",      // Simple, fast queries
        "BALANCED",  // Moderate complexity with CRAG
        "PRECISE",   // Multi-agent with validation
        "EXPERT",    // Web search + multi-hop
        "CONSENSUS", // Multiple LLMs vote
        "CUSTOM"     // Admin-defined
    };

    public CreateAgentTypologyWithPhaseModelsCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters");

        RuleFor(x => x.BasePrompt)
            .NotEmpty().WithMessage("Base prompt is required")
            .MaximumLength(5000).WithMessage("Base prompt cannot exceed 5000 characters");

        RuleFor(x => x.Strategy)
            .NotEmpty().WithMessage("Strategy is required")
            .Must(s => ValidStrategies.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Strategy must be one of: {string.Join(", ", ValidStrategies)}");

        RuleFor(x => x.PhaseModels)
            .NotNull().WithMessage("PhaseModels configuration is required")
            .SetValidator(new StrategyPhaseModelsValidator());

        RuleFor(x => x.CreatedBy)
            .NotEmpty().WithMessage("CreatedBy is required");

        // Strategy-specific validation
        RuleFor(x => x)
            .Must(HaveRequiredPhasesForStrategy)
            .WithMessage(x => $"PhaseModels must include required phases for {x.Strategy} strategy");

        // StrategyOptions validation for advanced strategies
        When(x => x.Strategy?.ToUpperInvariant() is "EXPERT" or "CONSENSUS" or "CUSTOM", () =>
        {
            RuleFor(x => x.StrategyOptions)
                .SetValidator(new StrategyOptionsValidator()!);
        });
    }

    private static bool HaveRequiredPhasesForStrategy(CreateAgentTypologyWithPhaseModelsCommand cmd)
    {
        var strategy = cmd.Strategy?.ToUpperInvariant();
        var phases = cmd.PhaseModels;

        if (phases == null) return false;

        return strategy switch
        {
            // FAST: Only needs synthesis
            "FAST" => phases.Synthesis != null,

            // BALANCED: Needs synthesis + CRAG evaluation
            "BALANCED" => phases.Synthesis != null && phases.CragEvaluation != null,

            // PRECISE: Full multi-agent pipeline
            "PRECISE" => phases.Retrieval != null &&
                         phases.Analysis != null &&
                         phases.Synthesis != null &&
                         phases.Validation != null,

            // EXPERT: Web search + multi-hop + synthesis
            "EXPERT" => phases.WebSearch != null &&
                        phases.MultiHop != null &&
                        phases.Synthesis != null,

            // CONSENSUS: 3 voters + aggregator
            "CONSENSUS" => phases.ConsensusVoter1 != null &&
                           phases.ConsensusVoter2 != null &&
                           phases.ConsensusVoter3 != null &&
                           phases.ConsensusAggregator != null,

            // CUSTOM: At least synthesis is required
            "CUSTOM" => phases.Synthesis != null,

            _ => false
        };
    }

    private sealed class StrategyPhaseModelsValidator : AbstractValidator<StrategyPhaseModelsDto>
    {
        public StrategyPhaseModelsValidator()
        {
            // Standard phases
            When(x => x.Retrieval != null, () =>
                RuleFor(x => x.Retrieval!).SetValidator(CreatePhaseModelConfigValidator("Retrieval")));

            When(x => x.Analysis != null, () =>
                RuleFor(x => x.Analysis!).SetValidator(CreatePhaseModelConfigValidator("Analysis")));

            When(x => x.Synthesis != null, () =>
                RuleFor(x => x.Synthesis!).SetValidator(CreatePhaseModelConfigValidator("Synthesis")));

            When(x => x.Validation != null, () =>
                RuleFor(x => x.Validation!).SetValidator(CreatePhaseModelConfigValidator("Validation")));

            When(x => x.CragEvaluation != null, () =>
                RuleFor(x => x.CragEvaluation!).SetValidator(CreatePhaseModelConfigValidator("CragEvaluation")));

            When(x => x.SelfReflection != null, () =>
                RuleFor(x => x.SelfReflection!).SetValidator(CreatePhaseModelConfigValidator("SelfReflection")));

            // EXPERT phases
            When(x => x.WebSearch != null, () =>
                RuleFor(x => x.WebSearch!).SetValidator(CreatePhaseModelConfigValidator("WebSearch")));

            When(x => x.MultiHop != null, () =>
                RuleFor(x => x.MultiHop!).SetValidator(CreatePhaseModelConfigValidator("MultiHop")));

            // CONSENSUS phases
            When(x => x.ConsensusVoter1 != null, () =>
                RuleFor(x => x.ConsensusVoter1!).SetValidator(CreatePhaseModelConfigValidator("ConsensusVoter1")));

            When(x => x.ConsensusVoter2 != null, () =>
                RuleFor(x => x.ConsensusVoter2!).SetValidator(CreatePhaseModelConfigValidator("ConsensusVoter2")));

            When(x => x.ConsensusVoter3 != null, () =>
                RuleFor(x => x.ConsensusVoter3!).SetValidator(CreatePhaseModelConfigValidator("ConsensusVoter3")));

            When(x => x.ConsensusAggregator != null, () =>
                RuleFor(x => x.ConsensusAggregator!).SetValidator(CreatePhaseModelConfigValidator("ConsensusAggregator")));
        }

        /// <summary>
        /// Creates an inline validator for PhaseModelConfigurationDto.
        /// Uses InlineValidator pattern to avoid DI registration issues with constructor parameters.
        /// </summary>
        private static InlineValidator<PhaseModelConfigurationDto> CreatePhaseModelConfigValidator(string phaseName)
        {
            var validator = new InlineValidator<PhaseModelConfigurationDto>();

            validator.RuleFor(x => x.Model)
                .NotEmpty().WithMessage($"{phaseName}.Model is required")
                .MaximumLength(200).WithMessage($"{phaseName}.Model cannot exceed 200 characters");

            validator.RuleFor(x => x.MaxTokens)
                .InclusiveBetween(50, 32000)
                .WithMessage($"{phaseName}.MaxTokens must be between 50 and 32000");

            validator.RuleFor(x => x.Temperature)
                .InclusiveBetween(0.0m, 2.0m)
                .WithMessage($"{phaseName}.Temperature must be between 0.0 and 2.0");

            return validator;
        }
    }

    private sealed class StrategyOptionsValidator : AbstractValidator<StrategyOptionsDto?>
    {
        public StrategyOptionsValidator()
        {
            When(x => x != null, () =>
            {
                RuleFor(x => x!.MaxHops)
                    .InclusiveBetween(1, 5)
                    .WithMessage("MaxHops must be between 1 and 5");

                RuleFor(x => x!.ConsensusThreshold)
                    .InclusiveBetween(0.5m, 1.0m)
                    .WithMessage("ConsensusThreshold must be between 0.5 and 1.0");
            });
        }
    }
}
