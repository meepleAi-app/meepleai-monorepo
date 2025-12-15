using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.DependencyInjection;

internal static class SystemConfigurationServiceExtensions
{
    public static IServiceCollection AddSystemConfigurationContext(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Domain Services
        services.AddScoped<ConfigurationValidator>();

        return services;
    }
}
