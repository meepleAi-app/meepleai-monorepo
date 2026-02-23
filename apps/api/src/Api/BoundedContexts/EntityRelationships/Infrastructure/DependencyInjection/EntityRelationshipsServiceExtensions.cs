using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.EntityRelationships.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for EntityRelationships bounded context.
/// Issue #5130: BC scaffold registration.
/// </summary>
internal static class EntityRelationshipsServiceExtensions
{
    /// <summary>
    /// Registers all EntityRelationships bounded context services.
    /// Repositories will be registered in Issue #5132 (EF Core + migration).
    /// </summary>
    public static IServiceCollection AddEntityRelationshipsContext(this IServiceCollection services)
    {
        // Repositories registered in Issue #5132 (EntityLinkRepository)
        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
