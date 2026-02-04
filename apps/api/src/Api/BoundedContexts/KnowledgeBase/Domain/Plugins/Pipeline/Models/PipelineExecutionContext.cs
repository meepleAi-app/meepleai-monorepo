// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3415 - DAG Orchestrator
// =============================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;

/// <summary>
/// Execution context that maintains state during pipeline execution.
/// Provides thread-safe access to shared data between nodes.
/// </summary>
public sealed class PipelineExecutionContext
{
    private readonly ConcurrentDictionary<string, PluginOutput> _nodeOutputs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, NodeExecutionState> _nodeStates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, object> _metadata = new(StringComparer.Ordinal);

    /// <summary>
    /// Unique identifier for this execution.
    /// </summary>
    public Guid ExecutionId { get; }

    /// <summary>
    /// The pipeline being executed.
    /// </summary>
    public PipelineDefinition Pipeline { get; }

    /// <summary>
    /// The initial input for the pipeline.
    /// </summary>
    public PluginInput InitialInput { get; }

    /// <summary>
    /// When execution started.
    /// </summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// User ID associated with this execution.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Game ID associated with this execution.
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// Creates a new execution context.
    /// </summary>
    public PipelineExecutionContext(PipelineDefinition pipeline, PluginInput initialInput)
    {
        ExecutionId = Guid.NewGuid();
        Pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        InitialInput = initialInput ?? throw new ArgumentNullException(nameof(initialInput));
        StartedAt = DateTimeOffset.UtcNow;
        UserId = initialInput.UserId;
        GameId = initialInput.GameId;
    }

    /// <summary>
    /// Sets the output for a completed node.
    /// </summary>
    public void SetNodeOutput(string nodeId, PluginOutput output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(output);
        _nodeOutputs[nodeId] = output;
    }

    /// <summary>
    /// Gets the output from a completed node.
    /// </summary>
    public PluginOutput? GetNodeOutput(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        return _nodeOutputs.TryGetValue(nodeId, out var output) ? output : null;
    }

    /// <summary>
    /// Gets all node outputs as a dictionary for pipeline context.
    /// </summary>
    public IReadOnlyDictionary<string, JsonDocument> GetPipelineContext()
    {
        var context = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);
        foreach (var (nodeId, output) in _nodeOutputs)
        {
            if (output.Result != null)
            {
                context[nodeId] = output.Result;
            }
        }
        return context;
    }

    /// <summary>
    /// Sets the execution state for a node.
    /// </summary>
    public void SetNodeState(string nodeId, NodeExecutionState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        _nodeStates[nodeId] = state;
    }

    /// <summary>
    /// Gets the execution state for a node.
    /// </summary>
    public NodeExecutionState GetNodeState(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        return _nodeStates.TryGetValue(nodeId, out var state) ? state : NodeExecutionState.Pending;
    }

    /// <summary>
    /// Checks if a node has completed (successfully or with error).
    /// </summary>
    public bool IsNodeCompleted(string nodeId)
    {
        var state = GetNodeState(nodeId);
        return state is NodeExecutionState.Completed or NodeExecutionState.Failed or NodeExecutionState.Skipped;
    }

    /// <summary>
    /// Gets metadata value.
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (_metadata.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <summary>
    /// Sets metadata value.
    /// </summary>
    public void SetMetadata(string key, object value)
    {
        _metadata[key] = value;
    }

    /// <summary>
    /// Gets total execution duration.
    /// </summary>
    public TimeSpan GetElapsedTime() => DateTimeOffset.UtcNow - StartedAt;

    /// <summary>
    /// Builds the final pipeline result.
    /// </summary>
    public PipelineResult BuildResult()
    {
        var finalOutputs = Pipeline.ExitPoints
            .Select(ep => (NodeId: ep, Output: GetNodeOutput(ep)))
            .Where(x => x.Output != null)
            .ToDictionary(x => x.NodeId, x => x.Output!, StringComparer.Ordinal);

        var nodeMetrics = _nodeOutputs
            .ToDictionary(x => x.Key, x => x.Value.Metrics, StringComparer.Ordinal);

        var success = Pipeline.ExitPoints.All(ep =>
        {
            var output = GetNodeOutput(ep);
            return output?.Success == true;
        });

        return new PipelineResult
        {
            ExecutionId = ExecutionId,
            PipelineId = Pipeline.Id,
            Success = success,
            FinalOutputs = finalOutputs,
            NodeMetrics = nodeMetrics,
            TotalDurationMs = GetElapsedTime().TotalMilliseconds,
            NodesExecuted = _nodeStates.Count(x => x.Value == NodeExecutionState.Completed),
            NodesFailed = _nodeStates.Count(x => x.Value == NodeExecutionState.Failed),
            NodesSkipped = _nodeStates.Count(x => x.Value == NodeExecutionState.Skipped)
        };
    }
}

/// <summary>
/// Execution state of a pipeline node.
/// </summary>
public enum NodeExecutionState
{
    /// <summary>Node has not started execution.</summary>
    Pending = 0,
    /// <summary>Node is currently executing.</summary>
    Running = 1,
    /// <summary>Node completed successfully.</summary>
    Completed = 2,
    /// <summary>Node execution failed.</summary>
    Failed = 3,
    /// <summary>Node was skipped due to condition not being met.</summary>
    Skipped = 4
}
