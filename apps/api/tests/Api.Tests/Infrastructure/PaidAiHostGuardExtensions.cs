using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Installs the <see cref="PaidAiHostGuardHandler"/> on every HttpClient produced by
/// IHttpClientFactory, enforcing REQ-AI-TEST-001 (automated tests must not consume AI tokens).
///
/// Uses <c>ConfigureHttpClientDefaults</c>, so the guard applies to all named clients — including
/// "OpenRouter" registered by the production DI — regardless of registration order. Call this from
/// every test WebApplicationFactory's <c>ConfigureTestServices</c>.
/// </summary>
internal static class PaidAiHostGuardExtensions
{
    public static IServiceCollection AddPaidAiHostGuard(this IServiceCollection services)
    {
        services.ConfigureHttpClientDefaults(builder =>
            builder.AddHttpMessageHandler(() => new PaidAiHostGuardHandler()));
        return services;
    }
}
