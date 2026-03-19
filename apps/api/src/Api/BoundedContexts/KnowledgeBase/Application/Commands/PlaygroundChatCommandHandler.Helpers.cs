using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.Models;
using Api.Services.LlmClients;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

internal sealed partial class PlaygroundChatCommandHandler
{
    /// <summary>
    /// Builds a cache key from gameId and message content using SHA256 hash.
    /// Issue #4443: Cache observability.
    /// </summary>
    private static string BuildCacheKey(Guid gameId, string message)
    {
        var input = $"{gameId}:{message.Trim().ToLowerInvariant()}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Lazily evicts expired entries from the in-memory playground cache.
    /// </summary>
    private static void EvictExpiredCacheEntries()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in QueryCache)
        {
            if (kvp.Value.CachedAt.Add(CacheTtl) < now)
            {
                QueryCache.TryRemove(kvp.Key, out _);
            }
        }
    }

    /// <summary>
    /// Categorizes a strategy name into a broad type for display.
    /// </summary>
    private static string CategorizeStrategy(string strategyName)
    {
        return strategyName switch
        {
            "RetrievalOnly" or "HybridSearch" or "VectorOnly" => "retrieval",
            "SingleModel" => "generation",
            "MultiModelConsensus" => "consensus",
            "CitationValidation" or "ConfidenceScoring" => "validation",
            _ => "custom"
        };
    }

    /// <summary>
    /// Resolves the effective strategy name from command override or defaults to SingleModel.
    /// </summary>
    private static string ResolveStrategy(string? strategyOverride)
    {
        if (string.IsNullOrWhiteSpace(strategyOverride))
            return "SingleModel";

        return strategyOverride switch
        {
            "RetrievalOnly" or "SingleModel" or "MultiModelConsensus" => strategyOverride,
            _ => "SingleModel" // Unknown strategies fall back to default
        };
    }

    private static string BuildSystemPrompt(
        Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition agentDefinition)
    {
        // Look for a "system" role prompt in the AgentDefinition's prompts
        var systemPromptTemplate = agentDefinition.Prompts
            .FirstOrDefault(p => string.Equals(p.Role, "system", StringComparison.OrdinalIgnoreCase));

        var basePrompt = systemPromptTemplate != null && !string.IsNullOrWhiteSpace(systemPromptTemplate.Content)
            ? systemPromptTemplate.Content
            : $"You are {agentDefinition.Name}. {agentDefinition.Description} " +
              "Answer questions about board games clearly and helpfully.";

        // E5-3: Inject ChatLanguage instruction when not "auto"
        var languageInstruction = GetChatLanguageInstruction(agentDefinition.ChatLanguage);
        if (languageInstruction != null)
        {
            return languageInstruction + "\n\n" + basePrompt;
        }

        return basePrompt;
    }

    /// <summary>
    /// Language names mapping for ChatLanguage system prompt injection (E5-3).
    /// </summary>
    private static readonly Dictionary<string, string> LanguageNames = new(StringComparer.Ordinal)
    {
        ["it"] = "Italian",
        ["en"] = "English",
        ["de"] = "German",
        ["fr"] = "French",
        ["es"] = "Spanish",
        ["pt"] = "Portuguese",
        ["ja"] = "Japanese",
        ["zh"] = "Chinese",
        ["ko"] = "Korean",
        ["ru"] = "Russian",
        ["pl"] = "Polish",
        ["nl"] = "Dutch"
    };

    /// <summary>
    /// Returns a language instruction for the system prompt, or null if ChatLanguage is "auto".
    /// </summary>
    internal static string? GetChatLanguageInstruction(string chatLanguage)
    {
        if (string.IsNullOrEmpty(chatLanguage) || string.Equals(chatLanguage, "auto", StringComparison.Ordinal))
            return null;

        var languageName = LanguageNames.TryGetValue(chatLanguage, out var name) ? name : chatLanguage.ToUpperInvariant();

        return $"Always respond in {languageName}. " +
               "When citing the rulebook, translate the relevant section and include the original text in parentheses.";
    }

    private static IReadOnlyList<string> GenerateFollowUpQuestions(string agentName)
    {
        return new List<string>
        {
            "Can you explain that in more detail?",
            "What are the common mistakes to avoid?",
            "Are there any alternative strategies?"
        };
    }

    /// <summary>
    /// Maps a strategy name to its minimum required user tier.
    /// Issue #4471: Tier display in debug panel.
    /// </summary>
    private static string GetRequiredTierForStrategy(string strategy)
    {
        return strategy switch
        {
            "RetrievalOnly" => "free",
            "SingleModel" => "free",
            "MultiModelConsensus" => "premium",
            _ => "free"
        };
    }

    /// <summary>
    /// Estimates cost range for a strategy based on model pricing and typical token usage.
    /// Issue #4472: Pre-execution cost estimate.
    /// </summary>
    private PlaygroundCostEstimate EstimateCostRange(string strategy, string modelId)
    {
        var pricing = _costCalculator.GetModelPricing(modelId);
        if (pricing == null || pricing.IsFree)
        {
            return new PlaygroundCostEstimate(0, 0, 0, 0, true);
        }

        if (string.Equals(strategy, "RetrievalOnly", StringComparison.Ordinal))
        {
            return new PlaygroundCostEstimate(0, 0, pricing.InputCostPer1M, pricing.OutputCostPer1M, true);
        }

        // Token estimates per strategy (based on typical RAG interactions)
        var (minPrompt, maxPrompt, minCompletion, maxCompletion) = strategy switch
        {
            "MultiModelConsensus" => (1000, 4000, 400, 1600), // 2x SingleModel
            _ => (500, 2000, 200, 800) // SingleModel default
        };

        var minCost = (minPrompt / 1_000_000m * pricing.InputCostPer1M) +
                      (minCompletion / 1_000_000m * pricing.OutputCostPer1M);
        var maxCost = (maxPrompt / 1_000_000m * pricing.InputCostPer1M) +
                      (maxCompletion / 1_000_000m * pricing.OutputCostPer1M);

        return new PlaygroundCostEstimate(
            Math.Round(minCost, 6),
            Math.Round(maxCost, 6),
            pricing.InputCostPer1M,
            pricing.OutputCostPer1M,
            false);
    }

    private static RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, DateTime.UtcNow);
    }
}
