using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
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
        services.AddScoped<ISharedGameDocumentRepository, SharedGameDocumentRepository>(); // Issue #2391 Sprint 1

        // Register domain services
        services.AddScoped<DocumentVersioningService>(); // Issue #2391 Sprint 1

        // Register Unit of Work (shared across bounded contexts)
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }

    /// <summary>
    /// Registers authorization policies for SharedGameCatalog endpoints.
    /// Issue #2371 Phase 2
    /// </summary>
    public static IServiceCollection AddSharedGameCatalogPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOrEditorPolicy", policy =>
                policy.RequireRole("Admin", "Editor"))
            .AddPolicy("AdminOnlyPolicy", policy =>
                policy.RequireRole("Admin"));

        return services;
    }
}
