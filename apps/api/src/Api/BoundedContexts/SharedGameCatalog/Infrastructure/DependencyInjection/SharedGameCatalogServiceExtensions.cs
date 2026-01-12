using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for SharedGameCatalog bounded context.
/// </summary>
internal static class SharedGameCatalogServiceExtensions
{
    /// <summary>
    /// Registers all SharedGameCatalog bounded context services.
    /// Issue #2370 Phase 1
    /// </summary>
    public static IServiceCollection AddSharedGameCatalogContext(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();
        services.AddScoped<ISharedGameDeleteRequestRepository, SharedGameDeleteRequestRepository>();

        // Register Unit of Work (shared across bounded contexts)
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
