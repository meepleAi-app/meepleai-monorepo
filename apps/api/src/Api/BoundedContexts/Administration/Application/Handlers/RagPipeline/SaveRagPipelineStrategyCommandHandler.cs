using Api.BoundedContexts.Administration.Application.Commands.RagPipeline;
using Api.BoundedContexts.Administration.Domain.Aggregates.RagPipelineStrategy;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers.RagPipeline;

/// <summary>
/// Handler for SaveRagPipelineStrategyCommand.
/// Issue #3464: Save/load/export for custom strategies.
/// </summary>
internal sealed class SaveRagPipelineStrategyCommandHandler(
    IRagPipelineStrategyRepository repository,
    ILogger<SaveRagPipelineStrategyCommandHandler> logger
) : IRequestHandler<SaveRagPipelineStrategyCommand, SaveRagPipelineStrategyResult>
{
    public async Task<SaveRagPipelineStrategyResult> Handle(
        SaveRagPipelineStrategyCommand request,
        CancellationToken cancellationToken)
    {
        // Check for duplicate name
        if (await repository.ExistsByNameAsync(request.Name, request.UserId, request.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"A strategy with name '{request.Name}' already exists.");
        }

        RagPipelineStrategy strategy;

        if (request.Id.HasValue)
        {
            // Update existing
            strategy = await repository.GetByIdAsync(request.Id.Value, cancellationToken).ConfigureAwait(false)
                ?? throw new NotFoundException($"Strategy with ID {request.Id.Value} not found.");

            // Verify ownership
            if (strategy.CreatedByUserId != request.UserId)
            {
                throw new ForbiddenException("You can only update your own strategies.");
            }

            strategy.Update(
                request.Name,
                request.Description,
                request.NodesJson,
                request.EdgesJson);

            if (request.Tags != null)
            {
                strategy.UpdateMetadata(request.Name, request.Description, strategy.IsActive, request.Tags);
            }

            await repository.UpdateAsync(strategy, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Updated RAG pipeline strategy {StrategyId} for user {UserId}",
                strategy.Id,
                request.UserId);
        }
        else
        {
            // Create new
            strategy = RagPipelineStrategy.Create(
                request.Name,
                request.Description,
                request.NodesJson,
                request.EdgesJson,
                request.UserId,
                tags: request.Tags);

            await repository.AddAsync(strategy, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Created new RAG pipeline strategy {StrategyId} for user {UserId}",
                strategy.Id,
                request.UserId);
        }

        return new SaveRagPipelineStrategyResult(
            strategy.Id,
            strategy.Name,
            strategy.Version,
            strategy.UpdatedAt);
    }
}
