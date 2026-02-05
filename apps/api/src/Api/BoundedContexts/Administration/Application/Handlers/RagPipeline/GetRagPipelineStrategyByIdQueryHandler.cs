using Api.BoundedContexts.Administration.Application.Queries.RagPipeline;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.RagPipeline;

/// <summary>
/// Handler for GetRagPipelineStrategyByIdQuery.
/// Issue #3464: Save/load/export for custom strategies.
/// </summary>
internal sealed class GetRagPipelineStrategyByIdQueryHandler(
    IRagPipelineStrategyRepository repository
) : IRequestHandler<GetRagPipelineStrategyByIdQuery, RagPipelineStrategyDetailDto?>
{
    public async Task<RagPipelineStrategyDetailDto?> Handle(
        GetRagPipelineStrategyByIdQuery request,
        CancellationToken cancellationToken)
    {
        var strategy = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (strategy == null)
        {
            return null;
        }

        // Allow access to own strategies or templates
        if (strategy.CreatedByUserId != request.UserId && !strategy.IsTemplate)
        {
            return null;
        }

        return new RagPipelineStrategyDetailDto(
            strategy.Id,
            strategy.Name,
            strategy.Description,
            strategy.Version,
            strategy.NodesJson,
            strategy.EdgesJson,
            strategy.IsTemplate,
            strategy.TemplateCategory,
            strategy.GetTags(),
            strategy.IsActive,
            strategy.CreatedByUserId,
            strategy.CreatedAt,
            strategy.UpdatedAt
        );
    }
}
