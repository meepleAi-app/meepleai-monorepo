using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.BoundedContexts.GameToolkit.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.GameToolkit.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for GameToolkit bounded context.
/// </summary>
internal static class GameToolkitServiceExtensions
{
    /// <summary>
    /// Registers all GameToolkit bounded context services.
    /// </summary>
    public static IServiceCollection AddGameToolkitContext(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IGameToolkitRepository, GameToolkitRepository>();
        services.AddScoped<IToolkitRepository, ToolkitRepository>();

        // Register Unit of Work (shared across bounded contexts)
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
