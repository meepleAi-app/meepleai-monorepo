using Api.BoundedContexts.Administration.Domain.Services;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for <see cref="GetAllProvidersQuotaQuery"/>. Thin delegation to
/// <see cref="IProviderQuotaService.GetAllQuotasAsync"/> (auto-registered by MediatR). Issue #3043.
/// </summary>
internal sealed class GetAllProvidersQuotaQueryHandler
    : IQueryHandler<GetAllProvidersQuotaQuery, IReadOnlyList<ProviderQuotaDto>>
{
    private readonly IProviderQuotaService _quotaService;

    public GetAllProvidersQuotaQueryHandler(IProviderQuotaService quotaService)
        => _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));

    public Task<IReadOnlyList<ProviderQuotaDto>> Handle(GetAllProvidersQuotaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _quotaService.GetAllQuotasAsync(cancellationToken);
    }
}
