using System.Collections.Generic;
using System.Linq;
using Api.DevTools.Scenarios;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Api.DevTools;

internal static class DevToolsServiceCollectionExtensions
{
    /// <summary>
    /// Registers MeepleDev mock-aware services. Call only when env.IsDevelopment().
    /// Concrete mock-aware proxy registrations for ILlmService, IEmbeddingService,
    /// and IBlobStorageService are added in Task 22.
    /// </summary>
    public static IServiceCollection AddMeepleDevTools(this IServiceCollection services)
    {
        var env = System.Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(
                e => (string)e.Key,
                e => e.Value?.ToString(),
                System.StringComparer.OrdinalIgnoreCase);

        var provider = new MockToggleStateProvider(env!, KnownMockServices.All);
        services.AddSingleton(provider);
        services.AddSingleton<IMockToggleReader>(_ => provider);
        services.AddSingleton<IMockToggleWriter>(_ => provider);
        services.AddSingleton<IMockToggleEvents>(_ => provider);
        services.AddSingleton<ScenarioLoader>();

        // Mock service wiring (proxies) is added in Task 22.
        return services;
    }

    /// <summary>
    /// Adds the middleware that writes X-Meeple-Mock response headers.
    /// </summary>
    public static IApplicationBuilder UseMeepleDevTools(this IApplicationBuilder app)
    {
        return app.UseMiddleware<MockHeaderMiddleware>();
    }
}
