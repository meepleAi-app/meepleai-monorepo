using Api.BoundedContexts.Administration.Domain.Services;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

internal sealed class GetProviderQuotaQueryHandler : IQueryHandler<GetProviderQuotaQuery, ProviderQuotaDto>
{
    private readonly IProviderQuotaService _quotaService;

    public GetProviderQuotaQueryHandler(IProviderQuotaService quotaService) => _quotaService = quotaService;

    public Task<ProviderQuotaDto> Handle(GetProviderQuotaQuery query, CancellationToken cancellationToken)
        => _quotaService.GetQuotaAsync(query.ProviderName, cancellationToken);
}
