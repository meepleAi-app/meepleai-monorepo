using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.CustomPipeline;

/// <summary>
/// Command to update an existing custom RAG pipeline.
/// Issue #3453: Visual RAG Strategy Builder - Update functionality.
/// </summary>
public sealed record UpdateCustomPipelineCommand : IRequest<Unit>
{
    public required Guid PipelineId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required PipelineDefinition Pipeline { get; init; }
    public bool IsPublished { get; init; }
    public string[] Tags { get; init; } = Array.Empty<string>();
}
