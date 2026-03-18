using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.AgentMemory.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering AgentMemory bounded context services.
/// </summary>
internal static class AgentMemoryServiceExtensions
{
    /// <summary>
    /// Registers all AgentMemory bounded context services.
    /// </summary>
    public static IServiceCollection AddAgentMemoryContext(this IServiceCollection services)
    {
        // Repositories will be registered here in Task 20
        return services;
    }
}
