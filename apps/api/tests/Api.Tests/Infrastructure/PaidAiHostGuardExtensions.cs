using Api.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Test-only DI wiring that enforces REQ-AI-TEST-001 (automated tests must not consume AI tokens).
/// </summary>
internal static class PaidAiHostGuardExtensions
{
    /// <summary>
    /// Installs the <see cref="PaidAiHostGuardHandler"/> on every HttpClient produced by
    /// IHttpClientFactory. Uses <c>ConfigureHttpClientDefaults</c>, so the guard applies to all named
    /// clients — including "OpenRouter" registered by the production DI — regardless of registration
    /// order.
    /// </summary>
    public static IServiceCollection AddPaidAiHostGuard(this IServiceCollection services)
    {
        services.ConfigureHttpClientDefaults(builder =>
            builder.AddHttpMessageHandler(() => new PaidAiHostGuardHandler()));
        return services;
    }

    /// <summary>
    /// Registers BOTH fail-closed layers as a single unit so they cannot drift apart between
    /// factories: (1) the HTTP-level <see cref="PaidAiHostGuardHandler"/> (covers every paid path),
    /// and (2) the service-level network-free <see cref="TestFailingLlmService"/> fake.
    ///
    /// Call from a test WebApplicationFactory. A test that needs a positive LLM response overrides
    /// <c>ILlmService</c> in its own <c>WithWebHostBuilder(...).ConfigureTestServices</c>, which runs
    /// after the factory and therefore wins (e.g. GlobalKbAskStreamEndpointTests).
    /// </summary>
    public static IServiceCollection AddFailClosedAiTestDoubles(this IServiceCollection services)
    {
        services.AddPaidAiHostGuard();
        services.RemoveAll(typeof(Api.Services.ILlmService));
        services.AddScoped<Api.Services.ILlmService, TestFailingLlmService>();
        return services;
    }
}
