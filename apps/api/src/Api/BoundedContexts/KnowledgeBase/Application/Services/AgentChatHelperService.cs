using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Stateless helper service for the agent chat pipeline.
/// Provides RAG context formatting, model fallback selection, typology
/// resolution, game name lookup, and strategy tier mapping.
///
/// Extracted from SendAgentMessageCommandHandler to keep the handler thin
/// and to enable reuse across agent chat flows.
/// </summary>
internal sealed class AgentChatHelperService : IAgentChatHelperService
{
    private readonly IModelConfigurationService _modelConfigService;
    private readonly MeepleAiDbContext _dbContext;

    public AgentChatHelperService(
        IModelConfigurationService modelConfigService,
        MeepleAiDbContext dbContext)
    {
        _modelConfigService = modelConfigService ?? throw new ArgumentNullException(nameof(modelConfigService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public string BuildContextFromChunks(IReadOnlyList<SearchResultItem> chunks)
    {
        if (chunks.Count == 0)
            return string.Empty;

        var contextParts = chunks.Select((chunk, index) =>
            $"[{index + 1}] (Score: {chunk.Score:F2}, Page: {chunk.Page})\n{chunk.Text}");

        return string.Join("\n\n---\n\n", contextParts);
    }

    /// <inheritdoc />
    public string? GetFallbackModel(string failedModel)
    {
        var modelConfig = _modelConfigService.GetModelById(failedModel);

        // Unknown model -> Ollama fallback (safe default)
        if (modelConfig == null)
            return AgentDefaults.OllamaFallbackModel;

        // Free tier -> always fall back to Ollama (zero cost, no exceptions)
        if (modelConfig.Tier == ModelTier.Free)
            return AgentDefaults.OllamaFallbackModel;

        // Paid tiers -> find another model in the exact same tier (different provider preferred)
        var sameTierModels = _modelConfigService.GetAllModels()
            .Where(m => m.Tier == modelConfig.Tier
                      && !string.Equals(m.Id, failedModel, StringComparison.Ordinal)
                      && !string.Equals(m.Provider, "ollama", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Prefer different provider to avoid same upstream outage
        var differentProvider = sameTierModels
            .FirstOrDefault(m => !string.Equals(m.Provider, modelConfig.Provider, StringComparison.OrdinalIgnoreCase));

        if (differentProvider != null)
            return differentProvider.Id;

        // Same provider, different model
        if (sameTierModels.Count > 0)
            return sameTierModels[0].Id;

        // No same-tier alternative -> fall back to Ollama
        return AgentDefaults.OllamaFallbackModel;
    }

    /// <inheritdoc />
    public string? ResolveTypologyName(Agent agent)
    {
        return agent.Type.Value switch
        {
            "RAG" => "Tutor",
            "RulesInterpreter" => "Arbitro",
            "Confidence" => "Stratega",   // Legacy "Decisore" backend type
            "Strategist" => "Stratega",   // New backend type
            "Narrator" => "Narratore",    // New backend type
            _ => null                     // Custom / unknown -> Custom profile
        };
    }

    /// <inheritdoc />
    public async Task<string?> ResolveGameNameAsync(Agent agent, CancellationToken cancellationToken)
    {
        if (!agent.GameId.HasValue || agent.GameId == Guid.Empty)
            return null;

        return await _dbContext.Games
            .Where(g => g.Id == agent.GameId.Value)
            .Select(g => g.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public string ResolveStrategyTier(string modelId)
    {
        var modelConfig = _modelConfigService.GetModelById(modelId);
        if (modelConfig == null)
            return "Fast"; // Unknown model → conservative label

        return modelConfig.Tier switch
        {
            ModelTier.Free => "Fast",
            ModelTier.Normal => "Balanced",
            ModelTier.Premium => "Premium",
            ModelTier.Custom => "HybridRAG",
            _ => "Balanced"
        };
    }
}
