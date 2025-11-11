using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.DependencyInjection;

public static class SystemConfigurationServiceExtensions
{
    public static IServiceCollection AddSystemConfigurationContext(this IServiceCollection services)
    {
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        return services;
    }
}
