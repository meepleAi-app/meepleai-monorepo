namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Configuration for a single phase/step in a multi-agent strategy.
/// Issue #3245: Admin Phase-Model Configuration API.
/// </summary>
/// <param name="Model">LLM model identifier (e.g., "claude-3-5-haiku-20241022").</param>
/// <param name="MaxTokens">Maximum tokens for this phase's output.</param>
/// <param name="Temperature">Temperature for generation (0.0-2.0).</param>
public record PhaseModelConfigurationDto(
    string Model,
    int MaxTokens = 500,
    decimal Temperature = 0.7m
);

/// <summary>
/// Complete phase model configuration for multi-agent strategies.
/// Maps strategy phases to their LLM configurations.
/// Supports all strategy types: FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM.
/// </summary>
/// <param name="Retrieval">Retrieval phase: Analyzes query, plans retrieval. Recommended: Fast model (Haiku).</param>
/// <param name="Analysis">Analysis phase: Extracts rules from docs. Recommended: Fast model (Haiku).</param>
/// <param name="Synthesis">Synthesis phase: Creates final answer. Recommended: Premium model (Sonnet).</param>
/// <param name="Validation">Validation phase: Verifies correctness. Recommended: Fast model (Haiku).</param>
/// <param name="CragEvaluation">CRAG evaluation phase. Recommended: Fast model.</param>
/// <param name="SelfReflection">Self-reflection phase (PRECISE only). Recommended: Same as synthesis.</param>
/// <param name="WebSearch">Web search phase (EXPERT only). Recommended: Fast model for query generation.</param>
/// <param name="MultiHop">Multi-hop reasoning phase (EXPERT only). Recommended: Premium model.</param>
/// <param name="ConsensusVoter1">First voter in consensus (CONSENSUS only).</param>
/// <param name="ConsensusVoter2">Second voter in consensus (CONSENSUS only).</param>
/// <param name="ConsensusVoter3">Third voter in consensus (CONSENSUS only).</param>
/// <param name="ConsensusAggregator">Aggregator that combines votes (CONSENSUS only). Recommended: Premium model.</param>
public record StrategyPhaseModelsDto(
    PhaseModelConfigurationDto? Retrieval = null,
    PhaseModelConfigurationDto? Analysis = null,
    PhaseModelConfigurationDto? Synthesis = null,
    PhaseModelConfigurationDto? Validation = null,
    PhaseModelConfigurationDto? CragEvaluation = null,
    PhaseModelConfigurationDto? SelfReflection = null,
    // EXPERT strategy phases
    PhaseModelConfigurationDto? WebSearch = null,
    PhaseModelConfigurationDto? MultiHop = null,
    // CONSENSUS strategy phases
    PhaseModelConfigurationDto? ConsensusVoter1 = null,
    PhaseModelConfigurationDto? ConsensusVoter2 = null,
    PhaseModelConfigurationDto? ConsensusVoter3 = null,
    PhaseModelConfigurationDto? ConsensusAggregator = null
);

/// <summary>
/// Extended configuration for custom strategies.
/// </summary>
/// <param name="EnableWebSearch">Enable web search augmentation.</param>
/// <param name="EnableMultiHop">Enable multi-hop retrieval.</param>
/// <param name="MaxHops">Maximum hops for multi-hop retrieval (1-5).</param>
/// <param name="ConsensusThreshold">Minimum agreement threshold for consensus (0.0-1.0).</param>
/// <param name="EnableCitationValidation">Enable strict citation validation.</param>
/// <param name="EnableSelfReflection">Enable self-reflection loop.</param>
/// <param name="CustomParameters">Additional custom parameters as key-value pairs.</param>
public record StrategyOptionsDto(
    bool EnableWebSearch = false,
    bool EnableMultiHop = false,
    int MaxHops = 2,
    decimal ConsensusThreshold = 0.8m,
    bool EnableCitationValidation = true,
    bool EnableSelfReflection = false,
    IDictionary<string, object>? CustomParameters = null
);

/// <summary>
/// Response DTO with computed cost estimates.
/// </summary>
/// <param name="Id">Typology unique identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Description of the agent's purpose.</param>
/// <param name="BasePrompt">System prompt for the agent.</param>
/// <param name="Strategy">RAG strategy (FAST/BALANCED/PRECISE/EXPERT/CONSENSUS/CUSTOM).</param>
/// <param name="PhaseModels">Model configuration per phase.</param>
/// <param name="StrategyOptions">Extended strategy options.</param>
/// <param name="Status">Approval status.</param>
/// <param name="CreatedBy">Creator user ID.</param>
/// <param name="CreatedAt">Creation timestamp.</param>
/// <param name="CostEstimate">Estimated costs.</param>
public record AgentTypologyWithCostDto(
    Guid Id,
    string Name,
    string Description,
    string BasePrompt,
    string Strategy,
    StrategyPhaseModelsDto PhaseModels,
    StrategyOptionsDto? StrategyOptions,
    string Status,
    Guid CreatedBy,
    DateTime CreatedAt,
    CostEstimateDto CostEstimate
);

/// <summary>
/// Estimated costs per query for the configured models.
/// </summary>
/// <param name="EstimatedTokensPerQuery">Estimated total tokens per query.</param>
/// <param name="EstimatedCostPerQuery">Estimated cost in USD per query.</param>
/// <param name="EstimatedMonthlyCost10K">Estimated monthly cost for 10K queries.</param>
/// <param name="CostByPhase">Cost breakdown by phase.</param>
public record CostEstimateDto(
    int EstimatedTokensPerQuery,
    decimal EstimatedCostPerQuery,
    decimal EstimatedMonthlyCost10K,
    IReadOnlyDictionary<string, decimal> CostByPhase
);

/// <summary>
/// Summary of available RAG strategies for documentation/UI.
/// </summary>
public record RagStrategyInfoDto(
    string Name,
    string DisplayName,
    string Description,
    string[] RequiredPhases,
    string[] OptionalPhases,
    int EstimatedTokens,
    decimal EstimatedCostPerQuery,
    string RecommendedFor
);
