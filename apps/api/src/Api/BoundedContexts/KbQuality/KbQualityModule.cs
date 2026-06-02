using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.KbQuality;

/// <summary>
/// DI registration entry point for the KbQuality bounded context (#1675).
/// Wires aggregate repo, application services, MediatR behaviors, ports/adapters,
/// background jobs, and configuration options.
/// </summary>
public static class KbQualityModule
{
    public static IServiceCollection AddKbQualityModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Services + behaviors + adapters registered task-by-task; placeholder for now.
        return services;
    }
}
