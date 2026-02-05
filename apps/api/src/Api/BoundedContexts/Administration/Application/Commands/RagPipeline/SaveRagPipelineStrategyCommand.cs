using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.RagPipeline;

/// <summary>
/// Command to save a RAG pipeline strategy.
/// Issue #3464: Save/load/export for custom strategies.
/// </summary>
public sealed record SaveRagPipelineStrategyCommand(
    Guid? Id,
    string Name,
    string Description,
    string NodesJson,
    string EdgesJson,
    Guid UserId,
    IEnumerable<string>? Tags = null
) : IRequest<SaveRagPipelineStrategyResult>;

/// <summary>
/// Result of saving a RAG pipeline strategy.
/// </summary>
public sealed record SaveRagPipelineStrategyResult(
    Guid Id,
    string Name,
    string Version,
    DateTime UpdatedAt
);
