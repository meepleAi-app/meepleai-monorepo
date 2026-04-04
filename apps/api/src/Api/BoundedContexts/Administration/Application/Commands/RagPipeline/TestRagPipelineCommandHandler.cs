using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Commands.RagPipeline;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands.RagPipeline;

/// <summary>
/// Handler for TestRagPipelineCommand.
/// Executes pipeline blocks in sequence and streams results via SSE.
/// Issue #3463: Live test API with SSE streaming.
/// </summary>
internal sealed class TestRagPipelineCommandHandler(
    ILogger<TestRagPipelineCommandHandler> logger
) : IRequestHandler<TestRagPipelineCommand, IAsyncEnumerable<RagPipelineTestEvent>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<IAsyncEnumerable<RagPipelineTestEvent>> Handle(
        TestRagPipelineCommand request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(ExecutePipelineAsync(request, cancellationToken));
    }

    private async IAsyncEnumerable<RagPipelineTestEvent> ExecutePipelineAsync(
        TestRagPipelineCommand request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var totalTokens = 0;
        var totalCost = 0m;
        var blocksExecuted = 0;
        var blocksFailed = 0;
        string? finalResponse = null;

        // Parse pipeline definition
        var (pipeline, parseError) = ParsePipeline(request.PipelineDefinition);
        if (pipeline == null)
        {
            yield return new PipelineTestCompletedEvent
            {
                EventType = nameof(PipelineTestCompletedEvent),
                Timestamp = DateTime.UtcNow,
                Success = false,
                TotalDurationMs = 0,
                TotalTokensUsed = 0,
                TotalCost = 0,
                BlocksExecuted = 0,
                BlocksFailed = 1,
                Error = parseError
            };
            yield break;
        }

        logger.LogInformation(
            "Starting pipeline test for user {UserId}: {BlockCount} blocks, query: {Query}",
            request.UserId,
            pipeline.Nodes.Count,
            request.TestQuery);

        // Emit start event
        yield return new PipelineTestStartedEvent
        {
            EventType = nameof(PipelineTestStartedEvent),
            Timestamp = DateTime.UtcNow,
            PipelineId = pipeline.Id,
            Query = request.TestQuery,
            TotalBlocks = pipeline.Nodes.Count
        };

        // Execute each block in topological order
        var executionOrder = GetTopologicalOrder(pipeline);
        var currentContext = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["query"] = request.TestQuery
        };

        foreach (var (nodeId, index) in executionOrder.Select((id, i) => (id, i)))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield return new PipelineTestCompletedEvent
                {
                    EventType = nameof(PipelineTestCompletedEvent),
                    Timestamp = DateTime.UtcNow,
                    Success = false,
                    TotalDurationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    TotalTokensUsed = totalTokens,
                    TotalCost = totalCost,
                    BlocksExecuted = blocksExecuted,
                    BlocksFailed = blocksFailed,
                    Error = "Test cancelled by user"
                };
                yield break;
            }

            var node = pipeline.Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.Ordinal));
            if (node == null) continue;

            // Emit block started event
            yield return new BlockExecutionStartedEvent
            {
                EventType = nameof(BlockExecutionStartedEvent),
                Timestamp = DateTime.UtcNow,
                BlockId = node.Id,
                BlockType = node.Type,
                BlockName = node.Data?.Block?.Name ?? node.Type,
                BlockIndex = index + 1
            };

            var blockStartTime = DateTime.UtcNow;

            // Execute block (no try-catch around yield)
            var blockResult = await ExecuteBlockAsync(node, currentContext, request.TestQuery, cancellationToken).ConfigureAwait(false);

            if (blockResult.Success)
            {
                totalTokens += blockResult.Tokens;
                totalCost += blockResult.Cost;
                blocksExecuted++;

                // Update context for downstream blocks
                if (node.Type.Contains("search", StringComparison.OrdinalIgnoreCase) ||
                    node.Type.Contains("retrieval", StringComparison.OrdinalIgnoreCase))
                {
                    currentContext["documents"] = blockResult.Output ?? new object();
                }

                // Emit documents retrieved if applicable
                if (blockResult.Documents != null && blockResult.Documents.Count > 0)
                {
                    yield return new DocumentsRetrievedEvent
                    {
                        EventType = nameof(DocumentsRetrievedEvent),
                        Timestamp = DateTime.UtcNow,
                        BlockId = node.Id,
                        DocumentCount = blockResult.Documents.Count,
                        Documents = blockResult.Documents
                    };
                }

                // Emit validation result if applicable
                if (blockResult.Validation != null)
                {
                    yield return new ValidationResultEvent
                    {
                        EventType = nameof(ValidationResultEvent),
                        Timestamp = DateTime.UtcNow,
                        BlockId = node.Id,
                        ValidationType = blockResult.Validation.Type,
                        Passed = blockResult.Validation.Passed,
                        Score = blockResult.Validation.Score,
                        Details = blockResult.Validation.Details
                    };
                }

                // Track final response from agent blocks
                if (node.Type.Contains("agent", StringComparison.OrdinalIgnoreCase))
                {
                    finalResponse = blockResult.Output?.ToString();
                }

                // Emit block completed event
                yield return new BlockExecutionCompletedEvent
                {
                    EventType = nameof(BlockExecutionCompletedEvent),
                    Timestamp = DateTime.UtcNow,
                    BlockId = node.Id,
                    BlockType = node.Type,
                    Success = true,
                    DurationMs = (int)(DateTime.UtcNow - blockStartTime).TotalMilliseconds,
                    TokensUsed = blockResult.Tokens,
                    Cost = blockResult.Cost,
                    Output = TruncateOutput(blockResult.Output?.ToString())
                };
            }
            else
            {
                logger.LogError("Block {BlockId} ({BlockType}) failed: {Error}", node.Id, node.Type, blockResult.Error);
                blocksFailed++;

                yield return new BlockExecutionCompletedEvent
                {
                    EventType = nameof(BlockExecutionCompletedEvent),
                    Timestamp = DateTime.UtcNow,
                    BlockId = node.Id,
                    BlockType = node.Type,
                    Success = false,
                    DurationMs = (int)(DateTime.UtcNow - blockStartTime).TotalMilliseconds,
                    TokensUsed = 0,
                    Cost = 0,
                    Error = blockResult.Error
                };
            }

            // Small delay to allow SSE events to be processed
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        // Emit completion event
        yield return new PipelineTestCompletedEvent
        {
            EventType = nameof(PipelineTestCompletedEvent),
            Timestamp = DateTime.UtcNow,
            Success = blocksFailed == 0,
            TotalDurationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
            TotalTokensUsed = totalTokens,
            TotalCost = totalCost,
            BlocksExecuted = blocksExecuted,
            BlocksFailed = blocksFailed,
            FinalResponse = finalResponse
        };
    }

    /// <summary>
    /// Parse pipeline definition from JSON.
    /// </summary>
    private (PipelineDefinition? Pipeline, string? Error) ParsePipeline(string json)
    {
        try
        {
            var pipeline = JsonSerializer.Deserialize<PipelineDefinition>(json, JsonOptions);
            if (pipeline == null)
            {
                return (null, "Failed to parse pipeline definition");
            }
            return (pipeline, null);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse pipeline definition");
            return (null, $"Invalid pipeline definition: {ex.Message}");
        }
    }

    /// <summary>
    /// Get topological order of nodes based on edges.
    /// </summary>
    private static List<string> GetTopologicalOrder(PipelineDefinition pipeline)
    {
        var result = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var inDegree = pipeline.Nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);

        foreach (var edge in pipeline.Edges)
        {
            if (inDegree.TryGetValue(edge.Target, out var current))
            {
                inDegree[edge.Target] = current + 1;
            }
        }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;

            result.Add(current);

            foreach (var edge in pipeline.Edges.Where(e => string.Equals(e.Source, current, StringComparison.Ordinal)))
            {
                if (inDegree.TryGetValue(edge.Target, out var deg) && deg > 0)
                {
                    inDegree[edge.Target] = deg - 1;
                    if (inDegree[edge.Target] == 0)
                    {
                        queue.Enqueue(edge.Target);
                    }
                }
            }
        }

        // Add any remaining nodes not connected
        foreach (var node in pipeline.Nodes.Where(n => !visited.Contains(n.Id)))
        {
            result.Add(node.Id);
        }

        return result;
    }

    /// <summary>
    /// Execute a single block with simulation.
    /// </summary>
    private static async Task<BlockExecutionResult> ExecuteBlockAsync(
        PipelineNode node,
        Dictionary<string, object> context,
        string query,
        CancellationToken ct)
    {
        try
        {
            // Simulate latency based on block type
            var baseLatency = node.Type switch
            {
                "vector-search" => 100,
                "keyword-search" => 50,
                "hybrid-search" => 200,
                "cross-encoder-reranking" => 300,
                "crag-evaluator" => 300,
                "sequential-agent" => 1500,
                "parallel-agent" => 1000,
                _ => 150
            };

            await Task.Delay(baseLatency + Random.Shared.Next(50, 150), ct).ConfigureAwait(false);

            // Simulate tokens and cost
            var tokens = node.Type switch
            {
                "vector-search" => Random.Shared.Next(800, 1200),
                "hybrid-search" => Random.Shared.Next(1200, 1800),
                "sequential-agent" => Random.Shared.Next(6000, 10000),
                _ => Random.Shared.Next(200, 600)
            };

            var cost = tokens * 0.00001m;

            // Simulate documents for retrieval blocks
            IReadOnlyList<RetrievedDocumentDto>? documents = null;
            if (node.Type.Contains("search", StringComparison.OrdinalIgnoreCase) ||
                node.Type.Contains("retrieval", StringComparison.OrdinalIgnoreCase))
            {
                documents = Enumerable.Range(1, Random.Shared.Next(3, 7))
                    .Select(i => new RetrievedDocumentDto
                    {
                        Id = $"doc-{i.ToString(CultureInfo.InvariantCulture)}",
                        Title = $"Sample Document {i.ToString(CultureInfo.InvariantCulture)} for '{query}'",
                        Content = $"This is simulated content related to the query about {query}. " +
                                  $"Document {i.ToString(CultureInfo.InvariantCulture)} contains relevant information.",
                        Score = 0.95 - (i * 0.05) + Random.Shared.NextDouble() * 0.02,
                        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["source"] = $"test-source-{i.ToString(CultureInfo.InvariantCulture)}",
                            ["page"] = i.ToString(CultureInfo.InvariantCulture)
                        }
                    })
                    .ToList();
            }

            // Simulate validation for evaluation blocks
            ValidationResult? validation = null;
            if (node.Type.Contains("evaluator", StringComparison.OrdinalIgnoreCase) ||
                node.Type.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
                node.Type.Contains("confidence", StringComparison.OrdinalIgnoreCase))
            {
                var score = 0.75 + Random.Shared.NextDouble() * 0.2;
                validation = new ValidationResult
                {
                    Type = node.Type,
                    Passed = score >= 0.7,
                    Score = score,
                    Details = score >= 0.7
                        ? "Retrieved documents are relevant and sufficient"
                        : "Some documents may not be directly relevant"
                };
            }

            // Generate output
            object output = node.Type switch
            {
                "sequential-agent" or "parallel-agent" =>
                    $"Based on the available information about '{query}', here is a comprehensive answer: " +
                    "This is a simulated response that would be generated by the RAG pipeline.",
                _ => new { status = "completed", blockType = node.Type }
            };

            return new BlockExecutionResult
            {
                Success = true,
                Output = output,
                Tokens = tokens,
                Cost = cost,
                Documents = documents,
                Validation = validation
            };
        }
        catch (Exception ex)
        {
            return new BlockExecutionResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static string? TruncateOutput(string? output, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(output)) return output;
        return output.Length <= maxLength ? output : string.Concat(output.AsSpan(0, maxLength), "...");
    }

    // Internal DTOs for parsing
    private sealed record PipelineDefinition
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public IReadOnlyList<PipelineNode> Nodes { get; init; } = [];
        public IReadOnlyList<PipelineEdge> Edges { get; init; } = [];
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar", "S3459:Unassigned members should be removed", Justification = "Populated by JSON deserialization")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar", "S1144:Unused private types or members should be removed", Justification = "Populated by JSON deserialization")]
    private sealed class PipelineNode
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public PipelineNodeData? Data { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar", "S3459:Unassigned members should be removed", Justification = "Populated by JSON deserialization")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar", "S1144:Unused private types or members should be removed", Justification = "Populated by JSON deserialization")]
    private sealed class PipelineNodeData
    {
        public PipelineBlockInfo? Block { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar", "S3459:Unassigned members should be removed", Justification = "Populated by JSON deserialization")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar", "S1144:Unused private types or members should be removed", Justification = "Populated by JSON deserialization")]
    private sealed class PipelineBlockInfo
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed record PipelineEdge
    {
        public string Source { get; init; } = string.Empty;
        public string Target { get; init; } = string.Empty;
    }

    private sealed record BlockExecutionResult
    {
        public bool Success { get; init; }
        public object? Output { get; init; }
        public int Tokens { get; init; }
        public decimal Cost { get; init; }
        public IReadOnlyList<RetrievedDocumentDto>? Documents { get; init; }
        public ValidationResult? Validation { get; init; }
        public string? Error { get; init; }
    }

    private sealed record ValidationResult
    {
        public required string Type { get; init; }
        public required bool Passed { get; init; }
        public required double Score { get; init; }
        public string? Details { get; init; }
    }
}
