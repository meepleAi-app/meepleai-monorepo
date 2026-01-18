using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Services;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.BoundedContexts.UserLibrary.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for UserLibrary bounded context.
/// </summary>
internal static class UserLibraryServiceExtensions
{
    /// <summary>
    /// Registers all UserLibrary bounded context services.
    /// </summary>
    public static IServiceCollection AddUserLibraryContext(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IUserLibraryRepository, UserLibraryRepository>();

        // Register domain services
        services.AddScoped<IGameLibraryQuotaService, GameLibraryQuotaService>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
