using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.CustomPipeline;

/// <summary>
/// Command to delete a custom RAG pipeline.
/// Issue #3453: Visual RAG Strategy Builder - Delete functionality.
/// </summary>
public sealed record DeleteCustomPipelineCommand(
    Guid PipelineId,
    Guid UserId) : IRequest<bool>;
