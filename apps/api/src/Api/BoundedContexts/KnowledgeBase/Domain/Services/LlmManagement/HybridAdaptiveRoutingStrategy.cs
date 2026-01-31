using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;

/// <summary>
/// ISSUE-959 (BGAI-019): Hybrid LLM routing with adaptive tier-based model selection
/// ISSUE-963 (BGAI-021): Enhanced with AI:Providers configuration integration (Option C)
/// ISSUE-1725: Enhanced with budget-aware model override support
/// ISSUE-2596: Enhanced with database-driven tier routing (database > appsettings fallback)
/// </summary>
internal class HybridAdaptiveRoutingStrategy : ILlmRoutingStrategy
{
    private readonly IConfiguration _configuration;
    private readonly ILlmModelOverrideService _modelOverrideService;
    private readonly ILlmTierRoutingService? _tierRoutingService;
    private readonly ILogger<HybridAdaptiveRoutingStrategy> _logger;
    private readonly IOptions<AiProviderSettings> _aiSettings;

    // Model configuration keys
    private const string AnonymousModelKey = "LlmRouting:AnonymousModel";
    private const string UserModelKey = "LlmRouting:UserModel";
    private const string EditorModelKey = "LlmRouting:EditorModel";
    private const string AdminModelKey = "LlmRouting:AdminModel";
    private const string PremiumModelKey = "LlmRouting:PremiumModel";

    // Traffic split configuration keys
    private const string AnonymousOpenRouterPercentKey = "LlmRouting:AnonymousOpenRouterPercent";
    private const string UserOpenRouterPercentKey = "LlmRouting:UserOpenRouterPercent";
    private const string EditorOpenRouterPercentKey = "LlmRouting:EditorOpenRouterPercent";
    private const string AdminOpenRouterPercentKey = "LlmRouting:AdminOpenRouterPercent";

    // Default models (ISSUE-958 decision: 4 models configured)
    private const string DefaultAnonymousOllamaModel = "meta-llama/llama-3.3-70b-instruct:free"; // Free tier OpenRouter
    private const string DefaultUserOllamaModel = "meta-llama/llama-3.3-70b-instruct:free";
    private const string DefaultEditorOllamaModel = "llama3:8b"; // Local Ollama
    private const string DefaultAdminOllamaModel = "llama3:8b";

    private const string DefaultAnonymousOpenRouterModel = "openai/gpt-4o-mini";
    private const string DefaultUserOpenRouterModel = "openai/gpt-4o-mini";
    private const string DefaultEditorOpenRouterModel = "openai/gpt-4o-mini";
    private const string DefaultAdminOpenRouterModel = "anthropic/claude-3.5-haiku";

    // Default traffic splits (target: 80% free, 20% paid overall)
    private const int DefaultAnonymousOpenRouterPercent = 20; // 80% free tier
    private const int DefaultUserOpenRouterPercent = 20;
    private const int DefaultEditorOpenRouterPercent = 50; // Balance quality/cost
    private const int DefaultAdminOpenRouterPercent = 80; // Prioritize quality

    public HybridAdaptiveRoutingStrategy(
        IConfiguration configuration,
        IOptions<AiProviderSettings> aiSettings,
        ILogger<HybridAdaptiveRoutingStrategy> logger,
        ILlmModelOverrideService? modelOverrideService = null,
        ILlmTierRoutingService? tierRoutingService = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _aiSettings = aiSettings ?? throw new ArgumentNullException(nameof(aiSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modelOverrideService = modelOverrideService ?? new NullModelOverrideService();
        _tierRoutingService = tierRoutingService; // Nullable for backward compatibility

        if (_tierRoutingService != null)
        {
            _logger.LogInformation(
                "HybridAdaptiveRoutingStrategy initialized with budget-aware model override support and database-driven tier routing");
        }
        else
        {
            _logger.LogInformation(
                "HybridAdaptiveRoutingStrategy initialized with budget-aware model override support (database routing not available)");
        }
    }

    /// <inheritdoc/>
    public LlmRoutingDecision SelectProvider(User? user, string? context = null)
    {
        // P1: Distinguish anonymous from authenticated users for separate config
        var isAnonymous = user == null;
        var userRole = user?.Role ?? Role.User;
        var userId = user?.Id.ToString() ?? "anonymous";

        var settings = _aiSettings.Value;

        // BGAI-022 Step 1: Check PreferredProvider override (Approach B)
        if (TryGetPreferredDecision(settings, userRole, userId, out var preferredDecision))
        {
            return preferredDecision!;
        }

        // ISSUE-2596 Step 2: Try database-driven tier routing first
        if (_tierRoutingService != null)
        {
            var databaseDecision = TryGetDatabaseRoutingDecision(userRole, isAnonymous, userId);
            if (databaseDecision != null)
            {
                return ApplyBudgetModeOverride(databaseDecision, userId);
            }
        }

        // BGAI-022 Step 2 (fallback): Use appsettings.json user-tier routing
        var (ollamaModel, openRouterModel, openRouterPercent) = GetTierConfiguration(userRole, isAnonymous);

        // Traffic split: Random selection based on configured percentage
        var useOpenRouter = ShouldUseOpenRouter(openRouterPercent);

        // S3358: Extract nested ternary to separate logic
        // Determine primary choice
        var primaryProvider = DeterminePrimaryProvider(useOpenRouter, ollamaModel);
        var primaryModel = useOpenRouter ? openRouterModel : ollamaModel;

        // BGAI-022 Step 3: Check if selected provider is enabled
        return GetValidatedDecision(primaryProvider, primaryModel, userRole, userId, settings, openRouterPercent);
    }

    /// <summary>
    /// S3358: Extracted helper to determine primary provider (avoids nested ternary).
    /// </summary>
    private static string DeterminePrimaryProvider(bool useOpenRouter, string ollamaModel)
    {
        if (useOpenRouter)
        {
            return "OpenRouter";
        }

        // If ollamaModel contains '/', it's actually an OpenRouter model ID
        return ollamaModel.Contains('/') ? "OpenRouter" : "Ollama";
    }

    /// <summary>
    /// ISSUE-2596: Try to get routing decision from database tier configuration.
    /// Returns null if database routing is not available or not configured for this tier.
    /// </summary>
    private LlmRoutingDecision? TryGetDatabaseRoutingDecision(Role userRole, bool isAnonymous, string userId)
    {
        try
        {
            var tier = MapRoleToTier(userRole, isAnonymous);

            // Use GetAwaiter().GetResult() since this is called from sync context
            // HybridCache L1 in-memory cache makes this safe for the common case
            var modelConfig = _tierRoutingService!
                .GetModelForTierAsync(tier, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (modelConfig == null)
            {
                _logger.LogDebug(
                    "[{UserId}] No database tier routing configured for {Tier}, falling back to appsettings",
                    userId, tier);
                return null;
            }

            // Determine provider from model ID pattern (OpenRouter models contain '/')
            var provider = modelConfig.ModelId.Contains('/') ? "OpenRouter" : "Ollama";

            _logger.LogDebug(
                "[{UserId}] Database tier routing: {Tier} → {Provider} ({Model})",
                userId, tier, provider, modelConfig.ModelId);

            return new LlmRoutingDecision(
                provider,
                modelConfig.ModelId,
                $"Database tier routing: {tier} (environment: {modelConfig.EnvironmentType})");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[{UserId}] Database tier routing failed, falling back to appsettings",
                userId);
            return null;
        }
    }

    /// <summary>
    /// ISSUE-2596: Map authentication Role to LlmUserTier.
    /// </summary>
    private static LlmUserTier MapRoleToTier(Role userRole, bool isAnonymous)
    {
        if (isAnonymous)
        {
            return LlmUserTier.Anonymous;
        }

        return userRole.Value switch
        {
            "admin" => LlmUserTier.Admin,
            "editor" => LlmUserTier.Editor,
            _ => LlmUserTier.User
        };
    }

    private bool TryGetPreferredDecision(AiProviderSettings settings, Role userRole, string userId, out LlmRoutingDecision? decision)
    {
        decision = null;
        if (!string.IsNullOrEmpty(settings.PreferredProvider) &&
            settings.Providers?.ContainsKey(settings.PreferredProvider) is true &&
            settings.Providers[settings.PreferredProvider].Enabled)
        {
            var preferredConfig = settings.Providers[settings.PreferredProvider];
            var preferredModel = preferredConfig.Models.Count > 0
                ? preferredConfig.Models[0]
                : GetDefaultModelForProvider(settings.PreferredProvider, userRole);

            _logger.LogDebug(
                "[{UserId}] Routing to PreferredProvider {Provider} ({Model}) - overriding user-tier routing",
                userId, settings.PreferredProvider, preferredModel);

            var rawDecision = new LlmRoutingDecision(
                settings.PreferredProvider,
                preferredModel,
                $"PreferredProvider override (AI:PreferredProvider = {settings.PreferredProvider})");

            // ISSUE-1725: Apply budget mode overrides
            decision = ApplyBudgetModeOverride(rawDecision, userId);
            return true;
        }
        return false;
    }

    private LlmRoutingDecision GetValidatedDecision(
        string provider,
        string model,
        Role userRole,
        string userId,
        AiProviderSettings settings,
        int openRouterPercent)
    {
        // Verify provider is enabled (backward compatible: if AI section missing, allow all)
        if (settings.Providers?.ContainsKey(provider) is true &&
            !settings.Providers[provider].Enabled)
        {
            return CreateFallbackDecision(provider, userRole, userId, settings);
        }

        // Provider is enabled or AI section not configured (backward compatible)
        _logger.LogDebug(
            "[{UserId}] Routing to {Provider} ({Model}) - Role: {Role}, Traffic split: {Percent}%",
            userId, provider, model, userRole.Value, openRouterPercent);

        var reason = string.Equals(provider, "OpenRouter"
, StringComparison.Ordinal) ? $"User tier: {userRole.Value}, Traffic split: {openRouterPercent}%"
            : $"User tier: {userRole.Value}, Cost-optimized primary";

        var decision = new LlmRoutingDecision(provider, model, reason);

        // ISSUE-1725: Apply budget mode overrides if active
        return ApplyBudgetModeOverride(decision, userId);
    }

    private LlmRoutingDecision CreateFallbackDecision(string failedProvider, Role userRole, string userId, AiProviderSettings settings)
    {
        _logger.LogWarning(
            "[{UserId}] Selected provider {Provider} is disabled (AI:Providers:{ProviderName}:Enabled = false), trying fallback",
            userId, failedProvider, failedProvider);

        // Try alternative provider
        var alternativeProvider = string.Equals(failedProvider, "Ollama", StringComparison.Ordinal) ? "OpenRouter" : "Ollama";

        // ISSUE-1159: Treat missing providers as implicitly enabled (using default configuration)
        var alternativeEnabled = !settings.Providers.ContainsKey(alternativeProvider) ||
                                 settings.Providers[alternativeProvider].Enabled;

        if (alternativeEnabled)
        {
            // Get model from config or use default
            var alternativeModel = settings.Providers.TryGetValue(alternativeProvider, out var altProviderConfig) &&
                                   altProviderConfig.Models.Count > 0
                ? altProviderConfig.Models[0]
                : GetDefaultModelForProvider(alternativeProvider, userRole);

            _logger.LogInformation(
                "[{UserId}] Fallback to {Provider} ({Model}) - primary provider disabled",
                userId, alternativeProvider, alternativeModel);

            var fallbackDecision = new LlmRoutingDecision(
                alternativeProvider,
                alternativeModel,
                $"Fallback from {failedProvider} (disabled in AI:Providers)");

            // ISSUE-1725: Apply budget mode overrides
            return ApplyBudgetModeOverride(fallbackDecision, userId);
        }

        // Both providers explicitly disabled - throw exception
        throw new InvalidOperationException(
            "Both AI providers are disabled. At least one provider must be enabled.");
    }

    /// <summary>
    /// ISSUE-1725: Apply model override if budget mode is active
    /// </summary>
    private LlmRoutingDecision ApplyBudgetModeOverride(LlmRoutingDecision decision, string userId)
    {
        if (!_modelOverrideService.IsInBudgetMode())
        {
            return decision; // No override needed
        }

        var overrideModel = _modelOverrideService.GetOverrideModel(decision.ModelId);
        if (!string.Equals(overrideModel, decision.ModelId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "[{UserId}] Budget mode active: {Original} → {Override}",
                userId, decision.ModelId, overrideModel);

            return new LlmRoutingDecision(
                decision.ProviderName,
                overrideModel,
                $"{decision.Reason} (Budget mode: downgraded from {decision.ModelId})");
        }

        return decision;
    }

    /// <summary>
    /// Get tier-specific model configuration
    /// </summary>
    private (string ollamaModel, string openRouterModel, int openRouterPercent) GetTierConfiguration(Role role, bool isAnonymous)
    {
        // P1: Honor anonymous-tier routing configuration (separate from authenticated users)
        if (isAnonymous)
        {
            return (
                _configuration[AnonymousModelKey] ?? DefaultAnonymousOllamaModel,
                _configuration[AnonymousModelKey.Replace("Model", "OpenRouterModel")] ?? DefaultAnonymousOpenRouterModel,
                _configuration.GetValue<int>(AnonymousOpenRouterPercentKey, DefaultAnonymousOpenRouterPercent)
            );
        }

        return role.Value switch
        {
            "admin" => (
                _configuration[AdminModelKey] ?? DefaultAdminOllamaModel,
                _configuration[PremiumModelKey] ?? DefaultAdminOpenRouterModel,
                _configuration.GetValue<int>(AdminOpenRouterPercentKey, DefaultAdminOpenRouterPercent)
            ),
            "editor" => (
                _configuration[EditorModelKey] ?? DefaultEditorOllamaModel,
                _configuration[EditorModelKey.Replace("Model", "OpenRouterModel")] ?? DefaultEditorOpenRouterModel,
                _configuration.GetValue<int>(EditorOpenRouterPercentKey, DefaultEditorOpenRouterPercent)
            ),
            _ => ( // authenticated user (not admin/editor)
                _configuration[UserModelKey] ?? DefaultUserOllamaModel,
                _configuration[UserModelKey.Replace("Model", "OpenRouterModel")] ?? DefaultUserOpenRouterModel,
                _configuration.GetValue<int>(UserOpenRouterPercentKey, DefaultUserOpenRouterPercent)
            )
        };
    }

    /// <summary>
    /// Determine if OpenRouter should be used based on traffic split percentage
    /// </summary>
    /// <param name="openRouterPercent">Percentage of traffic routed to OpenRouter (0-100)</param>
    /// <returns>True if OpenRouter should be used</returns>
    private bool ShouldUseOpenRouter(int openRouterPercent)
    {
        if (openRouterPercent <= 0) return false;
        if (openRouterPercent >= 100) return true;

        // SCS0005: Random.Shared is acceptable for A/B testing (non-security-critical)
#pragma warning disable SCS0005
        var random = Random.Shared.Next(100);
#pragma warning restore SCS0005

        return random < openRouterPercent;
    }

    /// <summary>
    /// BGAI-022: Get default model for a provider when not specified in configuration
    /// ISSUE-1159: Enhanced to support tier-aware defaults for better fallback behavior
    /// </summary>
    private static string GetDefaultModelForProvider(string providerName, Role? userRole = null)
    {
        return providerName.ToLowerInvariant() switch
        {
            "ollama" => "llama3:8b",
            "openrouter" => string.Equals(userRole?.Value, "admin"
, StringComparison.Ordinal) ? "anthropic/claude-3.5-haiku"
                : "meta-llama/llama-3.3-70b-instruct:free",
            _ => "llama3:8b"
        };
    }
}
