using Api.BoundedContexts.Administration.Application.Queries.TokenManagement;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.TokenManagement;

/// <summary>
/// Handler for GetTokenTiersQuery (Issue #3787)
/// </summary>
internal sealed class GetTokenTiersQueryHandler : IRequestHandler<GetTokenTiersQuery, List<TierConfigDto>>
{
    private readonly ITokenTierRepository _repository;

    public GetTokenTiersQueryHandler(ITokenTierRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<TierConfigDto>> Handle(GetTokenTiersQuery request, CancellationToken cancellationToken)
    {
        var tiers = await _repository.GetAllTiersAsync(cancellationToken).ConfigureAwait(false);

        return tiers.Select(t => new TierConfigDto(
            Name: t.Name.ToString(),
            TokensPerMonth: t.Limits.TokensPerMonth,
            TokensPerDay: t.Limits.TokensPerDay,
            MessagesPerDay: t.Limits.MessagesPerDay,
            MonthlyPrice: t.Pricing.MonthlyFee,
            IsActive: t.IsActive
        )).ToList();
    }
}
