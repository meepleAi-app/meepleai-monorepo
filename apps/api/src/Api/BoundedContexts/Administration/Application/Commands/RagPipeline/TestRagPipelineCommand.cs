using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.RagPipeline;

/// <summary>
/// Command to test a RAG pipeline with SSE streaming results.
/// Issue #3463: Live test API with SSE streaming for visual strategy builder.
/// </summary>
/// <param name="PipelineDefinition">JSON pipeline definition from visual builder</param>
/// <param name="TestQuery">Sample query to test the pipeline</param>
/// <param name="UserId">User executing the test</param>
public record TestRagPipelineCommand(
    string PipelineDefinition,
    string TestQuery,
    Guid UserId
) : IRequest<IAsyncEnumerable<RagPipelineTestEvent>>;

/// <summary>
/// Base class for pipeline test events streamed via SSE.
/// </summary>
public abstract record RagPipelineTestEvent
{
    public required string EventType { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Event when pipeline test starts.
/// </summary>
public record PipelineTestStartedEvent : RagPipelineTestEvent
{
    public required string PipelineId { get; init; }
    public required string Query { get; init; }
    public required int TotalBlocks { get; init; }
}

/// <summary>
/// Event when a block execution starts.
/// </summary>
public record BlockExecutionStartedEvent : RagPipelineTestEvent
{
    public required string BlockId { get; init; }
    public required string BlockType { get; init; }
    public required string BlockName { get; init; }
    public required int BlockIndex { get; init; }
}

/// <summary>
/// Event when a block execution completes.
/// </summary>
public record BlockExecutionCompletedEvent : RagPipelineTestEvent
{
    public required string BlockId { get; init; }
    public required string BlockType { get; init; }
    public required bool Success { get; init; }
    public required int DurationMs { get; init; }
    public required int TokensUsed { get; init; }
    public decimal Cost { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Event with retrieved documents from a retrieval block.
/// </summary>
public record DocumentsRetrievedEvent : RagPipelineTestEvent
{
    public required string BlockId { get; init; }
    public required int DocumentCount { get; init; }
    public required IReadOnlyList<RetrievedDocumentDto> Documents { get; init; }
}

/// <summary>
/// Event with validation/evaluation results.
/// </summary>
public record ValidationResultEvent : RagPipelineTestEvent
{
    public required string BlockId { get; init; }
    public required string ValidationType { get; init; }
    public required bool Passed { get; init; }
    public required double Score { get; init; }
    public string? Details { get; init; }
}

/// <summary>
/// Event when pipeline test completes.
/// </summary>
public record PipelineTestCompletedEvent : RagPipelineTestEvent
{
    public required bool Success { get; init; }
    public required int TotalDurationMs { get; init; }
    public required int TotalTokensUsed { get; init; }
    public required decimal TotalCost { get; init; }
    public required int BlocksExecuted { get; init; }
    public required int BlocksFailed { get; init; }
    public string? FinalResponse { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// DTO for retrieved document in test results.
/// </summary>
public record RetrievedDocumentDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required double Score { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
