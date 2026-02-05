using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Service for validating user tier access to RAG strategies.
/// Issue #3436: Part of tier-strategy-model architecture.
/// </summary>
/// <remarks>
/// Default access matrix (when database is empty):
/// - Anonymous: [] (no strategies)
/// - User: [FAST, BALANCED]
/// - Editor: [FAST, BALANCED, PRECISE, SENTENCE_WINDOW]
/// - Admin: [ALL] (wildcard)
/// - Premium: [FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, SENTENCE_WINDOW, ITERATIVE, MULTI_AGENT]
/// </remarks>
internal sealed class TierStrategyAccessService : ITierStrategyAccessService
{
    private const string WildcardStrategy = "*";
    private readonly ITierStrategyAccessRepository _repository;
    private readonly ILogger<TierStrategyAccessService> _logger;

    // Default access matrix when database has no entries
    private static readonly IReadOnlyDictionary<LlmUserTier, RagStrategy[]> s_defaultAccessMatrix =
        new Dictionary<LlmUserTier, RagStrategy[]>
        {
            { LlmUserTier.Anonymous, Array.Empty<RagStrategy>() },
            { LlmUserTier.User, new[] { RagStrategy.Fast, RagStrategy.Balanced } },
            { LlmUserTier.Editor, new[] { RagStrategy.Fast, RagStrategy.Balanced, RagStrategy.Precise, RagStrategy.SentenceWindow, RagStrategy.StepBack, RagStrategy.QueryExpansion } },
            { LlmUserTier.Admin, Enum.GetValues<RagStrategy>() }, // All strategies
            { LlmUserTier.Premium, new[] { RagStrategy.Fast, RagStrategy.Balanced, RagStrategy.Precise, RagStrategy.Expert, RagStrategy.Consensus, RagStrategy.SentenceWindow, RagStrategy.Iterative, RagStrategy.MultiAgent, RagStrategy.StepBack, RagStrategy.QueryExpansion } }
        };

    public TierStrategyAccessService(
        ITierStrategyAccessRepository repository,
        ILogger<TierStrategyAccessService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<bool> HasAccessToStrategyAsync(
        LlmUserTier tier,
        RagStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var strategyName = strategy.GetDisplayName();

        // First check database for configured access
        var hasDbAccess = await _repository.IsAccessEnabledAsync(tier, strategyName, cancellationToken)
            .ConfigureAwait(false);

        if (hasDbAccess)
        {
            _logger.LogDebug(
                "Tier {Tier} has database-configured access to strategy {Strategy}",
                tier, strategyName);
            return true;
        }

        // Check if any database entries exist for this tier
        var tierEntries = await _repository.GetAllForTierAsync(tier, cancellationToken)
            .ConfigureAwait(false);

        if (tierEntries.Count > 0)
        {
            // Database has entries but this strategy is not enabled
            _logger.LogDebug(
                "Tier {Tier} has database config but no access to strategy {Strategy}",
                tier, strategyName);
            return false;
        }

        // Fall back to default matrix when no database entries exist
        var defaultAccess = GetDefaultStrategies(tier).Contains(strategy);
        _logger.LogDebug(
            "Using default access matrix for tier {Tier}: {HasAccess} to {Strategy}",
            tier, defaultAccess, strategyName);

        return defaultAccess;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RagStrategy>> GetAvailableStrategiesAsync(
        LlmUserTier tier,
        CancellationToken cancellationToken = default)
    {
        // First try database
        var dbStrategies = await _repository.GetEnabledStrategiesForTierAsync(tier, cancellationToken)
            .ConfigureAwait(false);

        if (dbStrategies.Count > 0)
        {
            // Check for wildcard (all strategies)
            if (dbStrategies.Contains(WildcardStrategy, StringComparer.Ordinal))
            {
                _logger.LogDebug("Tier {Tier} has wildcard access to all strategies", tier);
                return Enum.GetValues<RagStrategy>().ToList();
            }

            // Parse strategy names to enums
            var strategies = dbStrategies
                .Select(s => RagStrategyExtensions.TryParse(s, out var strategy) ? strategy : (RagStrategy?)null)
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .OrderBy(s => s)
                .ToList();

            _logger.LogDebug(
                "Tier {Tier} has {Count} database-configured strategies",
                tier, strategies.Count);

            return strategies;
        }

        // Check if tier has any entries (could be all disabled)
        var tierEntries = await _repository.GetAllForTierAsync(tier, cancellationToken)
            .ConfigureAwait(false);

        if (tierEntries.Count > 0)
        {
            // Has entries but all disabled
            _logger.LogDebug("Tier {Tier} has database entries but all are disabled", tier);
            return Array.Empty<RagStrategy>();
        }

        // Fall back to default matrix
        var defaultStrategies = GetDefaultStrategies(tier);
        _logger.LogDebug(
            "Using default access matrix for tier {Tier}: {Count} strategies",
            tier, defaultStrategies.Count);

        return defaultStrategies;
    }

    /// <inheritdoc/>
    public async Task<TierStrategyValidationResult> ValidateAccessAsync(
        LlmUserTier tier,
        RagStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var hasAccess = await HasAccessToStrategyAsync(tier, strategy, cancellationToken)
            .ConfigureAwait(false);

        if (hasAccess)
        {
            _logger.LogInformation(
                "Access validated: Tier {Tier} can use strategy {Strategy}",
                tier, strategy.GetDisplayName());

            return TierStrategyValidationResult.Success(tier, strategy);
        }

        // Get available strategies for helpful error message
        var availableStrategies = await GetAvailableStrategiesAsync(tier, cancellationToken)
            .ConfigureAwait(false);

        if (availableStrategies.Count == 0)
        {
            _logger.LogWarning(
                "Access denied: Tier {Tier} has no available strategies",
                tier);

            return TierStrategyValidationResult.NoStrategiesAvailable(tier, strategy);
        }

        _logger.LogWarning(
            "Access denied: Tier {Tier} cannot use strategy {Strategy}. Available: {Available}",
            tier,
            strategy.GetDisplayName(),
            string.Join(", ", availableStrategies.Select(s => s.GetDisplayName())));

        return TierStrategyValidationResult.AccessDenied(tier, strategy, availableStrategies);
    }

    /// <inheritdoc/>
    public async Task<RagStrategy?> GetDefaultStrategyAsync(
        LlmUserTier tier,
        CancellationToken cancellationToken = default)
    {
        var availableStrategies = await GetAvailableStrategiesAsync(tier, cancellationToken)
            .ConfigureAwait(false);

        if (availableStrategies.Count == 0)
        {
            _logger.LogDebug("No default strategy for tier {Tier}: no strategies available", tier);
            return null;
        }

        // Return the first (lowest complexity) available strategy
        var defaultStrategy = availableStrategies.OrderBy(s => s.GetComplexityLevel()).First();

        _logger.LogDebug(
            "Default strategy for tier {Tier}: {Strategy}",
            tier, defaultStrategy.GetDisplayName());

        return defaultStrategy;
    }

    private static IReadOnlyList<RagStrategy> GetDefaultStrategies(LlmUserTier tier)
    {
        return s_defaultAccessMatrix.TryGetValue(tier, out var strategies)
            ? strategies
            : Array.Empty<RagStrategy>();
    }
}
