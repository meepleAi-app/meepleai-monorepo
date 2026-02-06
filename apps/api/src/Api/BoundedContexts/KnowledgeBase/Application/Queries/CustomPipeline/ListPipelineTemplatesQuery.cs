using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.CustomPipeline;

/// <summary>
/// Query to list published template pipelines.
/// Issue #3453: Visual RAG Strategy Builder - Template library.
/// </summary>
public sealed record ListPipelineTemplatesQuery : IRequest<IReadOnlyList<CustomPipelineData>>;
