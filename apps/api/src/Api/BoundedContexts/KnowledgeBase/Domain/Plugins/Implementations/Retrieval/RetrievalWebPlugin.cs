// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3420 - Retrieval Plugins
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
/// Web search augmentation plugin for extended knowledge retrieval.
/// Uses external search engines when local knowledge is insufficient.
/// </summary>
[RagPlugin("retrieval-web-v1",
    Category = PluginCategory.Retrieval,
    Name = "Web Retrieval",
    Description = "Web search augmentation for missing knowledge",
    Author = "MeepleAI")]
public sealed class RetrievalWebPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "retrieval-web-v1";

    /// <inheritdoc />
    public override string Name => "Web Retrieval";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Retrieval;

    /// <inheritdoc />
    protected override string Description => "Web search augmentation for missing knowledge";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["retrieval", "web", "search", "augmentation"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["web-search", "domain-filtering", "content-extraction"];

    public RetrievalWebPlugin(ILogger<RetrievalWebPlugin> logger) : base(logger)
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

        // Perform web search
        var documents = await PerformWebSearchAsync(query, customConfig, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            documents = documents.Select(d => new
            {
                id = d.Id,
                content = d.Content,
                score = d.Score,
                source = d.Source,
                metadata = d.Metadata
            }),
            webSearchUsed = true
        }));

        Logger.LogInformation(
            "Web retrieval: Engine={Engine}, Results={Count}, Duration={Duration:F0}ms",
            customConfig.SearchEngine, documents.Count, stopwatch.Elapsed.TotalMilliseconds);

        return new PluginOutput
        {
            ExecutionId = input.ExecutionId,
            Success = true,
            Result = result,
            Confidence = documents.Count > 0 ? documents.Max(d => d.Score) : 0,
            Metrics = new PluginExecutionMetrics
            {
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                ItemsProcessed = documents.Count
            }
        };
    }

    private static async Task<List<RetrievedDocument>> PerformWebSearchAsync(
        string query,
        WebConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate web search (production integrates with Tavily, Serper, etc.)
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);

        var results = new List<RetrievedDocument>();
        var random = new Random(query.GetHashCode(StringComparison.Ordinal));

        // Generate simulated web search results
        var count = Math.Min(random.Next(3, 8), config.MaxResults);
        var domains = new[] { "boardgamegeek.com", "reddit.com/r/boardgames", "wiki.example.com", "rulebook.io" };

        for (int i = 0; i < count; i++)
        {
            var domain = domains[i % domains.Length];

            // Check domain filter
            if (config.FilterDomains.Count > 0 && !config.FilterDomains.Any(f => domain.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var score = 0.85 - (i * 0.10) + (random.NextDouble() * 0.08);
            results.Add(RetrievedDocument.Create(
                $"web-{Guid.NewGuid():N}",
                $"[Web] Result from {domain}: Information about '{query}'",
                score,
                $"web:{domain}",
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["searchEngine"] = config.SearchEngine,
                    ["domain"] = domain,
                    ["url"] = $"https://{domain}/article/{random.Next(1000, 9999)}",
                    ["rank"] = i + 1
                }));
        }

        return results.Take(config.MaxResults).ToList();
    }

    private static string GetQueryFromPayload(JsonDocument payload)
    {
        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            return queryElement.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static WebConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new WebConfig();
        }

        var root = config.CustomConfig.RootElement;
        var filterDomains = new List<string>();

        if (root.TryGetProperty("filterDomains", out var domainsElement) && domainsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var domain in domainsElement.EnumerateArray())
            {
                var d = domain.GetString();
                if (!string.IsNullOrEmpty(d))
                {
                    filterDomains.Add(d);
                }
            }
        }

        return new WebConfig
        {
            SearchEngine = root.TryGetProperty("searchEngine", out var se) ? se.GetString() ?? "tavily" : "tavily",
            MaxResults = root.TryGetProperty("maxResults", out var mr) ? mr.GetInt32() : 5,
            FilterDomains = filterDomains
        };
    }

    /// <inheritdoc />
    protected override async Task<HealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken)
    {
        // Check if web search service is available
        try
        {
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            // In production, ping the actual search API
            return HealthCheckResult.Healthy("Web search service is available");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Web search service unavailable: {ex.Message}");
        }
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
    protected override ValidationResult ValidateConfigCore(PluginConfig config)
    {
        var errors = new List<ValidationError>();

        if (config.CustomConfig != null)
        {
            var root = config.CustomConfig.RootElement;

            if (root.TryGetProperty("searchEngine", out var se))
            {
                var engine = se.GetString();
                var validEngines = new[] { "tavily", "serper", "google" };
                if (!string.IsNullOrEmpty(engine) && !validEngines.Contains(engine, StringComparer.Ordinal))
                {
                    errors.Add(new ValidationError
                    {
                        Message = $"Invalid search engine. Must be one of: {string.Join(", ", validEngines)}",
                        PropertyPath = "customConfig.searchEngine",
                        Code = "INVALID_SEARCH_ENGINE",
                        AttemptedValue = engine
                    });
                }
            }

            if (root.TryGetProperty("maxResults", out var mr) && mr.GetInt32() <= 0)
            {
                errors.Add(new ValidationError
                {
                    Message = "maxResults must be greater than 0",
                    PropertyPath = "customConfig.maxResults",
                    Code = "INVALID_MAX_RESULTS"
                });
            }
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
            "description": "Input for web retrieval plugin",
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
            "description": "Output from web retrieval plugin",
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
                "webSearchUsed": {
                    "type": "boolean"
                }
            },
            "required": ["documents", "webSearchUsed"]
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
            "description": "Configuration for web retrieval plugin",
            "properties": {
                "searchEngine": {
                    "type": "string",
                    "enum": ["tavily", "serper", "google"],
                    "default": "tavily"
                },
                "maxResults": {
                    "type": "integer",
                    "minimum": 1,
                    "maximum": 20,
                    "default": 5
                },
                "filterDomains": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "Only include results from these domains"
                }
            }
        }
        """);
    }

    private sealed class WebConfig
    {
        public string SearchEngine { get; init; } = "tavily";
        public int MaxResults { get; init; } = 5;
        public IReadOnlyList<string> FilterDomains { get; init; } = [];
    }
}
