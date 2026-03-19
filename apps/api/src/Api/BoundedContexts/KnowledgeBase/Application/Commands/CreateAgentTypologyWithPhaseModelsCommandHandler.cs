using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for CreateAgentTypologyWithPhaseModelsCommand.
/// Converts user-friendly phase-model configuration to AgentStrategy parameters.
/// Issue #3245: Admin Phase-Model Configuration API.
/// </summary>
internal sealed class CreateAgentTypologyWithPhaseModelsCommandHandler
    : IRequestHandler<CreateAgentTypologyWithPhaseModelsCommand, AgentTypologyWithCostDto>
{
    private readonly IAgentTypologyRepository _repository;
    private readonly ILogger<CreateAgentTypologyWithPhaseModelsCommandHandler> _logger;

    // Model pricing per million tokens (input/output)
    private static readonly Dictionary<string, (decimal Input, decimal Output)> ModelPricing = new(StringComparer.OrdinalIgnoreCase)
    {
        // Free models
        ["meta-llama/llama-3.3-70b-instruct:free"] = (0m, 0m),
        ["google/gemini-2.0-flash-exp:free"] = (0m, 0m),
        // Haiku
        ["claude-3-5-haiku-20241022"] = (0.25m, 1.25m),
        ["claude-3-haiku-20240307"] = (0.25m, 1.25m),
        // Sonnet
        ["claude-3-5-sonnet-20241022"] = (3m, 15m),
        ["claude-3-sonnet-20240229"] = (3m, 15m),
        // Opus
        ["claude-3-5-opus-20241022"] = (15m, 75m),
        ["claude-3-opus-20240229"] = (15m, 75m),
        // OpenAI
        ["openai/gpt-4o"] = (5m, 15m),
        ["openai/gpt-4o-mini"] = (0.15m, 0.60m),
        // DeepSeek
        ["deepseek/deepseek-chat"] = (0.14m, 0.28m),
        // Local (free)
        ["llama3:8b"] = (0m, 0m),
        ["mistral"] = (0m, 0m)
    };

    // Estimated input tokens per phase
    private static readonly Dictionary<string, int> PhaseInputTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        // Standard phases
        ["Retrieval"] = 1450,
        ["Analysis"] = 3250,
        ["Synthesis"] = 3650,
        ["Validation"] = 3150,
        ["CragEvaluation"] = 2000,
        ["SelfReflection"] = 4400,
        // EXPERT strategy phases
        ["WebSearch"] = 1800,
        ["MultiHop"] = 4500,
        // CONSENSUS strategy phases
        ["ConsensusVoter1"] = 3000,
        ["ConsensusVoter2"] = 3000,
        ["ConsensusVoter3"] = 3000,
        ["ConsensusAggregator"] = 2500
    };

    public CreateAgentTypologyWithPhaseModelsCommandHandler(
        IAgentTypologyRepository repository,
        ILogger<CreateAgentTypologyWithPhaseModelsCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AgentTypologyWithCostDto> Handle(
        CreateAgentTypologyWithPhaseModelsCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating AgentTypology with phase models: {Name}, Strategy: {Strategy}",
            request.Name,
            request.Strategy);

        // Convert phase models to AgentStrategy
        var strategy = ConvertToAgentStrategy(request.Strategy, request.PhaseModels);

        // Determine initial status
        var status = request.AutoApprove ? TypologyStatus.Approved : TypologyStatus.Draft;

        // Create the typology
        var typology = new AgentTypology(
            id: Guid.NewGuid(),
            name: request.Name,
            description: request.Description,
            basePrompt: request.BasePrompt,
            defaultStrategy: strategy,
            createdBy: request.CreatedBy,
            status: status);

        // Auto-approve if requested
        if (request.AutoApprove)
        {
            typology.Approve(request.CreatedBy);
        }

        await _repository.AddAsync(typology, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "AgentTypology created: {Id}, Name: {Name}, Status: {Status}",
            typology.Id,
            typology.Name,
            typology.Status);

        // Calculate cost estimates
        var costEstimate = CalculateCostEstimate(request.PhaseModels, request.Strategy);

        return new AgentTypologyWithCostDto(
            Id: typology.Id,
            Name: typology.Name,
            Description: typology.Description,
            BasePrompt: typology.BasePrompt,
            Strategy: request.Strategy,
            PhaseModels: request.PhaseModels,
            StrategyOptions: request.StrategyOptions,
            Status: typology.Status.ToString(),
            CreatedBy: typology.CreatedBy,
            CreatedAt: typology.CreatedAt,
            CostEstimate: costEstimate);
    }

    private static AgentStrategy ConvertToAgentStrategy(string strategyName, StrategyPhaseModelsDto phaseModels)
    {
        var models = new List<string>();
        var agentRoles = new Dictionary<string, object>(StringComparer.Ordinal);

        // Build models array and role mapping based on strategy
        var index = 0;

        if (phaseModels.Retrieval != null)
        {
            models.Add(phaseModels.Retrieval.Model);
            agentRoles["retrieval"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modelIndex"] = index,
                ["maxTokens"] = phaseModels.Retrieval.MaxTokens,
                ["temperature"] = phaseModels.Retrieval.Temperature
            };
            index++;
        }

        if (phaseModels.Analysis != null)
        {
            models.Add(phaseModels.Analysis.Model);
            agentRoles["analysis"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modelIndex"] = index,
                ["maxTokens"] = phaseModels.Analysis.MaxTokens,
                ["temperature"] = phaseModels.Analysis.Temperature
            };
            index++;
        }

        if (phaseModels.Synthesis != null)
        {
            models.Add(phaseModels.Synthesis.Model);
            agentRoles["synthesis"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modelIndex"] = index,
                ["maxTokens"] = phaseModels.Synthesis.MaxTokens,
                ["temperature"] = phaseModels.Synthesis.Temperature
            };
            index++;
        }

        if (phaseModels.Validation != null)
        {
            models.Add(phaseModels.Validation.Model);
            agentRoles["validation"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modelIndex"] = index,
                ["maxTokens"] = phaseModels.Validation.MaxTokens,
                ["temperature"] = phaseModels.Validation.Temperature
            };
            index++;
        }

        if (phaseModels.CragEvaluation != null)
        {
            models.Add(phaseModels.CragEvaluation.Model);
            agentRoles["cragEvaluation"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modelIndex"] = index,
                ["maxTokens"] = phaseModels.CragEvaluation.MaxTokens,
                ["temperature"] = phaseModels.CragEvaluation.Temperature
            };
            index++;
        }

        if (phaseModels.SelfReflection != null)
        {
            models.Add(phaseModels.SelfReflection.Model);
            agentRoles["selfReflection"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modelIndex"] = index,
                ["maxTokens"] = phaseModels.SelfReflection.MaxTokens,
                ["temperature"] = phaseModels.SelfReflection.Temperature
            };
            index++;
        }

        // EXPERT strategy phases
        if (phaseModels.WebSearch != null)
        {
            models.Add(phaseModels.WebSearch.Model);
            agentRoles["webSearch"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modelIndex"] = index,
                ["maxTokens"] = phaseModels.WebSearch.MaxTokens,
                ["temperature"] = phaseModels.WebSearch.Temperature
            };
            index++;
        }

        if (phaseModels.MultiHop != null)
        {
            models.Add(phaseModels.MultiHop.Model);
            agentRoles["multiHop"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modelIndex"] = index,
                ["maxTokens"] = phaseModels.MultiHop.MaxTokens,
                ["temperature"] = phaseModels.MultiHop.Temperature
            };
            index++;
        }

        // CONSENSUS strategy phases
        if (phaseModels.ConsensusVoter1 != null)
        {
            models.Add(phaseModels.ConsensusVoter1.Model);
            agentRoles["consensusVoter1"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modelIndex"] = index,
                ["maxTokens"] = phaseModels.ConsensusVoter1.MaxTokens,
                ["temperature"] = phaseModels.ConsensusVoter1.Temperature
            };
            index++;
        }

        if (phaseModels.ConsensusVoter2 != null)
        {
            models.Add(phaseModels.ConsensusVoter2.Model);
            agentRoles["consensusVoter2"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modelIndex"] = index,
                ["maxTokens"] = phaseModels.ConsensusVoter2.MaxTokens,
                ["temperature"] = phaseModels.ConsensusVoter2.Temperature
            };
            index++;
        }

        if (phaseModels.ConsensusVoter3 != null)
        {
            models.Add(phaseModels.ConsensusVoter3.Model);
            agentRoles["consensusVoter3"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modelIndex"] = index,
                ["maxTokens"] = phaseModels.ConsensusVoter3.MaxTokens,
                ["temperature"] = phaseModels.ConsensusVoter3.Temperature
            };
            index++;
        }

        if (phaseModels.ConsensusAggregator != null)
        {
            models.Add(phaseModels.ConsensusAggregator.Model);
            agentRoles["consensusAggregator"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modelIndex"] = index,
                ["maxTokens"] = phaseModels.ConsensusAggregator.MaxTokens,
                ["temperature"] = phaseModels.ConsensusAggregator.Temperature
            };
        }

        // Create strategy with explicit phase configuration
        var parameters = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Models"] = models.ToArray(),
            ["AgentRoles"] = agentRoles,
            ["ConsensusThreshold"] = 0.8,
            ["StrategyType"] = strategyName.ToUpperInvariant()
        };

        return AgentStrategy.Custom($"PhaseConfigured_{strategyName}", parameters);
    }

    private CostEstimateDto CalculateCostEstimate(StrategyPhaseModelsDto phaseModels, string strategy)
    {
        var costByPhase = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var totalInputTokens = 0;
        var totalOutputTokens = 0;
        decimal totalCost = 0m;

        void AddPhaseCost(string phaseName, PhaseModelConfigurationDto? config)
        {
            if (config == null) return;

            var inputTokens = PhaseInputTokens.GetValueOrDefault(phaseName, 2000);
            var outputTokens = config.MaxTokens;

            totalInputTokens += inputTokens;
            totalOutputTokens += outputTokens;

            if (ModelPricing.TryGetValue(config.Model, out var pricing))
            {
                var phaseCost = (inputTokens * pricing.Input / 1_000_000m) +
                               (outputTokens * pricing.Output / 1_000_000m);
                costByPhase[phaseName] = Math.Round(phaseCost, 6);
                totalCost += phaseCost;
            }
            else
            {
                // Unknown model, assume mid-range pricing
                var phaseCost = (inputTokens * 1m / 1_000_000m) + (outputTokens * 5m / 1_000_000m);
                costByPhase[phaseName] = Math.Round(phaseCost, 6);
                totalCost += phaseCost;
            }
        }

        // Standard phases
        AddPhaseCost("Retrieval", phaseModels.Retrieval);
        AddPhaseCost("Analysis", phaseModels.Analysis);
        AddPhaseCost("Synthesis", phaseModels.Synthesis);
        AddPhaseCost("Validation", phaseModels.Validation);
        AddPhaseCost("CragEvaluation", phaseModels.CragEvaluation);
        AddPhaseCost("SelfReflection", phaseModels.SelfReflection);

        // EXPERT strategy phases
        AddPhaseCost("WebSearch", phaseModels.WebSearch);
        AddPhaseCost("MultiHop", phaseModels.MultiHop);

        // CONSENSUS strategy phases
        AddPhaseCost("ConsensusVoter1", phaseModels.ConsensusVoter1);
        AddPhaseCost("ConsensusVoter2", phaseModels.ConsensusVoter2);
        AddPhaseCost("ConsensusVoter3", phaseModels.ConsensusVoter3);
        AddPhaseCost("ConsensusAggregator", phaseModels.ConsensusAggregator);

        return new CostEstimateDto(
            EstimatedTokensPerQuery: totalInputTokens + totalOutputTokens,
            EstimatedCostPerQuery: Math.Round(totalCost, 6),
            EstimatedMonthlyCost10K: Math.Round(totalCost * 10_000, 2),
            CostByPhase: costByPhase);
    }
}
