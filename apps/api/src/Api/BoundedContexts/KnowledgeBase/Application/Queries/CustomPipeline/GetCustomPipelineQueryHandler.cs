using Api.BoundedContexts.KnowledgeBase.Application.Queries.CustomPipeline;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.CustomPipeline;

/// <summary>
/// Handler for GetCustomPipelineQuery.
/// Issue #3453: Visual RAG Strategy Builder - Load functionality.
/// </summary>
internal sealed class GetCustomPipelineQueryHandler : IRequestHandler<GetCustomPipelineQuery, CustomPipelineData?>
{
    private readonly ICustomRagPipelineRepository _repository;
    private readonly ILogger<GetCustomPipelineQueryHandler> _logger;

    public GetCustomPipelineQueryHandler(
        ICustomRagPipelineRepository repository,
        ILogger<GetCustomPipelineQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CustomPipelineData?> Handle(
        GetCustomPipelineQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Loading custom RAG pipeline {PipelineId}", request.PipelineId);

        var pipeline = await _repository.GetByIdAsync(request.PipelineId, cancellationToken)
            .ConfigureAwait(false);

        if (pipeline == null)
        {
            _logger.LogWarning("Custom RAG pipeline {PipelineId} not found", request.PipelineId);
        }

        return pipeline;
    }
}
