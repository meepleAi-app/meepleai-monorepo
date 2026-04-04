using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.AgentMemory.Infrastructure.Persistence;
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
        // Repositories
        services.AddScoped<IGameMemoryRepository, GameMemoryRepository>();
        services.AddScoped<IGroupMemoryRepository, GroupMemoryRepository>();
        services.AddScoped<IPlayerMemoryRepository, PlayerMemoryRepository>();

        return services;
    }
}
