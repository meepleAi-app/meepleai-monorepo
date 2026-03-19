using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

internal sealed partial class SendAgentMessageCommandHandler
{
    /// <summary>
    /// Builds formatted context string from retrieved chunks for LLM prompt.
    /// Pattern from AskAgentQuestionCommandHandler.
    /// </summary>
    private static string BuildContextFromChunks(List<SearchResultItem> chunks)
    {
        if (chunks.Count == 0)
            return string.Empty;

        var contextParts = chunks.Select((chunk, index) =>
            $"[{index + 1}] (Score: {chunk.Score:F2}, Page: {chunk.Page})\n{chunk.Text}");

        return string.Join("\n\n---\n\n", contextParts);
    }

    /// <summary>
    /// Selects a fallback model based on the failed model's tier.
    /// Free tier -> Ollama local (zero cost).
    /// Paid tiers -> another model in the same tier.
    /// </summary>
    private string? GetFallbackModel(string failedModel)
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

    /// <summary>
    /// Resolves the typology display name from agent type.
    /// Maps backend AgentType values to frontend typology names.
    /// </summary>
    private static string? ResolveTypologyName(Agent agent)
    {
        return agent.Type.Value switch
        {
            "RAG" => "Tutor",
            "RulesInterpreter" => "Arbitro",
            "Confidence" => "Stratega",      // Legacy "Decisore" backend type
            "Strategist" => "Stratega",       // New backend type
            "Narrator" => "Narratore",        // New backend type
            _ => null                         // Custom / unknown -> Custom profile
        };
    }

    private async Task<string?> ResolveGameNameAsync(Agent agent, CancellationToken ct)
    {
        if (!agent.GameId.HasValue || agent.GameId == Guid.Empty) return null;
        var gameName = await _dbContext.Games
            .Where(g => g.Id == agent.GameId.Value)
            .Select(g => g.Name)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return gameName;
    }

    /// <summary>
    /// Maps the model tier to a user-facing strategy tier label.
    /// Issue #5481: ResponseMetaBadge — soft quality badge on AI responses.
    /// </summary>
    private string ResolveStrategyTier(string modelId)
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

    private static RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, DateTime.UtcNow);
    }
}
