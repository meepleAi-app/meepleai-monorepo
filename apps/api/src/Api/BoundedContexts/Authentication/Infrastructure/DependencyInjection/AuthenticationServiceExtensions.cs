using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.Authentication.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for Authentication bounded context.
/// </summary>
public static class AuthenticationServiceExtensions
{
    /// <summary>
    /// Registers all Authentication bounded context services.
    /// </summary>
    public static IServiceCollection AddAuthenticationContext(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
