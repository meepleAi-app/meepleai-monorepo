using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.External.Reranking;
using Api.DevTools.MockImpls;
using Api.DevTools.Scenarios;
using Api.Services;
using Api.Services.Pdf;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.DevTools;

internal static class DevToolsServiceCollectionExtensions
{
    /// <summary>
    /// Registers MeepleDev mock-aware services. Call only when env.IsDevelopment().
    /// Replaces real ILlmService, IEmbeddingService, and IBlobStorageService registrations
    /// with mock-aware proxies that dispatch at runtime based on MOCK_* env-var toggles.
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

        // Mock-aware proxies for the 3 services covered in PR #3.
        // Lifetime MUST match the existing real service registration (Scoped per bounded contexts).
        services.AddMockAwareService<ILlmService, HybridLlmService, MockLlmService>(
            "llm", ServiceLifetime.Scoped);
        services.AddMockAwareService<IEmbeddingService, EmbeddingService, MockEmbeddingService>(
            "embedding", ServiceLifetime.Scoped);
        services.AddMockAwareService<IBggApiService, BggApiService, MockBggApiService>(
            "bgg", ServiceLifetime.Scoped);

        // IBlobStorageService is registered via BlobStorageServiceFactory (not a constructible
        // concrete type), so we handle the proxy registration inline rather than using
        // AddMockAwareService<>, which requires TReal to be directly constructible by the DI container.
        services.AddScoped<MockBlobStorageService>();
        var existingBlob = services.FirstOrDefault(d => d.ServiceType == typeof(IBlobStorageService));
        if (existingBlob is not null)
        {
            services.Remove(existingBlob);
        }

        services.AddScoped<IBlobStorageService>(sp =>
        {
            var realBlob = BlobStorageServiceFactory.Create(sp);
            var mockBlob = sp.GetRequiredService<MockBlobStorageService>();
            var toggles = sp.GetRequiredService<IMockToggleReader>();
            return MockAwareProxy<IBlobStorageService>.Create(realBlob, mockBlob, toggles, "s3");
        });

        // ICrossEncoderReranker is registered via AddHttpClient (typed-client factory), so it
        // cannot be wrapped by AddMockAwareService<> (which requires TReal to be directly
        // constructible). We use the inline proxy pattern instead: resolve the real client
        // through the existing typed-client registration, then wrap it with the mock proxy.
        services.AddScoped<MockRerankerService>();
        var existingReranker = services.FirstOrDefault(d => d.ServiceType == typeof(ICrossEncoderReranker));
        if (existingReranker is not null)
        {
            services.Remove(existingReranker);
        }

        services.AddScoped<ICrossEncoderReranker>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            // The typed-client name for AddHttpClient<TInterface, TImpl> is typeof(TImpl).Name.
            var httpClient = httpClientFactory.CreateClient(nameof(CrossEncoderRerankerClient));
            var logger = sp.GetRequiredService<ILogger<CrossEncoderRerankerClient>>();
            var options = sp.GetRequiredService<IOptions<RerankerClientOptions>>();
            var realReranker = new CrossEncoderRerankerClient(httpClient, logger, options);
            var mockReranker = sp.GetRequiredService<MockRerankerService>();
            var toggles = sp.GetRequiredService<IMockToggleReader>();
            return MockAwareProxy<ICrossEncoderReranker>.Create(realReranker, mockReranker, toggles, "reranker");
        });

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
