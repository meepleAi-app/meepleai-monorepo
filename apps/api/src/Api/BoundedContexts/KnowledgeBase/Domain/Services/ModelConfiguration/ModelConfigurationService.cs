using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// In-memory implementation of model configuration service.
/// Issue #3377: Models Tier Endpoint
/// </summary>
/// <remarks>
/// Pricing sourced from OpenRouter (https://openrouter.ai/models) as of 2026-01-19.
/// NOTE: Future enhancement - move to database for runtime configuration.
/// </remarks>
internal sealed class ModelConfigurationService : IModelConfigurationService
{
    private readonly ILogger<ModelConfigurationService> _logger;
    private readonly Dictionary<string, ModelConfiguration> _models;

    public ModelConfigurationService(ILogger<ModelConfigurationService> logger)
    {
        _logger = logger;
        _models = InitializeModels();
    }

    /// <inheritdoc/>
    public IReadOnlyList<ModelConfiguration> GetAllModels()
    {
        return _models.Values.ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<ModelConfiguration> GetModelsByTier(ModelTier tier)
    {
        return _models.Values
            .Where(m => m.Tier <= tier)
            .OrderBy(m => m.Tier)
            .ThenBy(m => string.Equals(m.Provider, "ollama", StringComparison.Ordinal) ? 1 : 0) // Cloud models before local Ollama
            .ThenBy(m => m.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc/>
    public ModelConfiguration? GetModelById(string modelId)
    {
        return _models.TryGetValue(modelId, out var model) ? model : null;
    }

    /// <inheritdoc/>
    public ModelTierValidationResult ValidateUserTierForModel(ModelTier userTier, string modelId)
    {
        var model = GetModelById(modelId);
        if (model == null)
        {
            _logger.LogWarning("Model tier validation failed: Model {ModelId} not found", modelId);
            return ModelTierValidationResult.ModelNotFound(userTier, modelId);
        }

        if (userTier.CanAccess(model.Tier))
        {
            _logger.LogDebug("Model tier validation passed: User tier {UserTier} can access model {ModelId} (requires {ModelTier})",
                userTier, modelId, model.Tier);
            return ModelTierValidationResult.Success(userTier, model.Tier, modelId);
        }

        _logger.LogWarning("Model tier validation failed: User tier {UserTier} cannot access model {ModelId} (requires {ModelTier})",
            userTier, modelId, model.Tier);
        return ModelTierValidationResult.InsufficientTier(userTier, model.Tier, modelId);
    }

    private static Dictionary<string, ModelConfiguration> InitializeModels()
    {
        var models = new Dictionary<string, ModelConfiguration>(StringComparer.Ordinal);

        // ========== FREE TIER MODELS ==========
        // Meta Llama free tier (OpenRouter)
        AddModel(models, ModelConfiguration.Create(
            id: "meta-llama/llama-3.3-70b-instruct:free",
            name: "Llama 3.3 70B Instruct (Free)",
            provider: "meta-llama",
            tier: ModelTier.Free,
            costPer1kInput: 0m,
            costPer1kOutput: 0m,
            maxTokens: 8192,
            supportsStreaming: true,
            description: "Free tier Llama 3.3 70B model via OpenRouter"));

        // Local Ollama models (self-hosted, free)
        AddModel(models, ModelConfiguration.Create(
            id: "llama3:8b",
            name: "Llama 3 8B (Local)",
            provider: "ollama",
            tier: ModelTier.Free,
            costPer1kInput: 0m,
            costPer1kOutput: 0m,
            maxTokens: 8192,
            supportsStreaming: true,
            description: "Self-hosted Llama 3 8B via Ollama"));

        AddModel(models, ModelConfiguration.Create(
            id: "mistral",
            name: "Mistral 7B (Local)",
            provider: "ollama",
            tier: ModelTier.Free,
            costPer1kInput: 0m,
            costPer1kOutput: 0m,
            maxTokens: 8192,
            supportsStreaming: true,
            description: "Self-hosted Mistral 7B via Ollama"));

        // ========== NORMAL TIER MODELS ==========
        // DeepSeek (affordable)
        AddModel(models, ModelConfiguration.Create(
            id: "deepseek/deepseek-chat",
            name: "DeepSeek Chat",
            provider: "deepseek",
            tier: ModelTier.Normal,
            costPer1kInput: 0.00027m,   // $0.27/1M = $0.00027/1K
            costPer1kOutput: 0.00110m,  // $1.10/1M = $0.00110/1K
            maxTokens: 8192,
            supportsStreaming: true,
            description: "Cost-effective reasoning model"));

        // Google Gemini Pro
        AddModel(models, ModelConfiguration.Create(
            id: "google/gemini-pro",
            name: "Gemini Pro",
            provider: "google",
            tier: ModelTier.Normal,
            costPer1kInput: 0.000125m,  // $0.125/1M = $0.000125/1K
            costPer1kOutput: 0.000375m, // $0.375/1M = $0.000375/1K
            maxTokens: 32768,
            supportsStreaming: true,
            description: "Google's Gemini Pro model"));

        // Meta Llama paid tier
        AddModel(models, ModelConfiguration.Create(
            id: "meta-llama/llama-3.3-70b-instruct",
            name: "Llama 3.3 70B Instruct",
            provider: "meta-llama",
            tier: ModelTier.Normal,
            costPer1kInput: 0.00059m,   // $0.59/1M = $0.00059/1K
            costPer1kOutput: 0.00079m,  // $0.79/1M = $0.00079/1K
            maxTokens: 8192,
            supportsStreaming: true,
            description: "Paid tier Llama 3.3 70B with higher rate limits"));

        // Local Llama 70B (self-hosted)
        AddModel(models, ModelConfiguration.Create(
            id: "llama3:70b",
            name: "Llama 3 70B (Local)",
            provider: "ollama",
            tier: ModelTier.Normal,
            costPer1kInput: 0m,
            costPer1kOutput: 0m,
            maxTokens: 8192,
            supportsStreaming: true,
            description: "Self-hosted Llama 3 70B via Ollama (requires powerful hardware)"));

        // ========== PREMIUM TIER MODELS ==========
        // Anthropic Claude
        AddModel(models, ModelConfiguration.Create(
            id: "anthropic/claude-3.5-haiku",
            name: "Claude 3.5 Haiku",
            provider: "anthropic",
            tier: ModelTier.Premium,
            costPer1kInput: 0.00025m,   // $0.25/1M = $0.00025/1K
            costPer1kOutput: 0.00125m,  // $1.25/1M = $0.00125/1K
            maxTokens: 8192,
            supportsStreaming: true,
            description: "Fast and intelligent Claude model"));

        AddModel(models, ModelConfiguration.Create(
            id: "anthropic/claude-3.5-sonnet",
            name: "Claude 3.5 Sonnet",
            provider: "anthropic",
            tier: ModelTier.Premium,
            costPer1kInput: 0.003m,     // $3/1M = $0.003/1K
            costPer1kOutput: 0.015m,    // $15/1M = $0.015/1K
            maxTokens: 8192,
            supportsStreaming: true,
            description: "Balanced Claude model for complex tasks"));

        // OpenAI GPT-4o
        AddModel(models, ModelConfiguration.Create(
            id: "openai/gpt-4o-mini",
            name: "GPT-4o Mini",
            provider: "openai",
            tier: ModelTier.Premium,
            costPer1kInput: 0.00015m,   // $0.15/1M = $0.00015/1K
            costPer1kOutput: 0.0006m,   // $0.60/1M = $0.0006/1K
            maxTokens: 16384,
            supportsStreaming: true,
            description: "Fast and affordable GPT-4o variant"));

        AddModel(models, ModelConfiguration.Create(
            id: "openai/gpt-4o",
            name: "GPT-4o",
            provider: "openai",
            tier: ModelTier.Premium,
            costPer1kInput: 0.005m,     // $5/1M = $0.005/1K
            costPer1kOutput: 0.015m,    // $15/1M = $0.015/1K
            maxTokens: 128000,
            supportsStreaming: true,
            description: "Most capable GPT-4 model"));

        // ========== CUSTOM TIER MODELS ==========
        // Claude Opus (highest tier)
        AddModel(models, ModelConfiguration.Create(
            id: "anthropic/claude-3-opus",
            name: "Claude 3 Opus",
            provider: "anthropic",
            tier: ModelTier.Custom,
            costPer1kInput: 0.015m,     // $15/1M = $0.015/1K
            costPer1kOutput: 0.075m,    // $75/1M = $0.075/1K
            maxTokens: 4096,
            supportsStreaming: true,
            description: "Most capable Claude model for complex reasoning"));

        return models;
    }

    private static void AddModel(Dictionary<string, ModelConfiguration> models, ModelConfiguration model)
    {
        models[model.Id] = model;
    }
}
