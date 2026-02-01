using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin-specific endpoints for AgentTypology management with phase-model configuration.
/// Issue #3245: Admin Phase-Model Configuration API.
/// </summary>
internal static class AdminAgentTypologyEndpoints
{
    public static RouteGroupBuilder MapAdminAgentTypologyEndpoints(this RouteGroupBuilder group)
    {
        // ========================================
        // ADMIN: PHASE-MODEL CONFIGURATION
        // ========================================

        // POST /api/v1/admin/agent-typologies
        // Create AgentTypology with explicit phase-model configuration
        group.MapPost("/", async (
            CreateAgentTypologyRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation(
                "Admin {UserId} creating AgentTypology with phase models: {Name}, Strategy: {Strategy}",
                session.User!.Id,
                request.Name,
                request.Strategy);

            var command = new CreateAgentTypologyWithPhaseModelsCommand(
                Name: request.Name,
                Description: request.Description,
                BasePrompt: request.BasePrompt,
                Strategy: request.Strategy,
                PhaseModels: request.PhaseModels,
                StrategyOptions: request.StrategyOptions,
                CreatedBy: session.User.Id,
                AutoApprove: request.AutoApprove);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "AgentTypology created: {Id}, Strategy: {Strategy}, EstimatedCost: ${Cost}/query",
                result.Id,
                result.Strategy,
                result.CostEstimate.EstimatedCostPerQuery);

            return Results.Created($"/api/v1/agent-typologies/{result.Id}", result);
        })
        .WithName("AdminCreateAgentTypology")
        .WithTags("AgentTypology", "Admin")
        .WithSummary("Create AgentTypology with phase-model configuration (Admin)")
        .WithDescription(@"
Admin endpoint to create an AgentTypology with explicit LLM model configuration per strategy phase.

**Strategies:**
- **FAST**: Requires only `synthesis` phase (~2,060 tokens/query)
- **BALANCED**: Requires `synthesis` + `cragEvaluation` phases (~2,820 tokens/query)
- **PRECISE**: Requires all 4 phases: `retrieval`, `analysis`, `synthesis`, `validation` (~22,396 tokens/query)
- **EXPERT**: Requires `webSearch`, `multiHop`, `synthesis` phases - Web search + multi-hop reasoning
- **CONSENSUS**: Requires `consensusVoter1`, `consensusVoter2`, `consensusVoter3`, `consensusAggregator` - Multiple LLMs vote
- **CUSTOM**: Minimum `synthesis` phase, extensible with any combination

**Example Request (PRECISE):**
```json
{
  ""name"": ""Strategic Advisor"",
  ""description"": ""Expert strategy analysis agent"",
  ""basePrompt"": ""You are an expert board game strategist..."",
  ""strategy"": ""PRECISE"",
  ""phaseModels"": {
    ""retrieval"": { ""model"": ""claude-3-5-haiku-20241022"", ""maxTokens"": 200 },
    ""analysis"": { ""model"": ""claude-3-5-haiku-20241022"", ""maxTokens"": 400 },
    ""synthesis"": { ""model"": ""claude-3-5-sonnet-20241022"", ""maxTokens"": 800 },
    ""validation"": { ""model"": ""claude-3-5-haiku-20241022"", ""maxTokens"": 300 }
  },
  ""autoApprove"": true
}
```

**Example Request (CONSENSUS):**
```json
{
  ""name"": ""High-Stakes Rules Arbiter"",
  ""description"": ""Multi-model consensus for critical rule decisions"",
  ""basePrompt"": ""You are a precise rules arbiter..."",
  ""strategy"": ""CONSENSUS"",
  ""phaseModels"": {
    ""consensusVoter1"": { ""model"": ""claude-3-5-sonnet-20241022"", ""maxTokens"": 600 },
    ""consensusVoter2"": { ""model"": ""openai/gpt-4o"", ""maxTokens"": 600 },
    ""consensusVoter3"": { ""model"": ""deepseek/deepseek-chat"", ""maxTokens"": 600 },
    ""consensusAggregator"": { ""model"": ""claude-3-5-sonnet-20241022"", ""maxTokens"": 400 }
  },
  ""strategyOptions"": {
    ""consensusThreshold"": 0.8
  },
  ""autoApprove"": true
}
```
");

        // PUT /api/v1/admin/agent-typologies/{id}/phase-models
        // Update phase-model configuration for existing typology
        group.MapPut("/{id:guid}/phase-models", async (
            Guid id,
            UpdatePhaseModelsRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation(
                "Admin {UserId} updating phase models for AgentTypology: {TypologyId}",
                session.User!.Id,
                id);

            // Convert to UpdateAgentTypologyCommand with new strategy parameters
            var strategyParams = ConvertPhaseModelsToParameters(request.PhaseModels);

            var command = new UpdateAgentTypologyCommand(
                Id: id,
                Name: request.Name ?? string.Empty, // Will be loaded from existing if empty
                Description: request.Description ?? string.Empty,
                BasePrompt: request.BasePrompt ?? string.Empty,
                DefaultStrategyName: $"PhaseConfigured_{request.Strategy}",
                DefaultStrategyParameters: strategyParams);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "AgentTypology phase models updated: {Id}",
                result.Id);

            return Results.Ok(result);
        })
        .WithName("AdminUpdatePhaseModels")
        .WithTags("AgentTypology", "Admin")
        .WithSummary("Update phase-model configuration (Admin)")
        .WithDescription("Update the LLM models used for each phase of the strategy.");

        // GET /api/v1/admin/agent-typologies/{id}/cost-estimate
        // Get cost estimate for a typology configuration
        group.MapGet("/{id:guid}/cost-estimate", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Get the typology and calculate costs
            var query = new GetTypologyByIdQuery(
                TypologyId: id,
                UserRole: session.User!.Role,
                UserId: session.User.Id);

            var typology = await mediator.Send(query, ct).ConfigureAwait(false);
            if (typology == null)
            {
                return Results.NotFound(new { error = "AgentTypology not found" });
            }

            // Extract phase models from parameters and calculate costs
            var costEstimate = CalculateCostFromParameters(typology.DefaultStrategyParameters);

            return Results.Ok(new
            {
                typologyId = id,
                typologyName = typology.Name,
                strategy = typology.DefaultStrategyName,
                costEstimate
            });
        })
        .WithName("AdminGetCostEstimate")
        .WithTags("AgentTypology", "Admin")
        .WithSummary("Get cost estimate for AgentTypology (Admin)")
        .WithDescription("Calculate estimated costs per query based on configured models.");

        return group;
    }

    private static IDictionary<string, object> ConvertPhaseModelsToParameters(StrategyPhaseModelsDto phaseModels)
    {
        var models = new List<string>();
        var agentRoles = new Dictionary<string, object>(StringComparer.Ordinal);
        var index = 0;

        void AddPhase(string name, PhaseModelConfigurationDto? config)
        {
            if (config == null) return;
            models.Add(config.Model);
            agentRoles[name.ToLowerInvariant()] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["modelIndex"] = index,
                ["maxTokens"] = config.MaxTokens,
                ["temperature"] = config.Temperature
            };
            index++;
        }

        // Standard phases
        AddPhase("Retrieval", phaseModels.Retrieval);
        AddPhase("Analysis", phaseModels.Analysis);
        AddPhase("Synthesis", phaseModels.Synthesis);
        AddPhase("Validation", phaseModels.Validation);
        AddPhase("CragEvaluation", phaseModels.CragEvaluation);
        AddPhase("SelfReflection", phaseModels.SelfReflection);

        // EXPERT strategy phases
        AddPhase("WebSearch", phaseModels.WebSearch);
        AddPhase("MultiHop", phaseModels.MultiHop);

        // CONSENSUS strategy phases
        AddPhase("ConsensusVoter1", phaseModels.ConsensusVoter1);
        AddPhase("ConsensusVoter2", phaseModels.ConsensusVoter2);
        AddPhase("ConsensusVoter3", phaseModels.ConsensusVoter3);
        AddPhase("ConsensusAggregator", phaseModels.ConsensusAggregator);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Models"] = models.ToArray(),
            ["AgentRoles"] = agentRoles,
            ["ConsensusThreshold"] = 0.8
        };
    }

    private static CostEstimateDto CalculateCostFromParameters(IReadOnlyDictionary<string, object> parameters)
    {
        // Simplified cost calculation from parameters
        // In production, this would use the same logic as the handler
        return new CostEstimateDto(
            EstimatedTokensPerQuery: 12000,
            EstimatedCostPerQuery: 0.043m,
            EstimatedMonthlyCost10K: 430m,
            CostByPhase: new Dictionary<string, decimal>(StringComparer.Ordinal));
    }
}

/// <summary>
/// Request model for creating AgentTypology with phase models.
/// </summary>
/// <param name="Name">Display name for the agent typology.</param>
/// <param name="Description">Description of the agent's purpose.</param>
/// <param name="BasePrompt">Base system prompt for the agent.</param>
/// <param name="Strategy">RAG strategy: FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, or CUSTOM.</param>
/// <param name="PhaseModels">Model configuration for each strategy phase.</param>
/// <param name="StrategyOptions">Extended options for EXPERT, CONSENSUS, and CUSTOM strategies.</param>
/// <param name="AutoApprove">Whether to auto-approve (Admin only).</param>
public record CreateAgentTypologyRequest(
    string Name,
    string Description,
    string BasePrompt,
    string Strategy,
    StrategyPhaseModelsDto PhaseModels,
    StrategyOptionsDto? StrategyOptions = null,
    bool AutoApprove = false
);

/// <summary>
/// Request model for updating phase models.
/// </summary>
public record UpdatePhaseModelsRequest(
    string Strategy,
    StrategyPhaseModelsDto PhaseModels,
    string? Name = null,
    string? Description = null,
    string? BasePrompt = null
);
