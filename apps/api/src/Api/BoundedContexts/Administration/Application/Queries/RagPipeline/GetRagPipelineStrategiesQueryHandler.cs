using Api.BoundedContexts.Administration.Application.Queries.RagPipeline;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.RagPipeline;

/// <summary>
/// Handler for GetRagPipelineStrategiesQuery.
/// Issue #3464: Save/load/export for custom strategies.
/// </summary>
internal sealed class GetRagPipelineStrategiesQueryHandler(
    IRagPipelineStrategyRepository repository
) : IRequestHandler<GetRagPipelineStrategiesQuery, IReadOnlyList<RagPipelineStrategyDto>>
{
    public async Task<IReadOnlyList<RagPipelineStrategyDto>> Handle(
        GetRagPipelineStrategiesQuery request,
        CancellationToken cancellationToken)
    {
        var strategies = await repository.SearchAsync(
            request.SearchTerm,
            request.UserId,
            request.IncludeTemplates,
            cancellationToken).ConfigureAwait(false);

        return strategies.Select(s => new RagPipelineStrategyDto(
            s.Id,
            s.Name,
            s.Description,
            s.Version,
            s.IsTemplate,
            s.TemplateCategory,
            s.GetTags(),
            s.IsActive,
            s.CreatedAt,
            s.UpdatedAt
        )).ToList();
    }
}
