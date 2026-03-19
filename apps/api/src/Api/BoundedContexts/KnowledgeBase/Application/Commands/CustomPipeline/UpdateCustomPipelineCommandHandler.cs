using Api.BoundedContexts.KnowledgeBase.Application.Commands.CustomPipeline;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers.CustomPipeline;

/// <summary>
/// Handler for UpdateCustomPipelineCommand.
/// Issue #3453: Visual RAG Strategy Builder - Update functionality.
/// </summary>
internal sealed class UpdateCustomPipelineCommandHandler : IRequestHandler<UpdateCustomPipelineCommand, Unit>
{
    private readonly ICustomRagPipelineRepository _repository;
    private readonly ILogger<UpdateCustomPipelineCommandHandler> _logger;

    public UpdateCustomPipelineCommandHandler(
        ICustomRagPipelineRepository repository,
        ILogger<UpdateCustomPipelineCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(
        UpdateCustomPipelineCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Updating custom RAG pipeline {PipelineId}",
            request.PipelineId);

        await _repository.UpdateAsync(
            request.PipelineId,
            request.Name,
            request.Description,
            request.Pipeline,
            request.IsPublished,
            request.Tags,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Custom RAG pipeline {PipelineId} updated successfully",
            request.PipelineId);

        return Unit.Value;
    }
}
