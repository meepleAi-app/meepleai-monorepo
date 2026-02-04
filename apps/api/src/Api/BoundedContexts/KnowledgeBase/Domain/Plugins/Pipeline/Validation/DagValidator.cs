// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3415 - DAG Orchestrator
// =============================================================================

using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Validation;

/// <summary>
/// Validates pipeline definitions for correctness (DAG structure, connectivity, etc.).
/// </summary>
public sealed class DagValidator
{
    /// <summary>
    /// Validates a pipeline definition.
    /// </summary>
    public ValidationResult Validate(PipelineDefinition pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // Basic validation
        ValidateBasicStructure(pipeline, errors);

        if (errors.Count > 0)
        {
            return ValidationResult.Failure(errors.ToArray());
        }

        // Build node lookup
        var nodeIds = pipeline.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);

        // Validate edges reference valid nodes
        ValidateEdgeNodes(pipeline, nodeIds, errors);

        // Validate entry point exists
        ValidateEntryPoint(pipeline, nodeIds, errors);

        // Validate exit points exist
        ValidateExitPoints(pipeline, nodeIds, errors);

        if (errors.Count > 0)
        {
            return ValidationResult.Failure(errors.ToArray());
        }

        // Validate DAG (no cycles)
        ValidateNoCycles(pipeline, errors);

        // Validate connectivity (all nodes reachable from entry)
        ValidateConnectivity(pipeline, warnings);

        // Validate exit points are reachable
        ValidateExitPointsReachable(pipeline, errors);

        if (errors.Count > 0)
        {
            return ValidationResult.Failure(errors.ToArray());
        }

        return warnings.Count > 0
            ? ValidationResult.SuccessWithWarnings(warnings.ToArray())
            : ValidationResult.Success();
    }

    private static void ValidateBasicStructure(PipelineDefinition pipeline, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(pipeline.Id))
        {
            errors.Add(new ValidationError
            {
                Message = "Pipeline ID is required",
                PropertyPath = "Id",
                Code = "MISSING_ID"
            });
        }

        if (string.IsNullOrWhiteSpace(pipeline.Name))
        {
            errors.Add(new ValidationError
            {
                Message = "Pipeline name is required",
                PropertyPath = "Name",
                Code = "MISSING_NAME"
            });
        }

        if (pipeline.Nodes == null || pipeline.Nodes.Count == 0)
        {
            errors.Add(new ValidationError
            {
                Message = "Pipeline must have at least one node",
                PropertyPath = "Nodes",
                Code = "NO_NODES"
            });
        }

        // Check for duplicate node IDs
        var duplicateNodeIds = pipeline.Nodes?
            .GroupBy(n => n.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateNodeIds?.Count > 0)
        {
            errors.Add(new ValidationError
            {
                Message = $"Duplicate node IDs found: {string.Join(", ", duplicateNodeIds)}",
                PropertyPath = "Nodes",
                Code = "DUPLICATE_NODE_IDS"
            });
        }
    }

    private static void ValidateEdgeNodes(PipelineDefinition pipeline, HashSet<string> nodeIds, List<ValidationError> errors)
    {
        foreach (var edge in pipeline.Edges)
        {
            if (!nodeIds.Contains(edge.From))
            {
                errors.Add(new ValidationError
                {
                    Message = $"Edge references non-existent source node: {edge.From}",
                    PropertyPath = $"Edges[{edge.From}->{edge.To}].From",
                    Code = "INVALID_SOURCE_NODE"
                });
            }

            if (!nodeIds.Contains(edge.To))
            {
                errors.Add(new ValidationError
                {
                    Message = $"Edge references non-existent target node: {edge.To}",
                    PropertyPath = $"Edges[{edge.From}->{edge.To}].To",
                    Code = "INVALID_TARGET_NODE"
                });
            }
        }
    }

    private static void ValidateEntryPoint(PipelineDefinition pipeline, HashSet<string> nodeIds, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(pipeline.EntryPoint))
        {
            errors.Add(new ValidationError
            {
                Message = "Entry point is required",
                PropertyPath = "EntryPoint",
                Code = "MISSING_ENTRY_POINT"
            });
        }
        else if (!nodeIds.Contains(pipeline.EntryPoint))
        {
            errors.Add(new ValidationError
            {
                Message = $"Entry point references non-existent node: {pipeline.EntryPoint}",
                PropertyPath = "EntryPoint",
                Code = "INVALID_ENTRY_POINT"
            });
        }
    }

    private static void ValidateExitPoints(PipelineDefinition pipeline, HashSet<string> nodeIds, List<ValidationError> errors)
    {
        if (pipeline.ExitPoints == null || pipeline.ExitPoints.Count == 0)
        {
            errors.Add(new ValidationError
            {
                Message = "At least one exit point is required",
                PropertyPath = "ExitPoints",
                Code = "NO_EXIT_POINTS"
            });
            return;
        }

        foreach (var exitPoint in pipeline.ExitPoints)
        {
            if (!nodeIds.Contains(exitPoint))
            {
                errors.Add(new ValidationError
                {
                    Message = $"Exit point references non-existent node: {exitPoint}",
                    PropertyPath = "ExitPoints",
                    Code = "INVALID_EXIT_POINT"
                });
            }
        }
    }

    private static void ValidateNoCycles(PipelineDefinition pipeline, List<ValidationError> errors)
    {
        // Build adjacency list
        var adjacencyList = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var node in pipeline.Nodes)
        {
            adjacencyList[node.Id] = [];
        }

        foreach (var edge in pipeline.Edges)
        {
            if (adjacencyList.TryGetValue(edge.From, out var neighbors))
            {
                neighbors.Add(edge.To);
            }
        }

        // DFS cycle detection
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var recursionStack = new HashSet<string>(StringComparer.Ordinal);
        var cycleNodes = new List<string>();

        foreach (var node in pipeline.Nodes)
        {
            if (DetectCycleDfs(node.Id, adjacencyList, visited, recursionStack, cycleNodes))
            {
                errors.Add(new ValidationError
                {
                    Message = $"Cycle detected in pipeline involving nodes: {string.Join(" -> ", cycleNodes)}",
                    PropertyPath = "Edges",
                    Code = "CYCLE_DETECTED"
                });
                return;
            }
        }
    }

    private static bool DetectCycleDfs(
        string nodeId,
        Dictionary<string, List<string>> adjacencyList,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> cycleNodes)
    {
        if (recursionStack.Contains(nodeId))
        {
            cycleNodes.Add(nodeId);
            return true;
        }

        if (visited.Contains(nodeId))
        {
            return false;
        }

        visited.Add(nodeId);
        recursionStack.Add(nodeId);

        if (adjacencyList.TryGetValue(nodeId, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (DetectCycleDfs(neighbor, adjacencyList, visited, recursionStack, cycleNodes))
                {
                    cycleNodes.Insert(0, nodeId);
                    return true;
                }
            }
        }

        recursionStack.Remove(nodeId);
        return false;
    }

    private static void ValidateConnectivity(PipelineDefinition pipeline, List<ValidationWarning> warnings)
    {
        // BFS from entry point
        var reachable = new HashSet<string>(StringComparer.Ordinal) { pipeline.EntryPoint };
        var queue = new Queue<string>();
        queue.Enqueue(pipeline.EntryPoint);

        var adjacencyList = BuildAdjacencyList(pipeline);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (adjacencyList.TryGetValue(current, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (reachable.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        // Check for unreachable nodes
        var unreachableNodes = pipeline.Nodes
            .Where(n => !reachable.Contains(n.Id))
            .Select(n => n.Id)
            .ToList();

        if (unreachableNodes.Count > 0)
        {
            warnings.Add(new ValidationWarning
            {
                Message = $"Nodes not reachable from entry point: {string.Join(", ", unreachableNodes)}",
                PropertyPath = "Nodes",
                Code = "UNREACHABLE_NODES"
            });
        }
    }

    private static void ValidateExitPointsReachable(PipelineDefinition pipeline, List<ValidationError> errors)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal) { pipeline.EntryPoint };
        var queue = new Queue<string>();
        queue.Enqueue(pipeline.EntryPoint);

        var adjacencyList = BuildAdjacencyList(pipeline);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (adjacencyList.TryGetValue(current, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (reachable.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        var unreachableExitPoints = pipeline.ExitPoints
            .Where(ep => !reachable.Contains(ep))
            .ToList();

        if (unreachableExitPoints.Count > 0)
        {
            errors.Add(new ValidationError
            {
                Message = $"Exit points not reachable from entry point: {string.Join(", ", unreachableExitPoints)}",
                PropertyPath = "ExitPoints",
                Code = "UNREACHABLE_EXIT_POINTS"
            });
        }
    }

    private static Dictionary<string, List<string>> BuildAdjacencyList(PipelineDefinition pipeline)
    {
        var adjacencyList = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var node in pipeline.Nodes)
        {
            adjacencyList[node.Id] = [];
        }

        foreach (var edge in pipeline.Edges)
        {
            if (adjacencyList.TryGetValue(edge.From, out var neighbors))
            {
                neighbors.Add(edge.To);
            }
        }

        return adjacencyList;
    }

    /// <summary>
    /// Performs topological sort of pipeline nodes.
    /// </summary>
    public IReadOnlyList<string> TopologicalSort(PipelineDefinition pipeline)
    {
        var adjacencyList = BuildAdjacencyList(pipeline);
        var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var node in pipeline.Nodes)
        {
            inDegree[node.Id] = 0;
        }

        foreach (var edge in pipeline.Edges)
        {
            if (inDegree.TryGetValue(edge.To, out var currentDegree))
            {
                inDegree[edge.To] = currentDegree + 1;
            }
        }

        var queue = new Queue<string>();
        foreach (var (nodeId, degree) in inDegree)
        {
            if (degree == 0)
            {
                queue.Enqueue(nodeId);
            }
        }

        var result = new List<string>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            if (adjacencyList.TryGetValue(current, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return result;
    }
}
