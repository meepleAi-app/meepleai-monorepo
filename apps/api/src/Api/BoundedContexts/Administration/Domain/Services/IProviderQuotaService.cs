using Api.Models;

namespace Api.BoundedContexts.Administration.Domain.Services;

internal interface IProviderQuotaService
{
    Task<ProviderQuotaDto> GetQuotaAsync(string providerName, CancellationToken cancellationToken);
}
