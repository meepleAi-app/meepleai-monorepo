using Api.BoundedContexts.KnowledgeBase.Application.Commands.CustomPipeline;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers.CustomPipeline;

/// <summary>
/// Handler for SaveCustomPipelineCommand.
/// Issue #3453: Visual RAG Strategy Builder - Save functionality.
/// </summary>
internal sealed class SaveCustomPipelineCommandHandler : IRequestHandler<SaveCustomPipelineCommand, Guid>
{
    private readonly ICustomRagPipelineRepository _repository;
    private readonly ILogger<SaveCustomPipelineCommandHandler> _logger;

    public SaveCustomPipelineCommandHandler(
        ICustomRagPipelineRepository repository,
        ILogger<SaveCustomPipelineCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(
        SaveCustomPipelineCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Saving custom RAG pipeline '{Name}' for user {UserId}",
            request.Name,
            request.UserId);

        var pipelineId = await _repository.SaveAsync(
            request.Name,
            request.Description,
            request.Pipeline,
            request.UserId,
            request.IsPublished,
            request.Tags,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Custom RAG pipeline saved with ID {PipelineId}",
            pipelineId);

        return pipelineId;
    }
}
