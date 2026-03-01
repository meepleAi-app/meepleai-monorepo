using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.BoundedContexts.EntityRelationships.Infrastructure.Persistence;
using Api.BoundedContexts.EntityRelationships.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.EntityRelationships.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for EntityRelationships bounded context.
/// Issue #5130: BC scaffold registration.
/// Issue #5132: Repository registration added.
/// </summary>
internal static class EntityRelationshipsServiceExtensions
{
    /// <summary>
    /// Registers all EntityRelationships bounded context services.
    /// </summary>
    public static IServiceCollection AddEntityRelationshipsContext(this IServiceCollection services)
    {
        services.AddScoped<IEntityLinkRepository, EntityLinkRepository>();
        services.AddScoped<IBggExpansionImporter, BggExpansionImporter>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
