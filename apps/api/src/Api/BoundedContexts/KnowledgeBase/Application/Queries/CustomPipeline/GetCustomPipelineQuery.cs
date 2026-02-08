using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.CustomPipeline;

/// <summary>
/// Query to load a custom RAG pipeline by ID.
/// Issue #3453: Visual RAG Strategy Builder - Load functionality.
/// </summary>
public sealed record GetCustomPipelineQuery(Guid PipelineId) : IRequest<CustomPipelineData?>;
