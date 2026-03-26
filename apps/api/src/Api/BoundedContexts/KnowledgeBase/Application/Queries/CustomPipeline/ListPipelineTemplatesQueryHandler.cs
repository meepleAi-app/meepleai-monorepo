using Api.BoundedContexts.KnowledgeBase.Application.Queries.CustomPipeline;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.CustomPipeline;

/// <summary>
/// Handler for ListPipelineTemplatesQuery.
/// Issue #3453: Visual RAG Strategy Builder - Template library.
/// </summary>
internal sealed class ListPipelineTemplatesQueryHandler
    : IRequestHandler<ListPipelineTemplatesQuery, IReadOnlyList<CustomPipelineData>>
{
    private readonly ICustomRagPipelineRepository _repository;
    private readonly ILogger<ListPipelineTemplatesQueryHandler> _logger;

    public ListPipelineTemplatesQueryHandler(
        ICustomRagPipelineRepository repository,
        ILogger<ListPipelineTemplatesQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<CustomPipelineData>> Handle(
        ListPipelineTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Listing custom RAG pipeline templates");

        var templates = await _repository.GetTemplatesAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Retrieved {Count} pipeline templates", templates.Count);

        return templates;
    }
}
