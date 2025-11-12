using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Hybrid adaptive routing strategy combining user-type and traffic split
/// ISSUE-958: Implements Option B - Ollama Primary + OpenRouter Fallback
/// </summary>
/// <remarks>
/// Routing Logic:
/// 1. User-type mapping: Admin → premium models, Editor → standard, User/Anonymous → free
/// 2. Traffic split override: Configurable percentage for A/B testing per tier
/// 3. Cost optimization: 80% free tier (Ollama/Llama 3.3 70B), 20% paid (GPT-4o-mini)
///
/// Default Model Configuration:
/// - Anonymous/User: 80% llama3.3:70b-instruct-q4_K_M (free tier), 20% openai/gpt-4o-mini
/// - Editor: 50% llama3.3:70b, 50% openai/gpt-4o-mini
/// - Admin: 20% llama3:8b (local), 80% anthropic/claude-3.5-haiku
/// </remarks>
public class HybridAdaptiveRoutingStrategy : ILlmRoutingStrategy
{
    private readonly ILogger<HybridAdaptiveRoutingStrategy> _logger;
    private readonly IConfiguration _configuration;

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
        ILogger<HybridAdaptiveRoutingStrategy> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public LlmRoutingDecision SelectProvider(User? user, string? context = null)
    {
        // P1: Distinguish anonymous from authenticated users for separate config
        var isAnonymous = user == null;
        var userRole = user?.Role ?? Role.User;
        var userId = user?.Id.ToString() ?? "anonymous";

        // Get user-tier specific models and traffic split
        var (ollamaModel, openRouterModel, openRouterPercent) = GetTierConfiguration(userRole, isAnonymous);

        // Traffic split: Random selection based on configured percentage
        var useOpenRouter = ShouldUseOpenRouter(openRouterPercent);

        if (useOpenRouter)
        {
            _logger.LogDebug(
                "[{UserId}] Routing to OpenRouter ({Model}) - Role: {Role}, Traffic split: {Percent}%",
                userId, openRouterModel, userRole.Value, openRouterPercent);

            return LlmRoutingDecision.OpenRouter(
                openRouterModel,
                $"User tier: {userRole.Value}, Traffic split: {openRouterPercent}%");
        }

        // Determine if Ollama model is local or OpenRouter free tier
        var providerName = ollamaModel.Contains('/') ? "OpenRouter" : "Ollama";

        _logger.LogDebug(
            "[{UserId}] Routing to {Provider} ({Model}) - Role: {Role}, Cost optimization",
            userId, providerName, ollamaModel, userRole.Value);

        return new LlmRoutingDecision(
            providerName,
            ollamaModel,
            $"User tier: {userRole.Value}, Cost-optimized primary");
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
}
