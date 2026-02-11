using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Handler for getting saved cost scenarios for a user.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal sealed class GetCostScenariosQueryHandler
    : IQueryHandler<GetCostScenariosQuery, CostScenariosResponseDto>
{
    private readonly ICostScenarioRepository _repository;

    public GetCostScenariosQueryHandler(ICostScenarioRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<CostScenariosResponseDto> Handle(
        GetCostScenariosQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (scenarios, total) = await _repository.GetByUserAsync(
            userId: query.UserId,
            page: query.Page,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var dtos = scenarios.Select(CostScenarioDto.FromEntity).ToList();

        return new CostScenariosResponseDto(dtos, total, query.Page, query.PageSize);
    }
}
