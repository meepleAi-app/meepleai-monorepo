using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;

/// <summary>
/// ISSUE-1725: Implementation of ILlmModelOverrideService for budget-based model downgrading.
/// Thread-safe in-memory service for managing model overrides during budget constraints.
/// </summary>
public class LlmModelOverrideService : ILlmModelOverrideService
{
    private readonly ILogger<LlmModelOverrideService> _logger;
    private readonly Dictionary<string, string> _downgradeMappings;
    private readonly System.Threading.Lock _lock = new();

    private bool _budgetModeActive;
    private string? _budgetModeReason;
    private DateTime? _budgetModeEnabledAt;

    public LlmModelOverrideService(
        IConfiguration configuration,
        ILogger<LlmModelOverrideService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load downgrade mappings from configuration (or use defaults)
        _downgradeMappings = LoadDowngradeMappings(configuration);

        _logger.LogInformation(
            "LlmModelOverrideService initialized with {Count} downgrade mappings",
            _downgradeMappings.Count);
    }

    public bool IsInBudgetMode()
    {
        lock (_lock)
        {
            return _budgetModeActive;
        }
    }

    public void EnableBudgetMode(string reason)
    {
        lock (_lock)
        {
            if (_budgetModeActive)
            {
                _logger.LogDebug("Budget mode already active, updating reason: {Reason}", reason);
            }
            else
            {
                _logger.LogWarning("🚨 BUDGET MODE ENABLED: {Reason}", reason);
            }

            _budgetModeActive = true;
            _budgetModeReason = reason;
            _budgetModeEnabledAt = DateTime.UtcNow;
        }
    }

    public void DisableBudgetMode()
    {
        lock (_lock)
        {
            if (_budgetModeActive)
            {
                var duration = DateTime.UtcNow - (_budgetModeEnabledAt ?? DateTime.UtcNow);
                _logger.LogInformation(
                    "✅ Budget mode disabled after {Duration} (Reason was: {Reason})",
                    duration, _budgetModeReason);
            }

            _budgetModeActive = false;
            _budgetModeReason = null;
            _budgetModeEnabledAt = null;
        }
    }

    public string GetOverrideModel(string originalModel)
    {
        lock (_lock)
        {
            if (!_budgetModeActive)
            {
                return originalModel; // No override when not in budget mode
            }

            if (_downgradeMappings.TryGetValue(originalModel, out var cheaperModel))
            {
                _logger.LogDebug(
                    "Budget mode: Downgrading {Original} → {Cheaper}",
                    originalModel, cheaperModel);
                return cheaperModel;
            }

            // No mapping found, return original
            _logger.LogDebug(
                "Budget mode: No downgrade mapping for {Model}, using original",
                originalModel);
            return originalModel;
        }
    }

    public string GetBudgetModeStatus()
    {
        lock (_lock)
        {
            if (!_budgetModeActive)
            {
                return "Budget mode: INACTIVE";
            }

            var duration = DateTime.UtcNow - (_budgetModeEnabledAt ?? DateTime.UtcNow);
            return $"Budget mode: ACTIVE for {duration.TotalMinutes:F1}min - Reason: {_budgetModeReason}";
        }
    }

    private Dictionary<string, string> LoadDowngradeMappings(IConfiguration configuration)
    {
        // Try to load from configuration first
        var mappings = configuration.GetSection("LlmBudgetAlerts:ModelDowngradeMappings")
            .Get<Dictionary<string, string>>();

        if (mappings != null && mappings.Count > 0)
        {
            _logger.LogInformation(
                "Loaded {Count} model downgrade mappings from configuration",
                mappings.Count);
            return new Dictionary<string, string>(mappings, StringComparer.Ordinal);
        }

        // Fallback to defaults (based on Issue #1725 spec)
        _logger.LogInformation("Using default model downgrade mappings");
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // OpenAI models (10x cost reduction)
            ["openai/gpt-4o"] = "openai/gpt-4o-mini",
            ["openai/gpt-4"] = "openai/gpt-3.5-turbo",

            // Anthropic models (5x cost reduction)
            ["anthropic/claude-3-opus"] = "anthropic/claude-3.5-sonnet",
            ["anthropic/claude-3.5-sonnet"] = "anthropic/claude-3.5-haiku",

            // Already cheap models → switch to free tier
            ["openai/gpt-4o-mini"] = "meta-llama/llama-3.3-70b-instruct:free",
            ["openai/gpt-3.5-turbo"] = "meta-llama/llama-3.3-70b-instruct:free",
            ["anthropic/claude-3.5-haiku"] = "meta-llama/llama-3.3-70b-instruct:free",

            // DeepSeek → free tier
            ["deepseek/deepseek-chat"] = "meta-llama/llama-3.1-70b-instruct:free"
        };
    }
}

/// <summary>
/// ISSUE-1725: Null object pattern for ILlmModelOverrideService.
/// Used when budget mode is not enabled or service not injected (tests/backwards compatibility).
/// </summary>
internal class NullModelOverrideService : ILlmModelOverrideService
{
    public bool IsInBudgetMode() => false;
    public void EnableBudgetMode(string reason) { }
    public void DisableBudgetMode() { }
    public string GetOverrideModel(string originalModel) => originalModel;
    public string GetBudgetModeStatus() => "Budget mode: DISABLED (no override service)";
}

