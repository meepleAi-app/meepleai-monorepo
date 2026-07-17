using Api.BoundedContexts.Administration.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Observability;

/// <summary>
/// Issue #985 (G4): wires <see cref="ProviderQuotaMetricsRegistrar"/> with the DI-resolved
/// <see cref="IProviderQuotaService"/> at startup.
///
/// ObservableGauge callbacks are static, so we initialize the static facade once — with the
/// <see cref="IServiceScopeFactory"/>. Since #3044 <see cref="IProviderQuotaService"/> depends
/// (transitively, via IProviderCredentialResolver) on the scoped MeepleAiDbContext, so the
/// registrar opens a fresh scope per gauge scrape rather than holding a service past scope disposal.
/// </summary>
internal sealed class ProviderQuotaMetricsHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ProviderQuotaMetricsHostedService> _logger;

    public ProviderQuotaMetricsHostedService(
        IServiceProvider services,
        ILogger<ProviderQuotaMetricsHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // #3044: pass the scope FACTORY, not a resolved service. IProviderQuotaService now depends
        // (transitively) on the scoped MeepleAiDbContext, so the registrar must open a fresh scope
        // per gauge scrape instead of holding a service past scope disposal.
        var scopeFactory = _services.GetRequiredService<IServiceScopeFactory>();
        ProviderQuotaMetricsRegistrar.Initialize(scopeFactory, _logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
