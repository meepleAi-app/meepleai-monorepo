using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for SystemConfiguration bounded context.
/// </summary>
internal static class SystemConfigurationServiceExtensions
{
    /// <summary>
    /// Registers all SystemConfiguration bounded context services.
    /// </summary>
    public static IServiceCollection AddSystemConfigurationContext(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IAiModelConfigurationRepository, EfAiModelConfigurationRepository>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
