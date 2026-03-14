using Api.BoundedContexts.GameToolbox.Adapters;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using Api.BoundedContexts.GameToolbox.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.GameToolbox.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for GameToolbox bounded context.
/// Epic #412 — Game Toolbox.
/// </summary>
internal static class GameToolboxServiceExtensions
{
    public static IServiceCollection AddGameToolboxContext(this IServiceCollection services)
    {
        services.AddScoped<IToolboxRepository, ToolboxRepository>();
        services.AddScoped<IToolboxTemplateRepository, ToolboxTemplateRepository>();
        services.AddScoped<CardDeckAdapter>();
        return services;
    }
}
