using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using MediatR;
using Microsoft.Extensions.Hosting;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handler for GetTierRoutingQuery.
/// Issue #2596: LLM tier routing admin overview.
/// </summary>
internal sealed class GetTierRoutingQueryHandler : IRequestHandler<GetTierRoutingQuery, TierRoutingListDto>
{
    private readonly ILlmTierRoutingService _tierRoutingService;
    private readonly IHostEnvironment _hostEnvironment;

    public GetTierRoutingQueryHandler(
        ILlmTierRoutingService tierRoutingService,
        IHostEnvironment hostEnvironment)
    {
        _tierRoutingService = tierRoutingService;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<TierRoutingListDto> Handle(GetTierRoutingQuery request, CancellationToken cancellationToken)
    {
        var routings = await _tierRoutingService.GetAllTierRoutingsAsync(cancellationToken)
            .ConfigureAwait(false);

        var dtos = routings.Select(r => new TierRoutingDto
        {
            Tier = r.Tier,
            TierName = r.Tier.ToString(),
            ProductionModelId = r.ProductionModelId,
            ProductionModelName = r.ProductionModelName,
            ProductionProvider = r.ProductionProvider,
            TestModelId = r.TestModelId,
            TestModelName = r.TestModelName,
            TestProvider = r.TestProvider,
            EstimatedMonthlyCostUsd = r.EstimatedMonthlyCostUsd
        }).ToList();

        return new TierRoutingListDto
        {
            Routings = dtos,
            TotalCount = dtos.Count,
            CurrentEnvironment = _hostEnvironment.EnvironmentName
        };
    }
}
