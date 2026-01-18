using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.Services.LlmClients;

/// <summary>
/// Factory for creating LLM client instances based on provider type.
/// Issue #2391 Sprint 2
/// </summary>
internal class LlmProviderFactory
{
    private readonly IEnumerable<ILlmClient> _clients;
    private readonly ILogger<LlmProviderFactory> _logger;

    public LlmProviderFactory(
        IEnumerable<ILlmClient> clients,
        ILogger<LlmProviderFactory> logger)
    {
        _clients = clients ?? throw new ArgumentNullException(nameof(clients));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets an LLM client for the specified provider.
    /// </summary>
    public ILlmClient GetClient(LlmProvider provider)
    {
        var providerName = provider switch
        {
            LlmProvider.OpenRouter => "OpenRouter",
            LlmProvider.Ollama => "Ollama",
            _ => throw new ArgumentException($"Unknown provider: {provider}", nameof(provider))
        };

        var client = _clients.FirstOrDefault(c => string.Equals(c.ProviderName, providerName, StringComparison.Ordinal));

        if (client == null)
        {
            _logger.LogError("LLM client not found for provider: {Provider}", providerName);
            throw new InvalidOperationException($"LLM client not configured for provider: {providerName}");
        }

        return client;
    }

    /// <summary>
    /// Gets an LLM client that supports the specified model.
    /// </summary>
    public ILlmClient GetClientForModel(string modelId)
    {
        var client = _clients.FirstOrDefault(c => c.SupportsModel(modelId));

        if (client == null)
        {
            _logger.LogWarning("No LLM client found for model: {ModelId}", modelId);
            throw new InvalidOperationException($"No LLM client supports model: {modelId}");
        }

        return client;
    }

    /// <summary>
    /// Gets all available provider names.
    /// </summary>
    public IEnumerable<string> GetAvailableProviders()
    {
        return _clients.Select(c => c.ProviderName).Distinct(StringComparer.Ordinal);
    }
}
