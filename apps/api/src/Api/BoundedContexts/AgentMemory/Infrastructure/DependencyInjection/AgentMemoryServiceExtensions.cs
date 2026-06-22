using Api.BoundedContexts.AgentMemory.Application.Services;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.AgentMemory.Infrastructure.Persistence;
using Api.BoundedContexts.AgentMemory.Infrastructure.Services;
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

        // Cross-BC read services (issue #2492 — SP6 §F drawer metadata)
        services.AddScoped<IKbChunkCountReader, KbChunkCountReader>();

        return services;
    }
}
