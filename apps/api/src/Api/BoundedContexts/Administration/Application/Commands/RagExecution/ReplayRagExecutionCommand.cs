using Api.BoundedContexts.Administration.Application.Commands.RagPipeline;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.RagExecution;

/// <summary>
/// Command to replay a past RAG execution with optional config overrides.
/// Returns SSE event stream (same format as TestRagPipeline).
/// Issue #4459: RAG Query Replay.
/// </summary>
public record ReplayRagExecutionCommand(
    Guid ExecutionId,
    Guid UserId,
    string? Strategy,
    int? TopK,
    string? Model,
    double? Temperature
) : IRequest<IAsyncEnumerable<RagPipelineTestEvent>>;
