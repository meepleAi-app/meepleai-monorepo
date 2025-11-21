using Api.BoundedContexts.KnowledgeBase.Domain.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Interface for provider health check service
/// </summary>
public interface IProviderHealthCheckService
{
    /// <summary>
    /// Gets the health status for a specific provider
    /// </summary>
    ProviderHealthStatus? GetProviderHealth(string providerName);

    /// <summary>
    /// Gets health statuses for all providers
    /// </summary>
    Dictionary<string, ProviderHealthStatus> GetAllProviderHealth();
}
