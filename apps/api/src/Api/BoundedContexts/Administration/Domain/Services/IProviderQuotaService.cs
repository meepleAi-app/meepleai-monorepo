using Api.Models;

namespace Api.BoundedContexts.Administration.Domain.Services;

internal interface IProviderQuotaService
{
    Task<ProviderQuotaDto> GetQuotaAsync(string providerName, CancellationToken cancellationToken);

    /// <summary>
    /// Aggregated quota across all quota-capable providers (SupportedProviderNames).
    /// Issue #3043. Reuses the per-provider HybridCache (5min).
    /// </summary>
    Task<IReadOnlyList<ProviderQuotaDto>> GetAllQuotasAsync(CancellationToken cancellationToken);
}
