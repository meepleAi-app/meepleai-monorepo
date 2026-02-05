using Api.BoundedContexts.Administration.Application.Commands.RagPipeline;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers.RagPipeline;

/// <summary>
/// Handler for DeleteRagPipelineStrategyCommand.
/// Issue #3464: Save/load/export for custom strategies.
/// </summary>
internal sealed class DeleteRagPipelineStrategyCommandHandler(
    IRagPipelineStrategyRepository repository,
    ILogger<DeleteRagPipelineStrategyCommandHandler> logger
) : IRequestHandler<DeleteRagPipelineStrategyCommand, bool>
{
    public async Task<bool> Handle(
        DeleteRagPipelineStrategyCommand request,
        CancellationToken cancellationToken)
    {
        var strategy = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Strategy with ID {request.Id} not found.");

        // Verify ownership (templates can only be deleted by admins - handled at endpoint level)
        if (strategy.CreatedByUserId != request.UserId && !strategy.IsTemplate)
        {
            throw new ForbiddenException("You can only delete your own strategies.");
        }

        await repository.DeleteAsync(strategy, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Deleted RAG pipeline strategy {StrategyId} by user {UserId}",
            request.Id,
            request.UserId);

        return true;
    }
}
