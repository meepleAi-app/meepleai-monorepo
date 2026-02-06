using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.CustomPipeline;

/// <summary>
/// Command to save a new custom RAG pipeline.
/// Issue #3453: Visual RAG Strategy Builder - Save functionality.
/// </summary>
public sealed record SaveCustomPipelineCommand : IRequest<Guid>
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required PipelineDefinition Pipeline { get; init; }
    public required Guid UserId { get; init; }
    public bool IsPublished { get; init; }
    public string[] Tags { get; init; } = Array.Empty<string>();
}
