using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Admin command to create an AgentTypology with explicit phase-model configuration.
/// Provides user-friendly API for configuring which LLM model to use for each strategy phase.
/// Supports strategies: FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM.
/// Issue #3245: Admin Phase-Model Configuration API.
/// </summary>
/// <param name="Name">Display name for the agent typology.</param>
/// <param name="Description">Description of the agent's purpose and capabilities.</param>
/// <param name="BasePrompt">Base system prompt that defines the agent's behavior.</param>
/// <param name="Strategy">RAG strategy: FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, or CUSTOM.</param>
/// <param name="PhaseModels">Explicit model configuration for each strategy phase.</param>
/// <param name="StrategyOptions">Extended options for EXPERT, CONSENSUS, and CUSTOM strategies.</param>
/// <param name="CreatedBy">Admin user ID creating this typology.</param>
/// <param name="AutoApprove">Whether to auto-approve (Admin only, skips Draft status).</param>
public record CreateAgentTypologyWithPhaseModelsCommand(
    string Name,
    string Description,
    string BasePrompt,
    string Strategy,
    StrategyPhaseModelsDto PhaseModels,
    StrategyOptionsDto? StrategyOptions,
    Guid CreatedBy,
    bool AutoApprove = false
) : IRequest<AgentTypologyWithCostDto>;
