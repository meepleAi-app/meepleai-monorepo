using Api.BoundedContexts.KnowledgeBase.Application.Commands.CustomPipeline;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.CustomPipeline;

/// <summary>
/// Handler for DeleteCustomPipelineCommand.
/// Issue #3453: Visual RAG Strategy Builder - Delete functionality.
/// </summary>
internal sealed class DeleteCustomPipelineCommandHandler : IRequestHandler<DeleteCustomPipelineCommand, bool>
{
    private readonly ICustomRagPipelineRepository _repository;
    private readonly ILogger<DeleteCustomPipelineCommandHandler> _logger;

    public DeleteCustomPipelineCommandHandler(
        ICustomRagPipelineRepository repository,
        ILogger<DeleteCustomPipelineCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(
        DeleteCustomPipelineCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Deleting custom RAG pipeline {PipelineId} for user {UserId}",
            request.PipelineId,
            request.UserId);

        var deleted = await _repository.DeleteAsync(
            request.PipelineId,
            request.UserId,
            cancellationToken).ConfigureAwait(false);

        if (deleted)
        {
            _logger.LogInformation("Custom RAG pipeline {PipelineId} deleted", request.PipelineId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to delete pipeline {PipelineId} - not found or unauthorized",
                request.PipelineId);
        }

        return deleted;
    }
}
