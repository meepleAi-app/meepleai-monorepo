using System.Diagnostics;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;

/// <summary>
/// Orchestrates multi-source context assembly for AI agents.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
/// <remarks>
/// The ContextAssembler:
/// 1. Collects context from multiple sources (rules, memory, state, tools)
/// 2. Applies source-specific retrieval strategies
/// 3. Manages token budget across sources
/// 4. Assembles final context with priority-based ordering
/// </remarks>
public sealed class ContextAssembler
{
    private readonly IReadOnlyList<IContextSource> _sources;
    private readonly IReadOnlyList<IContextRetrievalStrategy> _strategies;
    private readonly ContextAssemblerOptions _options;

    public ContextAssembler(
        IEnumerable<IContextSource> sources,
        IEnumerable<IContextRetrievalStrategy> strategies,
        ContextAssemblerOptions? options = null)
    {
        _sources = sources?.ToList() ?? throw new ArgumentNullException(nameof(sources));
        _strategies = strategies?.ToList() ?? throw new ArgumentNullException(nameof(strategies));
        _options = options ?? new ContextAssemblerOptions();

        if (_sources.Count == 0)
            throw new ArgumentException("At least one context source is required", nameof(sources));
    }

    /// <summary>
    /// Assembles context from all registered sources for the given request.
    /// </summary>
    /// <param name="request">The assembly request with query and constraints.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Assembled context with all retrieved items and metrics.</returns>
    public async Task<AssembledContext> AssembleAsync(
        ContextAssemblyRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var metrics = new AssemblyMetrics();

        // Initialize budget manager
        var budgetManager = new ContextBudgetManager(request.MaxTotalTokens);
        foreach (var source in _sources)
        {
            var priority = GetDictionaryValueOrDefault(request.SourcePriorities, source.SourceId, source.DefaultPriority);
            var minTokens = GetDictionaryValueOrDefault(request.MinTokensPerSource, source.SourceId, 0);
            var maxTokens = GetDictionaryValueOrDefault(request.MaxTokensPerSource, source.SourceId, 0);

            budgetManager.RegisterSource(source.SourceId, priority, minTokens, maxTokens);
        }

        var allocations = budgetManager.CalculateAllocations();

        // Retrieve context from all sources (parallel or sequential)
        var retrievalResults = new List<ContextRetrievalResult>();

        if (_options.EnableParallelRetrieval)
        {
            var tasks = _sources.Select(async source =>
            {
                if (!await source.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
                {
                    metrics.RecordSourceUnavailable(source.SourceId);
                    return ContextRetrievalResult.Failure(source.SourceId, "Source unavailable");
                }

                var retrievalRequest = CreateRetrievalRequest(request, source, allocations);
                var sourceStopwatch = Stopwatch.StartNew();

                try
                {
                    var result = await source.RetrieveAsync(retrievalRequest, cancellationToken)
                        .ConfigureAwait(false);
                    sourceStopwatch.Stop();
                    metrics.RecordSourceRetrieval(source.SourceId, sourceStopwatch.ElapsedMilliseconds, result.Items.Count);
                    return result;
                }
                catch (Exception ex)
                {
                    sourceStopwatch.Stop();
                    metrics.RecordSourceError(source.SourceId, ex.Message);
                    return ContextRetrievalResult.Failure(source.SourceId, ex.Message);
                }
            });

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            retrievalResults.AddRange(results);
        }
        else
        {
            foreach (var source in _sources)
            {
                if (!await source.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
                {
                    metrics.RecordSourceUnavailable(source.SourceId);
                    retrievalResults.Add(ContextRetrievalResult.Failure(source.SourceId, "Source unavailable"));
                    continue;
                }

                var retrievalRequest = CreateRetrievalRequest(request, source, allocations);
                var sourceStopwatch = Stopwatch.StartNew();

                try
                {
                    var result = await source.RetrieveAsync(retrievalRequest, cancellationToken)
                        .ConfigureAwait(false);
                    sourceStopwatch.Stop();
                    metrics.RecordSourceRetrieval(source.SourceId, sourceStopwatch.ElapsedMilliseconds, result.Items.Count);
                    retrievalResults.Add(result);
                }
                catch (Exception ex)
                {
                    sourceStopwatch.Stop();
                    metrics.RecordSourceError(source.SourceId, ex.Message);
                    retrievalResults.Add(ContextRetrievalResult.Failure(source.SourceId, ex.Message));
                }
            }
        }

        // Apply strategies and collect items
        var allItems = new List<AssembledContextItem>();

        foreach (var result in retrievalResults.Where(r => r.IsSuccess))
        {
            var strategy = FindStrategy(result.SourceId);
            var strategyContext = new StrategyContext
            {
                Query = request.Query,
                ReferenceTime = DateTime.UtcNow,
                QueryEmbedding = request.QueryEmbedding,
                MaxItems = GetReadOnlyDictionaryValueOrDefault(allocations, result.SourceId, _options.DefaultMaxItemsPerSource),
                MinScore = request.MinRelevance
            };

            var processedItems = strategy?.Apply(result.Items, strategyContext) ?? result.Items;

            foreach (var item in processedItems)
            {
                allItems.Add(new AssembledContextItem
                {
                    SourceId = result.SourceId,
                    Item = item,
                    Priority = GetDictionaryValueOrDefault(request.SourcePriorities, result.SourceId, 50)
                });
            }

            budgetManager.RecordUsage(result.SourceId, result.TotalTokens);
        }

        // Sort by priority and relevance, then trim to budget
        var sortedItems = allItems
            .OrderByDescending(i => i.Priority)
            .ThenByDescending(i => i.Item.Relevance)
            .ToList();

        var finalItems = new List<AssembledContextItem>();
        var totalTokens = 0;

        foreach (var item in sortedItems)
        {
            if (totalTokens + item.Item.TokenCount <= request.MaxTotalTokens)
            {
                finalItems.Add(item);
                totalTokens += item.Item.TokenCount;
            }
            else
            {
                // Budget exceeded - stop adding items
                // Note: _options.AllowPartialItems could be used for truncation in future
                break;
            }
        }

        stopwatch.Stop();
        metrics.TotalDurationMs = stopwatch.ElapsedMilliseconds;
        metrics.TotalItemsRetrieved = finalItems.Count;
        metrics.TotalTokensUsed = totalTokens;

        return new AssembledContext
        {
            Query = request.Query,
            Items = finalItems,
            TotalTokens = totalTokens,
            BudgetSnapshot = budgetManager.CreateSnapshot(),
            Metrics = metrics,
            AssembledAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Builds the formatted context string for injection into a prompt.
    /// </summary>
    public static string BuildContextString(
        AssembledContext context,
        ContextFormatOptions? formatOptions = null)
    {
        var options = formatOptions ?? new ContextFormatOptions();
        var builder = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(options.Preamble))
        {
            builder.AppendLine(options.Preamble);
            builder.AppendLine();
        }

        // Group by source if requested
        if (options.GroupBySource)
        {
            var grouped = context.Items.GroupBy(i => i.SourceId, StringComparer.Ordinal);
            foreach (var group in grouped)
            {
                if (options.IncludeSourceHeaders)
                {
                    builder.AppendLine($"## {FormatSourceName(group.Key)}");
                    builder.AppendLine();
                }

                foreach (var item in group)
                {
                    AppendItem(builder, item, options);
                }
            }
        }
        else
        {
            foreach (var item in context.Items)
            {
                AppendItem(builder, item, options);
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendItem(
        System.Text.StringBuilder builder,
        AssembledContextItem item,
        ContextFormatOptions options)
    {
        if (options.IncludeMetadata)
        {
            builder.AppendLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"[{item.SourceId}:{item.Item.ContentType}] (relevance: {item.Item.Relevance:F2})"));
        }

        builder.AppendLine(item.Item.Content);
        builder.AppendLine();
    }

    private static string FormatSourceName(string sourceId)
    {
        return sourceId switch
        {
            "static_knowledge" => "Knowledge Base",
            "conversation_memory" => "Recent Conversation",
            "game_state" => "Current Game State",
            "tool_metadata" => "Available Actions",
            _ => sourceId.Replace("_", " ", StringComparison.Ordinal).ToUpper(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private IContextRetrievalStrategy? FindStrategy(string sourceId)
    {
        var source = _sources.FirstOrDefault(s => string.Equals(s.SourceId, sourceId, StringComparison.Ordinal));
        if (source == null) return null;

        // Find strategy that supports this source type
        return _strategies.FirstOrDefault(s =>
            s.SupportedSourceTypes.Any(t =>
                sourceId.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    private static ContextRetrievalRequest CreateRetrievalRequest(
        ContextAssemblyRequest request,
        IContextSource source,
        IReadOnlyDictionary<string, int> allocations)
    {
        return new ContextRetrievalRequest
        {
            Query = request.Query,
            GameId = request.GameId,
            UserId = request.UserId,
            SessionId = request.SessionId,
            MaxTokens = GetReadOnlyDictionaryValueOrDefault(allocations, source.SourceId, 1000),
            MaxItems = 20, // Retrieve more, strategy will filter
            MinRelevance = request.MinRelevance,
            QueryEmbedding = request.QueryEmbedding,
            Metadata = request.Metadata
        };
    }

    private static TValue GetDictionaryValueOrDefault<TValue>(IDictionary<string, TValue>? dictionary, string key, TValue defaultValue)
    {
        if (dictionary == null)
            return defaultValue;

        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    private static TValue GetReadOnlyDictionaryValueOrDefault<TValue>(IReadOnlyDictionary<string, TValue> dictionary, string key, TValue defaultValue)
    {
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }
}

/// <summary>
/// Options for the ContextAssembler.
/// </summary>
public sealed record ContextAssemblerOptions
{
    /// <summary>
    /// Whether to retrieve from sources in parallel.
    /// </summary>
    public bool EnableParallelRetrieval { get; init; } = true;

    /// <summary>
    /// Default maximum items per source if not specified.
    /// </summary>
    public int DefaultMaxItemsPerSource { get; init; } = 10;

    /// <summary>
    /// Whether to allow partial items when hitting budget limits.
    /// </summary>
    public bool AllowPartialItems { get; init; }

    /// <summary>
    /// Timeout for individual source retrieval in milliseconds.
    /// </summary>
    public int RetrievalTimeoutMs { get; init; } = 5000;
}

/// <summary>
/// Request for context assembly.
/// </summary>
public sealed record ContextAssemblyRequest
{
    /// <summary>
    /// The user's query.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Optional game context.
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// Optional user context.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Optional session context.
    /// </summary>
    public Guid? SessionId { get; init; }

    /// <summary>
    /// Maximum total tokens for assembled context.
    /// </summary>
    public int MaxTotalTokens { get; init; } = 8000;

    /// <summary>
    /// Minimum relevance score (0.0-1.0).
    /// </summary>
    public double MinRelevance { get; init; } = 0.5;

    /// <summary>
    /// Optional query embedding for similarity search.
    /// </summary>
    public float[]? QueryEmbedding { get; init; }

    /// <summary>
    /// Priority overrides by source ID (0-100).
    /// </summary>
    public IDictionary<string, int>? SourcePriorities { get; init; }

    /// <summary>
    /// Minimum token allocation per source.
    /// </summary>
    public IDictionary<string, int>? MinTokensPerSource { get; init; }

    /// <summary>
    /// Maximum token allocation per source.
    /// </summary>
    public IDictionary<string, int>? MaxTokensPerSource { get; init; }

    /// <summary>
    /// Additional metadata for retrieval.
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Result of context assembly.
/// </summary>
public sealed record AssembledContext
{
    /// <summary>
    /// The original query.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Assembled context items sorted by priority and relevance.
    /// </summary>
    public required IReadOnlyList<AssembledContextItem> Items { get; init; }

    /// <summary>
    /// Total tokens used.
    /// </summary>
    public int TotalTokens { get; init; }

    /// <summary>
    /// Budget allocation snapshot.
    /// </summary>
    public required ContextBudgetSnapshot BudgetSnapshot { get; init; }

    /// <summary>
    /// Assembly performance metrics.
    /// </summary>
    public required AssemblyMetrics Metrics { get; init; }

    /// <summary>
    /// When the context was assembled.
    /// </summary>
    public DateTime AssembledAt { get; init; }
}

/// <summary>
/// A single item in the assembled context.
/// </summary>
public sealed record AssembledContextItem
{
    /// <summary>
    /// Source that provided this item.
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// The retrieved item.
    /// </summary>
    public required RetrievedContextItem Item { get; init; }

    /// <summary>
    /// Priority of the source.
    /// </summary>
    public int Priority { get; init; }
}

/// <summary>
/// Metrics for context assembly performance.
/// </summary>
public sealed class AssemblyMetrics
{
    private readonly Dictionary<string, SourceMetrics> _sourceMetrics = new(StringComparer.Ordinal);

    /// <summary>
    /// Total assembly duration in milliseconds.
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// Total items retrieved across all sources.
    /// </summary>
    public int TotalItemsRetrieved { get; set; }

    /// <summary>
    /// Total tokens used.
    /// </summary>
    public int TotalTokensUsed { get; set; }

    /// <summary>
    /// Gets metrics by source.
    /// </summary>
    public IReadOnlyDictionary<string, SourceMetrics> BySource => _sourceMetrics;

    public void RecordSourceRetrieval(string sourceId, long durationMs, int itemCount)
    {
        _sourceMetrics[sourceId] = new SourceMetrics
        {
            DurationMs = durationMs,
            ItemCount = itemCount,
            IsAvailable = true,
            Error = null
        };
    }

    public void RecordSourceError(string sourceId, string error)
    {
        _sourceMetrics[sourceId] = new SourceMetrics
        {
            DurationMs = 0,
            ItemCount = 0,
            IsAvailable = true,
            Error = error
        };
    }

    public void RecordSourceUnavailable(string sourceId)
    {
        _sourceMetrics[sourceId] = new SourceMetrics
        {
            DurationMs = 0,
            ItemCount = 0,
            IsAvailable = false,
            Error = "Source unavailable"
        };
    }
}

/// <summary>
/// Metrics for a single source.
/// </summary>
public sealed record SourceMetrics
{
    public long DurationMs { get; init; }
    public int ItemCount { get; init; }
    public bool IsAvailable { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Options for formatting assembled context.
/// </summary>
public sealed record ContextFormatOptions
{
    /// <summary>
    /// Text to prepend to the context.
    /// </summary>
    public string? Preamble { get; init; }

    /// <summary>
    /// Whether to group items by source.
    /// </summary>
    public bool GroupBySource { get; init; } = true;

    /// <summary>
    /// Whether to include source section headers.
    /// </summary>
    public bool IncludeSourceHeaders { get; init; } = true;

    /// <summary>
    /// Whether to include item metadata.
    /// </summary>
    public bool IncludeMetadata { get; init; }
}
