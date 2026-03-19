using Api.BoundedContexts.KnowledgeBase.Application.Queries.CustomPipeline;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.CustomPipeline;

/// <summary>
/// Handler for ListUserPipelinesQuery.
/// Issue #3453: Visual RAG Strategy Builder - Load functionality.
/// </summary>
internal sealed class ListUserPipelinesQueryHandler
    : IRequestHandler<ListUserPipelinesQuery, IReadOnlyList<CustomPipelineData>>
{
    private readonly ICustomRagPipelineRepository _repository;
    private readonly ILogger<ListUserPipelinesQueryHandler> _logger;

    public ListUserPipelinesQueryHandler(
        ICustomRagPipelineRepository repository,
        ILogger<ListUserPipelinesQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<CustomPipelineData>> Handle(
        ListUserPipelinesQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Listing custom RAG pipelines for user {UserId} (includePublished: {IncludePublished})",
            request.UserId,
            request.IncludePublished);

        var pipelines = await _repository.GetUserPipelinesAsync(
            request.UserId,
            request.IncludePublished,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Retrieved {Count} custom RAG pipelines for user {UserId}",
            pipelines.Count,
            request.UserId);

        return pipelines;
    }
}
