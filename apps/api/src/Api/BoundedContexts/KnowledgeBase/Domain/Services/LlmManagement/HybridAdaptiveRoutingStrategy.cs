using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;

/// <summary>
/// ISSUE-959 (BGAI-019): Hybrid LLM routing with adaptive model selection
/// ISSUE-963 (BGAI-021): Enhanced with AI:Providers configuration integration
/// ISSUE-1725: Enhanced with budget-aware model override support
/// ISSUE-3435: Refactored from tier-based to strategy-based routing.
///
/// Strategy determines model selection, tier validates access.
/// </summary>
internal class HybridAdaptiveRoutingStrategy : ILlmRoutingStrategy
{
    private readonly IStrategyModelMappingService _strategyModelMappingService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILlmModelOverrideService _modelOverrideService;
    private readonly ILogger<HybridAdaptiveRoutingStrategy> _logger;
    private readonly IOptions<AiProviderSettings> _aiSettings;

    public HybridAdaptiveRoutingStrategy(
        IStrategyModelMappingService strategyModelMappingService,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AiProviderSettings> aiSettings,
        ILogger<HybridAdaptiveRoutingStrategy> logger,
        ILlmModelOverrideService? modelOverrideService = null)
    {
        _strategyModelMappingService = strategyModelMappingService ?? throw new ArgumentNullException(nameof(strategyModelMappingService));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _aiSettings = aiSettings ?? throw new ArgumentNullException(nameof(aiSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modelOverrideService = modelOverrideService ?? new NullModelOverrideService();

        _logger.LogInformation(
            "HybridAdaptiveRoutingStrategy initialized with strategy-based routing (Issue #3435)");
    }

    /// <inheritdoc/>
    public LlmRoutingDecision SelectProvider(User? user, RagStrategy strategy, string? context = null)
    {
        var userId = user?.Id.ToString() ?? "anonymous";
        var tier = MapUserToLlmTier(user);

        var settings = _aiSettings.Value;

        _logger.LogDebug(
            "[{UserId}] SelectProvider called with strategy {Strategy}, tier {Tier}",
            userId, strategy.GetDisplayName(), tier);

        // Step 1: Check PreferredProvider override (still honored if explicitly set)
        if (TryGetPreferredDecision(settings, userId, out var preferredDecision))
        {
            return preferredDecision!;
        }

        // Step 2: Validate tier has access to the requested strategy
        var validationResult = ValidateTierAccess(tier, strategy, userId);
        if (!validationResult.IsValid)
        {
            throw new UnauthorizedAccessException(
                $"Tier {tier} does not have access to strategy {strategy.GetDisplayName()}. " +
                $"Available strategies: {string.Join(", ", validationResult.AvailableStrategies.Select(s => s.GetDisplayName()))}");
        }

        // Step 3: Get model mapping for the strategy (strategy determines model)
        var (provider, modelId) = GetModelForStrategy(strategy, userId);

        // Step 4: Validate provider is enabled and create decision
        var decision = CreateValidatedDecision(provider, modelId, strategy, tier, userId, settings);

        // Step 5: Apply budget mode override if active
        return ApplyBudgetModeOverride(decision, userId);
    }

    /// <summary>
    /// Validate that the user's tier has access to the requested strategy.
    /// Uses IServiceScopeFactory to resolve scoped ITierStrategyAccessService.
    /// </summary>
    private TierAccessValidation ValidateTierAccess(LlmUserTier tier, RagStrategy strategy, string userId)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var tierStrategyAccessService = scope.ServiceProvider.GetRequiredService<ITierStrategyAccessService>();

            var hasAccess = tierStrategyAccessService
                .HasAccessToStrategyAsync(tier, strategy, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (hasAccess)
            {
                _logger.LogDebug(
                    "[{UserId}] Tier {Tier} has access to strategy {Strategy}",
                    userId, tier, strategy.GetDisplayName());

                return TierAccessValidation.Valid();
            }

            var availableStrategies = tierStrategyAccessService
                .GetAvailableStrategiesAsync(tier, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            _logger.LogWarning(
                "[{UserId}] Tier {Tier} denied access to strategy {Strategy}. Available: {Available}",
                userId, tier, strategy.GetDisplayName(),
                string.Join(", ", availableStrategies.Select(s => s.GetDisplayName())));

            return TierAccessValidation.Denied(availableStrategies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{UserId}] Error validating tier access for {Tier}/{Strategy}",
                userId, tier, strategy.GetDisplayName());

            // On error, deny access for safety
            return TierAccessValidation.Denied(Array.Empty<RagStrategy>());
        }
    }

    /// <summary>
    /// Get the model mapping for a strategy from the strategy-model mapping service.
    /// </summary>
    private (string Provider, string ModelId) GetModelForStrategy(RagStrategy strategy, string userId)
    {
        try
        {
            var (provider, modelId) = _strategyModelMappingService
                .GetModelForStrategyAsync(strategy, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            _logger.LogDebug(
                "[{UserId}] Strategy {Strategy} → {Provider}/{Model}",
                userId, strategy.GetDisplayName(), provider, modelId);

            return (provider, modelId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[{UserId}] Error getting model for strategy {Strategy}, using default",
                userId, strategy.GetDisplayName());

            // Fall back to default mapping
            var defaultMapping = StrategyModelMapping.Default(strategy);
            return (defaultMapping.Provider, defaultMapping.PrimaryModel);
        }
    }

    /// <summary>
    /// Create a validated routing decision, checking provider enablement.
    /// </summary>
    private LlmRoutingDecision CreateValidatedDecision(
        string provider,
        string modelId,
        RagStrategy strategy,
        LlmUserTier tier,
        string userId,
        AiProviderSettings settings)
    {
        // Verify provider is enabled
        if (settings.Providers?.ContainsKey(provider) is true &&
            !settings.Providers[provider].Enabled)
        {
            _logger.LogWarning(
                "[{UserId}] Provider {Provider} is disabled, attempting fallback",
                userId, provider);

            return CreateFallbackDecision(provider, strategy, tier, userId, settings);
        }

        var reason = $"Strategy: {strategy.GetDisplayName()}, Tier: {tier}";

        _logger.LogDebug(
            "[{UserId}] Routing decision: {Provider}/{Model} - {Reason}",
            userId, provider, modelId, reason);

        return new LlmRoutingDecision(provider, modelId, reason);
    }

    /// <summary>
    /// Create a fallback decision when the primary provider is disabled.
    /// </summary>
    private LlmRoutingDecision CreateFallbackDecision(
        string failedProvider,
        RagStrategy strategy,
        LlmUserTier tier,
        string userId,
        AiProviderSettings settings)
    {
        // Try alternative provider
        var alternativeProvider = string.Equals(failedProvider, "Ollama", StringComparison.Ordinal)
            ? "OpenRouter"
            : "Ollama";

        var alternativeEnabled = !settings.Providers.ContainsKey(alternativeProvider) ||
                                 settings.Providers[alternativeProvider].Enabled;

        if (alternativeEnabled)
        {
            // Get fallback models for the strategy
            var fallbackModels = _strategyModelMappingService
                .GetFallbackModelsAsync(strategy, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            // Find a fallback model for the alternative provider
            var alternativeModel = fallbackModels
                .FirstOrDefault(m =>
                    (string.Equals(alternativeProvider, "OpenRouter", StringComparison.Ordinal) && m.Contains('/')) ||
                    (string.Equals(alternativeProvider, "Ollama", StringComparison.Ordinal) && !m.Contains('/')));

            if (string.IsNullOrEmpty(alternativeModel))
            {
                // Use default for the alternative provider
                alternativeModel = GetDefaultModelForProvider(alternativeProvider);
            }

            _logger.LogInformation(
                "[{UserId}] Fallback to {Provider}/{Model} - strategy: {Strategy}",
                userId, alternativeProvider, alternativeModel, strategy.GetDisplayName());

            return new LlmRoutingDecision(
                alternativeProvider,
                alternativeModel,
                $"Fallback from {failedProvider} (disabled), Strategy: {strategy.GetDisplayName()}, Tier: {tier}");
        }

        // Both providers disabled
        throw new InvalidOperationException(
            $"Both AI providers are disabled. Cannot route strategy {strategy.GetDisplayName()}.");
    }

    /// <summary>
    /// Map user to LlmUserTier based on role AND subscription tier.
    /// E4-2: Tier-aware LLM routing — premium subscribers get Premium tier
    /// regardless of their role. Admin role always wins.
    /// When user is null (internal pipeline calls like ConversationQueryRewriter),
    /// default to User tier so internal services aren't blocked by Anonymous tier
    /// which has zero strategy access.
    /// </summary>
    internal static LlmUserTier MapUserToLlmTier(User? user)
    {
        if (user is null)
        {
            // Internal pipeline calls (e.g., query rewriting) pass user=null.
            // Use User tier as default so they can access basic strategies.
            return LlmUserTier.User;
        }

        // Admin role always gets Admin tier (highest access)
        if (string.Equals(user.Role.Value, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return LlmUserTier.Admin;
        }

        // Editor role gets Editor tier
        if (string.Equals(user.Role.Value, "editor", StringComparison.OrdinalIgnoreCase))
        {
            return LlmUserTier.Editor;
        }

        // E4-2: Check subscription tier for premium/enterprise users
        if (user.Tier.IsPremium() || user.Tier.IsEnterprise())
        {
            return LlmUserTier.Premium;
        }

        // Free/normal subscription tier → User LLM tier
        return LlmUserTier.User;
    }

    /// <summary>
    /// Check for PreferredProvider override in settings.
    /// </summary>
    private bool TryGetPreferredDecision(AiProviderSettings settings, string userId, out LlmRoutingDecision? decision)
    {
        decision = null;

        if (!string.IsNullOrEmpty(settings.PreferredProvider) &&
            settings.Providers?.ContainsKey(settings.PreferredProvider) is true &&
            settings.Providers[settings.PreferredProvider].Enabled)
        {
            var preferredConfig = settings.Providers[settings.PreferredProvider];
            var preferredModel = preferredConfig.Models.Count > 0
                ? preferredConfig.Models[0]
                : GetDefaultModelForProvider(settings.PreferredProvider);

            _logger.LogDebug(
                "[{UserId}] Using PreferredProvider override: {Provider}/{Model}",
                userId, settings.PreferredProvider, preferredModel);

            var rawDecision = new LlmRoutingDecision(
                settings.PreferredProvider,
                preferredModel,
                $"PreferredProvider override (AI:PreferredProvider = {settings.PreferredProvider})");

            decision = ApplyBudgetModeOverride(rawDecision, userId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Apply budget mode override if active.
    /// </summary>
    private LlmRoutingDecision ApplyBudgetModeOverride(LlmRoutingDecision decision, string userId)
    {
        if (!_modelOverrideService.IsInBudgetMode())
        {
            return decision;
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
    /// Get default model for a provider.
    /// Uses AgentDefaults for centralized, configurable model names.
    /// </summary>
    private static string GetDefaultModelForProvider(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "ollama" => AgentDefaults.OllamaFallbackModel,
            "openrouter" => AgentDefaults.DefaultModel,
            _ => AgentDefaults.OllamaFallbackModel
        };
    }

    /// <summary>
    /// Internal struct for tier access validation result.
    /// </summary>
    private readonly struct TierAccessValidation
    {
        public bool IsValid { get; }
        public IReadOnlyList<RagStrategy> AvailableStrategies { get; }

        private TierAccessValidation(bool isValid, IReadOnlyList<RagStrategy> availableStrategies)
        {
            IsValid = isValid;
            AvailableStrategies = availableStrategies;
        }

        public static TierAccessValidation Valid() => new(true, Array.Empty<RagStrategy>());
        public static TierAccessValidation Denied(IReadOnlyList<RagStrategy> available) => new(false, available);
    }
}
