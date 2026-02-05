using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.RagPipeline;

/// <summary>
/// Command to delete a RAG pipeline strategy.
/// Issue #3464: Save/load/export for custom strategies.
/// </summary>
public sealed record DeleteRagPipelineStrategyCommand(
    Guid Id,
    Guid UserId
) : IRequest<bool>;
