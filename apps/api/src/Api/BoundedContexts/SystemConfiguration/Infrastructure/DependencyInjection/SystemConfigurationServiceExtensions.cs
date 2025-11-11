using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.DependencyInjection;

public static class SystemConfigurationServiceExtensions
{
    public static IServiceCollection AddSystemConfigurationContext(this IServiceCollection services)
    {
        // Note: Repositories not yet implemented, will be added when ConfigurationRepository is created
        // services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        // services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        return services;
    }
}
