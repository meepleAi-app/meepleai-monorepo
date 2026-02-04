// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3415 - DAG Orchestrator
// =============================================================================

using System.Collections.Concurrent;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Validation;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Services;

/// <summary>
/// Orchestrates the execution of a pipeline DAG with parallel execution support.
/// </summary>
public sealed class DagOrchestrator : IDagOrchestrator
{
    private readonly Func<string, IRagPlugin?> _pluginResolver;
    private readonly DagValidator _validator;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly ILogger<DagOrchestrator> _logger;

    /// <summary>
    /// Creates a new DAG orchestrator.
    /// </summary>
    /// <param name="pluginResolver">Function to resolve plugins by ID.</param>
    /// <param name="logger">Logger instance.</param>
    public DagOrchestrator(
        Func<string, IRagPlugin?> pluginResolver,
        ILogger<DagOrchestrator> logger)
    {
        _pluginResolver = pluginResolver ?? throw new ArgumentNullException(nameof(pluginResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validator = new DagValidator();
        _conditionEvaluator = new ConditionEvaluator();
    }

    /// <inheritdoc />
    public async Task<PipelineResult> ExecuteAsync(
        PipelineDefinition pipeline,
        PluginInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(input);

        // Validate pipeline
        var validationResult = _validator.Validate(pipeline);
        if (!validationResult.IsValid)
        {
            _logger.LogError("Pipeline validation failed: {Errors}",
                string.Join("; ", validationResult.Errors.Select(e => e.Message)));

            return new PipelineResult
            {
                ExecutionId = Guid.NewGuid(),
                PipelineId = pipeline.Id,
                Success = false,
                FinalOutputs = new Dictionary<string, PluginOutput>(StringComparer.Ordinal),
                NodeMetrics = new Dictionary<string, PluginExecutionMetrics>(StringComparer.Ordinal),
                TotalDurationMs = 0,
                ErrorMessage = $"Pipeline validation failed: {string.Join("; ", validationResult.Errors.Select(e => e.Message))}"
            };
        }

        // Create execution context
        var context = new PipelineExecutionContext(pipeline, input);

        _logger.LogInformation("Starting pipeline execution {ExecutionId} for pipeline {PipelineId}",
            context.ExecutionId, pipeline.Id);

        try
        {
            // Create global timeout
            using var globalCts = new CancellationTokenSource(pipeline.GlobalTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, globalCts.Token);

            // Build dependency graph
            var dependencies = BuildDependencies(pipeline);
            var reverseDependencies = BuildReverseDependencies(pipeline);

            // Execute with parallel scheduling
            await ExecutePipelineAsync(pipeline, context, dependencies, reverseDependencies, linkedCts.Token)
                .ConfigureAwait(false);

            var result = context.BuildResult();

            _logger.LogInformation(
                "Pipeline execution {ExecutionId} completed. Success: {Success}, Duration: {Duration}ms",
                context.ExecutionId, result.Success, result.TotalDurationMs);

            return result;
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Pipeline execution {ExecutionId} cancelled", context.ExecutionId);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Pipeline execution {ExecutionId} timed out after {Timeout}ms",
                context.ExecutionId, pipeline.GlobalTimeoutMs);

            return context.BuildResult() with
            {
                Success = false,
                ErrorMessage = $"Pipeline execution timed out after {pipeline.GlobalTimeoutMs}ms"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline execution {ExecutionId} failed", context.ExecutionId);

            return context.BuildResult() with
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task ExecutePipelineAsync(
        PipelineDefinition pipeline,
        PipelineExecutionContext context,
        Dictionary<string, HashSet<string>> dependencies,
        Dictionary<string, List<PipelineEdge>> reverseDependencies,
        CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(pipeline.MaxParallelism);
        var pendingTasks = new List<Task>();
        var nodeQueue = new ConcurrentQueue<string>();

        // Start with entry point
        nodeQueue.Enqueue(pipeline.EntryPoint);

        while (!nodeQueue.IsEmpty || pendingTasks.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Start ready nodes
            while (nodeQueue.TryDequeue(out var nodeId))
            {

                // Check if all dependencies are satisfied
                if (!AreDependenciesSatisfied(nodeId, dependencies, context))
                {
                    continue;
                }

                // Skip if already processed
                if (context.IsNodeCompleted(nodeId))
                {
                    continue;
                }

                // Check if node should be skipped due to edge conditions
                if (ShouldSkipNode(nodeId, reverseDependencies, context))
                {
                    context.SetNodeState(nodeId, NodeExecutionState.Skipped);
                    EnqueueDependentNodes(nodeId, pipeline, context, nodeQueue);
                    continue;
                }

                // Start execution
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                var task = ExecuteNodeAsync(nodeId, pipeline, context, semaphore, cancellationToken)
                    .ContinueWith(t =>
                    {
                        EnqueueDependentNodes(nodeId, pipeline, context, nodeQueue);
                        return t;
                    }, cancellationToken, TaskContinuationOptions.None, TaskScheduler.Default);

                pendingTasks.Add(task.Unwrap());
            }

            // Wait for at least one task to complete
            if (pendingTasks.Count > 0)
            {
                var completed = await Task.WhenAny(pendingTasks).ConfigureAwait(false);
                pendingTasks.Remove(completed);
                await completed.ConfigureAwait(false); // Propagate exceptions
            }
        }

        // Wait for all remaining tasks
        if (pendingTasks.Count > 0)
        {
            await Task.WhenAll(pendingTasks).ConfigureAwait(false);
        }
    }

    private async Task ExecuteNodeAsync(
        string nodeId,
        PipelineDefinition pipeline,
        PipelineExecutionContext context,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        try
        {
            context.SetNodeState(nodeId, NodeExecutionState.Running);

            var node = pipeline.Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.Ordinal));
            if (node == null)
            {
                _logger.LogError("Node not found in pipeline: {NodeId} in pipeline {PipelineId}",
                    nodeId, pipeline.Id);

                var failedOutput = PluginOutput.Failed(
                    context.ExecutionId,
                    $"Node not found in pipeline: {nodeId}",
                    "NODE_NOT_FOUND");

                context.SetNodeOutput(nodeId, failedOutput);
                context.SetNodeState(nodeId, NodeExecutionState.Failed);
                return;
            }

            var plugin = _pluginResolver(node.PluginId);

            if (plugin == null)
            {
                _logger.LogError("Plugin not found: {PluginId} for node {NodeId}",
                    node.PluginId, nodeId);

                var failedOutput = PluginOutput.Failed(
                    context.ExecutionId,
                    $"Plugin not found: {node.PluginId}",
                    "PLUGIN_NOT_FOUND");

                context.SetNodeOutput(nodeId, failedOutput);
                context.SetNodeState(nodeId, NodeExecutionState.Failed);
                return;
            }

            // Build input for this node
            var nodeInput = new PluginInput
            {
                ExecutionId = context.ExecutionId,
                Payload = context.InitialInput.Payload,
                PipelineContext = context.GetPipelineContext(),
                UserId = context.UserId,
                GameId = context.GameId
            };

            // Execute plugin
            var config = node.Config ?? PluginConfig.Create(
                timeoutMs: node.TimeoutMs,
                maxRetries: node.Retry?.MaxAttempts ?? 3);

            _logger.LogDebug("Executing node {NodeId} with plugin {PluginId}",
                nodeId, node.PluginId);

            var output = await plugin.ExecuteAsync(nodeInput, config, cancellationToken)
                .ConfigureAwait(false);

            context.SetNodeOutput(nodeId, output);
            context.SetNodeState(nodeId, output.Success ? NodeExecutionState.Completed : NodeExecutionState.Failed);

            _logger.LogDebug("Node {NodeId} completed. Success: {Success}",
                nodeId, output.Success);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static Dictionary<string, HashSet<string>> BuildDependencies(PipelineDefinition pipeline)
    {
        var dependencies = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var node in pipeline.Nodes)
        {
            dependencies[node.Id] = [];
        }

        foreach (var edge in pipeline.Edges)
        {
            if (dependencies.TryGetValue(edge.To, out var deps))
            {
                deps.Add(edge.From);
            }
        }

        return dependencies;
    }

    private static Dictionary<string, List<PipelineEdge>> BuildReverseDependencies(PipelineDefinition pipeline)
    {
        var reverse = new Dictionary<string, List<PipelineEdge>>(StringComparer.Ordinal);

        foreach (var node in pipeline.Nodes)
        {
            reverse[node.Id] = [];
        }

        foreach (var edge in pipeline.Edges)
        {
            if (reverse.TryGetValue(edge.To, out var edges))
            {
                edges.Add(edge);
            }
        }

        return reverse;
    }

    private static bool AreDependenciesSatisfied(
        string nodeId,
        Dictionary<string, HashSet<string>> dependencies,
        PipelineExecutionContext context)
    {
        if (!dependencies.TryGetValue(nodeId, out var deps))
        {
            return true;
        }

        return deps.All(context.IsNodeCompleted);
    }

    private bool ShouldSkipNode(
        string nodeId,
        Dictionary<string, List<PipelineEdge>> reverseDependencies,
        PipelineExecutionContext context)
    {
        if (!reverseDependencies.TryGetValue(nodeId, out var incomingEdges) || incomingEdges.Count == 0)
        {
            // No incoming edges (entry point), don't skip
            return false;
        }

        // Check if any incoming edge condition is satisfied
        foreach (var edge in incomingEdges.OrderBy(e => e.Priority))
        {
            var sourceOutput = context.GetNodeOutput(edge.From);
            if (sourceOutput == null)
            {
                continue;
            }

            // If any condition is met, don't skip
            if (_conditionEvaluator.Evaluate(edge.Condition, sourceOutput))
            {
                return false;
            }
        }

        // No conditions met, skip the node
        return true;
    }

    private static void EnqueueDependentNodes(
        string completedNodeId,
        PipelineDefinition pipeline,
        PipelineExecutionContext context,
        ConcurrentQueue<string> nodeQueue)
    {
        // Find all edges from this node
        var outgoingEdges = pipeline.Edges
            .Where(e => string.Equals(e.From, completedNodeId, StringComparison.Ordinal))
            .OrderBy(e => e.Priority);

        foreach (var edge in outgoingEdges)
        {
            var targetNodeId = edge.To;

            // Skip if already completed
            if (context.IsNodeCompleted(targetNodeId))
            {
                continue;
            }

            // Skip if already in queue (we'll check dependencies when dequeued)
            nodeQueue.Enqueue(targetNodeId);
        }
    }
}
