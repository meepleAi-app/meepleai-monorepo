using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Issue #3304: RAG Dashboard commands for configuration management.
/// </summary>

/// <summary>
/// Command to save RAG configuration.
/// </summary>
public sealed record SaveRagConfigCommand : ICommand<RagConfigDto>
{
    public required Guid UserId { get; init; }
    public required RagConfigDto Config { get; init; }
}

/// <summary>
/// Command to reset RAG configuration to defaults.
/// </summary>
public sealed record ResetRagConfigCommand : ICommand<RagConfigDto>
{
    public required Guid UserId { get; init; }
    public string? Strategy { get; init; } // null = reset all strategies
}
