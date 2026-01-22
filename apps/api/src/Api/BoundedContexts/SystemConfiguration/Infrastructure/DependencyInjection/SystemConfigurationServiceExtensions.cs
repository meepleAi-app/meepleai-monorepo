using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Services;
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
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<IRateLimitConfigRepository, RateLimitConfigRepository>(); // Issue #2730: Rate limit config
        services.AddScoped<IUserRateLimitOverrideRepository, UserRateLimitOverrideRepository>(); // Issue #2730: User overrides

        // Register domain services
        services.AddScoped<ConfigurationValidator>();
        services.AddScoped<IRateLimitEvaluator, RateLimitEvaluator>(); // Issue #2724: CreateShareRequest (updated #2730)

        // Issue #2596: LLM tier routing service (Singleton - uses IServiceScopeFactory for DB access)
        // Registered as Singleton for use by HybridAdaptiveRoutingStrategy
        services.AddSingleton<ILlmTierRoutingService, LlmTierRoutingService>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
