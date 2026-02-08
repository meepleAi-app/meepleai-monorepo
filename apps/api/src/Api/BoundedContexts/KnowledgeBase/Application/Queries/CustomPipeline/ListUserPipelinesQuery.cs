using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.CustomPipeline;

/// <summary>
/// Query to list all custom pipelines for a user.
/// Issue #3453: Visual RAG Strategy Builder - Load functionality.
/// </summary>
public sealed record ListUserPipelinesQuery(
    Guid UserId,
    bool IncludePublished = true) : IRequest<IReadOnlyList<CustomPipelineData>>;
