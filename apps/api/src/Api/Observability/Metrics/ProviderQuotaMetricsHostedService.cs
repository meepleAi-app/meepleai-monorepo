using Api.BoundedContexts.Administration.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Observability;

/// <summary>
/// Issue #985 (G4): wires <see cref="ProviderQuotaMetricsRegistrar"/> with the DI-resolved
/// <see cref="IProviderQuotaService"/> at startup.
///
/// ObservableGauge callbacks are static, so we initialize the static facade once. Holding
/// a reference past scope disposal is safe here because <see cref="IProviderQuotaService"/>
/// (via ProviderQuotaProviderFactory singleton + IHybridCacheService singleton) carries no
/// scoped state — verified at plan-review time.
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
        using var scope = _services.CreateScope();
        var quotaService = scope.ServiceProvider.GetRequiredService<IProviderQuotaService>();
        ProviderQuotaMetricsRegistrar.Initialize(quotaService, _logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
