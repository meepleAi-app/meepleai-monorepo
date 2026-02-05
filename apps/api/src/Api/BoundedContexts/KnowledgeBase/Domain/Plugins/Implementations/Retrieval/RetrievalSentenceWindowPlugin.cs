// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3357 - Sentence Window RAG Strategy
// =============================================================================

using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Retrieval;

/// <summary>
/// Sentence Window retrieval plugin for precise context-aware answers.
/// Issue #3357: +7% accuracy improvement with ~3,250 tokens per query.
/// </summary>
/// <remarks>
/// Strategy Overview:
/// 1. Indexing: Documents split into individual sentences (~100 tokens each)
/// 2. Retrieval: Top 5 most relevant sentences retrieved
/// 3. Window Expansion: Each sentence expanded with ±N surrounding sentences
/// 4. Deduplication: Overlapping windows merged
/// 5. Generation: LLM generates answer using expanded context
///
/// Token Budget:
/// - Sentence Retrieval (5 sentences): ~500 tokens
/// - Window Expansion (±2 sentences): ~2,000 tokens
/// - Total Context: ~2,500 tokens
/// - Input (system + query + context): ~2,950 tokens
/// - Output: ~300 tokens
/// - Total: ~3,250 tokens
/// </remarks>
[RagPlugin("retrieval-sentence-window-v1",
    Category = PluginCategory.Retrieval,
    Name = "Sentence Window Retrieval",
    Description = "Sentence-level granular search with window expansion for precise citations",
    Author = "MeepleAI",
    Priority = 45)]
public sealed class RetrievalSentenceWindowPlugin : RagPluginBase
{
    private const int DefaultWindowSize = 2;
    private const int DefaultTopK = 5;
    private const double DefaultMinScore = 0.6;

    /// <inheritdoc />
    public override string Id => "retrieval-sentence-window-v1";

    /// <inheritdoc />
    public override string Name => "Sentence Window Retrieval";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Retrieval;

    /// <inheritdoc />
    protected override string Description => "Sentence-level granular search with window expansion for +7% accuracy";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["retrieval", "sentence-window", "precise", "citations"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["sentence-search", "window-expansion", "context-aware", "deduplication"];

    public RetrievalSentenceWindowPlugin(ILogger<RetrievalSentenceWindowPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override async Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var query = GetQueryFromPayload(input.Payload);
        if (string.IsNullOrWhiteSpace(query))
        {
            return PluginOutput.Failed(input.ExecutionId, "Query is required in payload", "MISSING_QUERY");
        }

        var customConfig = ParseCustomConfig(config);
        var stopwatch = Stopwatch.StartNew();

        // Phase 1: Generate query embedding
        var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken).ConfigureAwait(false);

        // Phase 2: Retrieve top-K most relevant sentences
        var sentences = await RetrieveSentencesAsync(queryEmbedding, customConfig, cancellationToken).ConfigureAwait(false);

        // Phase 3: Expand each sentence with surrounding context window
        var expandedContexts = ExpandSentenceWindows(sentences, customConfig.WindowSize);

        // Phase 4: Deduplicate and merge overlapping windows
        var mergedDocuments = DeduplicateAndMerge(expandedContexts, customConfig);

        stopwatch.Stop();

        // Calculate metrics
        var totalTokens = mergedDocuments.Sum(d => EstimateTokenCount(d.Content));
        var metrics = new SentenceWindowMetrics
        {
            SentencesRetrieved = sentences.Count,
            WindowsExpanded = expandedContexts.Count,
            DocumentsAfterMerge = mergedDocuments.Count,
            TotalTokens = totalTokens,
            WindowSize = customConfig.WindowSize
        };

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            documents = mergedDocuments.Select(d => new
            {
                id = d.Id,
                content = d.Content,
                score = d.Score,
                source = d.Source,
                metadata = d.Metadata
            }),
            metrics = new
            {
                sentencesRetrieved = metrics.SentencesRetrieved,
                windowsExpanded = metrics.WindowsExpanded,
                documentsAfterMerge = metrics.DocumentsAfterMerge,
                totalTokens = metrics.TotalTokens,
                windowSize = metrics.WindowSize
            }
        }));

        Logger.LogInformation(
            "Sentence Window retrieval: Sentences={Sentences}, Windows={Windows}, Merged={Merged}, Tokens={Tokens}, Duration={Duration:F0}ms",
            metrics.SentencesRetrieved,
            metrics.WindowsExpanded,
            metrics.DocumentsAfterMerge,
            metrics.TotalTokens,
            stopwatch.Elapsed.TotalMilliseconds);

        return new PluginOutput
        {
            ExecutionId = input.ExecutionId,
            Success = true,
            Result = result,
            Confidence = mergedDocuments.Count > 0 ? mergedDocuments.Max(d => d.Score) : 0,
            Metrics = new PluginExecutionMetrics
            {
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                ItemsProcessed = mergedDocuments.Count,
                InputTokens = totalTokens
            }
        };
    }

    /// <summary>
    /// Retrieves the most relevant sentences using vector similarity search.
    /// </summary>
    private static async Task<List<RetrievedSentence>> RetrieveSentencesAsync(
        float[] queryEmbedding,
        SentenceWindowConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate sentence-level retrieval (production integrates with Qdrant sentence index)
        await Task.Delay(20, cancellationToken).ConfigureAwait(false);

        var results = new List<RetrievedSentence>();
        var random = new Random(queryEmbedding.GetHashCode());

        // Simulated sentence retrieval with document context
        var documentContents = new[]
        {
            "The game begins with each player placing their starting pieces on the board. All players receive an equal number of resource tokens.",
            "During your turn, you may perform up to three actions. Each action costs one action point.",
            "Combat is resolved by comparing attack values against defense values. The attacker rolls dice equal to their attack strength.",
            "Victory points are earned by completing objectives. The first player to reach 10 victory points wins the game.",
            "Special abilities can be activated once per round. Each character has a unique special ability.",
            "Resource management is crucial for success. Players must balance spending with income generation.",
            "The end game triggers when any player reaches the final space. All other players get one more turn.",
            "Scoring bonuses are awarded for completing sets. A complete set of four matching cards earns 5 extra points.",
            "Movement rules allow diagonal movement on specified terrain types. Mountains block all movement.",
            "Trading between players is allowed during the market phase. Minimum trade value is 2 resources."
        };

        // Generate relevance scores (sorted by relevance)
        for (int i = 0; i < Math.Min(config.TopK, documentContents.Length); i++)
        {
            var score = 0.95 - (i * 0.05) + (random.NextDouble() * 0.02);
            if (score >= config.MinScore)
            {
                results.Add(new RetrievedSentence
                {
                    Id = $"sent-{Guid.NewGuid():N}",
                    DocumentId = $"doc-{i / 3}",
                    SentenceIndex = i % 4,
                    Content = documentContents[i],
                    Score = score,
                    Source = config.Collection,
                    SurroundingSentences = GetSimulatedSurroundingSentences(documentContents, i)
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Expands each retrieved sentence with surrounding context.
    /// </summary>
    private static List<ExpandedContext> ExpandSentenceWindows(
        List<RetrievedSentence> sentences,
        int windowSize)
    {
        var expandedContexts = new List<ExpandedContext>();

        foreach (var sentence in sentences)
        {
            var windowContent = new List<string>();
            var startIndex = Math.Max(0, sentence.SentenceIndex - windowSize);
            var endIndex = sentence.SentenceIndex + windowSize;

            // Add preceding sentences from window
            for (int i = startIndex; i < sentence.SentenceIndex; i++)
            {
                if (i >= 0 && i < sentence.SurroundingSentences.Count)
                {
                    windowContent.Add(sentence.SurroundingSentences[i]);
                }
            }

            // Add the core sentence
            windowContent.Add(sentence.Content);

            // Add following sentences from window
            for (int i = sentence.SentenceIndex + 1; i <= endIndex; i++)
            {
                if (i >= 0 && i < sentence.SurroundingSentences.Count)
                {
                    windowContent.Add(sentence.SurroundingSentences[i]);
                }
            }

            expandedContexts.Add(new ExpandedContext
            {
                OriginalSentence = sentence,
                ExpandedContent = string.Join(" ", windowContent),
                WindowStart = startIndex,
                WindowEnd = Math.Min(endIndex, sentence.SurroundingSentences.Count - 1),
                CoreSentenceIndex = sentence.SentenceIndex
            });
        }

        return expandedContexts;
    }

    /// <summary>
    /// Deduplicates overlapping windows and merges adjacent contexts.
    /// </summary>
    private static List<RetrievedDocument> DeduplicateAndMerge(
        List<ExpandedContext> expandedContexts,
        SentenceWindowConfig config)
    {
        if (expandedContexts.Count == 0)
            return [];

        // Group by document ID to detect overlaps
        var byDocument = expandedContexts
            .GroupBy(e => e.OriginalSentence.DocumentId, StringComparer.Ordinal)
            .ToList();

        var mergedDocuments = new List<RetrievedDocument>();

        foreach (var docGroup in byDocument)
        {
            // Sort by window start position
            var orderedWindows = docGroup.OrderBy(e => e.WindowStart).ToList();

            // Merge overlapping windows within same document
            var currentMerge = orderedWindows[0];
            var mergedContent = new List<string> { currentMerge.ExpandedContent };
            var maxScore = currentMerge.OriginalSentence.Score;

            for (int i = 1; i < orderedWindows.Count; i++)
            {
                var next = orderedWindows[i];

                // Check for overlap (window start is within previous window end)
                if (next.WindowStart <= currentMerge.WindowEnd + 1)
                {
                    // Merge: only add non-overlapping content
                    if (next.WindowEnd > currentMerge.WindowEnd)
                    {
                        // Extract only the new sentences
                        var overlapSentences = next.ExpandedContent.Split(". ")
                            .Skip(currentMerge.WindowEnd - next.WindowStart + 1)
                            .ToList();
                        if (overlapSentences.Count > 0)
                        {
                            mergedContent.Add(string.Join(". ", overlapSentences));
                        }
                    }
                    maxScore = Math.Max(maxScore, next.OriginalSentence.Score);
                    currentMerge = currentMerge with { WindowEnd = Math.Max(currentMerge.WindowEnd, next.WindowEnd) };
                }
                else
                {
                    // No overlap - create document from current merge and start new
                    mergedDocuments.Add(CreateMergedDocument(
                        docGroup.Key,
                        string.Join(" ", mergedContent),
                        maxScore,
                        currentMerge.OriginalSentence.Source,
                        currentMerge));

                    currentMerge = next;
                    mergedContent = [next.ExpandedContent];
                    maxScore = next.OriginalSentence.Score;
                }
            }

            // Add final merged document
            mergedDocuments.Add(CreateMergedDocument(
                docGroup.Key,
                string.Join(" ", mergedContent),
                maxScore,
                currentMerge.OriginalSentence.Source,
                currentMerge));
        }

        // Sort by score and apply topK limit
        return mergedDocuments
            .OrderByDescending(d => d.Score)
            .Take(config.TopK)
            .ToList();
    }

    private static RetrievedDocument CreateMergedDocument(
        string documentId,
        string content,
        double score,
        string source,
        ExpandedContext context)
    {
        return RetrievedDocument.Create(
            $"sw-{documentId}-{context.WindowStart}-{context.WindowEnd}",
            content,
            score,
            source,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["searchType"] = "sentence-window",
                ["windowStart"] = context.WindowStart,
                ["windowEnd"] = context.WindowEnd,
                ["coreSentenceIndex"] = context.CoreSentenceIndex,
                ["originalDocumentId"] = documentId
            });
    }

    private static List<string> GetSimulatedSurroundingSentences(string[] allSentences, int currentIndex)
    {
        // Return surrounding sentences for window expansion simulation
        var start = Math.Max(0, currentIndex - 3);
        var end = Math.Min(allSentences.Length - 1, currentIndex + 3);

        return allSentences[start..(end + 1)].ToList();
    }

    private static async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        // Simulate embedding generation (production calls embedding service)
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);

        var embedding = new float[384];
        var hash = text.GetHashCode(StringComparison.Ordinal);
        var random = new Random(hash);

        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }

        // Normalize
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }

    private static int EstimateTokenCount(string text)
    {
        // Rough estimation: ~4 characters per token
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    private static string GetQueryFromPayload(JsonDocument payload)
    {
        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            return queryElement.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static SentenceWindowConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new SentenceWindowConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new SentenceWindowConfig
        {
            TopK = root.TryGetProperty("topK", out var tk) ? tk.GetInt32() : DefaultTopK,
            MinScore = root.TryGetProperty("minScore", out var ms) ? ms.GetDouble() : DefaultMinScore,
            Collection = root.TryGetProperty("collection", out var c) ? c.GetString() ?? "default" : "default",
            WindowSize = root.TryGetProperty("windowSize", out var ws) ? ws.GetInt32() : DefaultWindowSize
        };
    }

    /// <inheritdoc />
    protected override ValidationResult ValidateInputCore(PluginInput input)
    {
        var errors = new List<ValidationError>();

        if (!input.Payload.RootElement.TryGetProperty("query", out var queryElement) ||
            string.IsNullOrWhiteSpace(queryElement.GetString()))
        {
            errors.Add(new ValidationError
            {
                Message = "Query is required in payload",
                PropertyPath = "payload.query",
                Code = "MISSING_QUERY"
            });
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure([.. errors]);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateInputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "description": "Input for sentence window retrieval plugin",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The search query"
                }
            },
            "required": ["query"]
        }
        """);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateOutputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "description": "Output from sentence window retrieval plugin",
            "properties": {
                "documents": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "id": { "type": "string" },
                            "content": { "type": "string" },
                            "score": { "type": "number" },
                            "source": { "type": "string" },
                            "metadata": { "type": "object" }
                        }
                    }
                },
                "metrics": {
                    "type": "object",
                    "properties": {
                        "sentencesRetrieved": { "type": "integer" },
                        "windowsExpanded": { "type": "integer" },
                        "documentsAfterMerge": { "type": "integer" },
                        "totalTokens": { "type": "integer" },
                        "windowSize": { "type": "integer" }
                    }
                }
            },
            "required": ["documents", "metrics"]
        }
        """);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateConfigSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "description": "Configuration for sentence window retrieval plugin",
            "properties": {
                "topK": {
                    "type": "integer",
                    "minimum": 1,
                    "maximum": 20,
                    "default": 5,
                    "description": "Number of sentences to retrieve"
                },
                "minScore": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "default": 0.6,
                    "description": "Minimum relevance score threshold"
                },
                "collection": {
                    "type": "string",
                    "description": "Qdrant collection to search",
                    "default": "default"
                },
                "windowSize": {
                    "type": "integer",
                    "minimum": 1,
                    "maximum": 5,
                    "default": 2,
                    "description": "Number of sentences to expand on each side (±N)"
                }
            }
        }
        """);
    }

    #region Internal Types

    private sealed class SentenceWindowConfig
    {
        public int TopK { get; init; } = DefaultTopK;
        public double MinScore { get; init; } = DefaultMinScore;
        public string Collection { get; init; } = "default";
        public int WindowSize { get; init; } = DefaultWindowSize;
    }

    private sealed class RetrievedSentence
    {
        public required string Id { get; init; }
        public required string DocumentId { get; init; }
        public required int SentenceIndex { get; init; }
        public required string Content { get; init; }
        public required double Score { get; init; }
        public required string Source { get; init; }
        public List<string> SurroundingSentences { get; init; } = [];
    }

    private sealed record ExpandedContext
    {
        public required RetrievedSentence OriginalSentence { get; init; }
        public required string ExpandedContent { get; init; }
        public required int WindowStart { get; init; }
        public required int WindowEnd { get; init; }
        public required int CoreSentenceIndex { get; init; }
    }

    private sealed class SentenceWindowMetrics
    {
        public int SentencesRetrieved { get; init; }
        public int WindowsExpanded { get; init; }
        public int DocumentsAfterMerge { get; init; }
        public int TotalTokens { get; init; }
        public int WindowSize { get; init; }
    }

    #endregion
}
