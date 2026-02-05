// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3424 - Transform/Filter Plugins
// =============================================================================

using System.Text.Json;
using System.Text.RegularExpressions;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Filter;

/// <summary>
/// Content safety and quality filter plugin.
/// Filters documents based on content policies, quality thresholds, and custom rules.
/// </summary>
[RagPlugin("filter-content-v1",
    Category = PluginCategory.Filter,
    Name = "Content Filter",
    Description = "Filters content based on safety and quality policies",
    Author = "MeepleAI")]
public sealed class FilterContentPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "filter-content-v1";

    /// <inheritdoc />
    public override string Name => "Content Filter";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Filter;

    /// <inheritdoc />
    protected override string Description => "Filters content based on safety and quality policies";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["filter", "content", "safety", "quality", "moderation"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["safety-filter", "quality-filter", "custom-rules"];

    // Default blocked patterns for safety
    private static readonly string[] DefaultBlockedPatterns =
    [
        @"\b(password|secret|api[_-]?key|private[_-]?key)\b",
        @"\b\d{3}-\d{2}-\d{4}\b", // SSN pattern
        @"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b" // Credit card pattern
    ];

    public FilterContentPlugin(ILogger<FilterContentPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var documents = ParseDocuments(input.Payload);

        if (documents.Count == 0)
        {
            var emptyResult = JsonDocument.Parse("""{"documents": [], "filteredCount": 0, "reasons": []}""");
            return Task.FromResult(PluginOutput.Successful(input.ExecutionId, emptyResult));
        }

        var customConfig = ParseCustomConfig(config);
        var filteredDocs = new List<DocumentItem>();
        var filterReasons = new List<FilterReason>();

        foreach (var doc in documents)
        {
            var (passed, reason) = EvaluateDocument(doc, customConfig);

            if (passed)
            {
                filteredDocs.Add(doc);
            }
            else
            {
                filterReasons.Add(new FilterReason(doc.Id, reason));
            }
        }

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            documents = filteredDocs.Select(d => new
            {
                id = d.Id,
                content = d.Content,
                score = d.Score,
                source = d.Source
            }),
            filteredCount = documents.Count - filteredDocs.Count,
            reasons = filterReasons.Select(r => new
            {
                documentId = r.DocumentId,
                reason = r.Reason
            })
        }));

        Logger.LogInformation(
            "Content filtering: Input={Input}, Passed={Passed}, Filtered={Filtered}",
            documents.Count, filteredDocs.Count, filterReasons.Count);

        return Task.FromResult(PluginOutput.Successful(input.ExecutionId, result));
    }

    private static (bool Passed, string Reason) EvaluateDocument(DocumentItem doc, ContentFilterConfig config)
    {
        // Quality filters
        if (config.MinLength.HasValue && doc.Content.Length < config.MinLength.Value)
        {
            return (false, $"Content too short: {doc.Content.Length} < {config.MinLength.Value}");
        }

        if (config.MaxLength.HasValue && doc.Content.Length > config.MaxLength.Value)
        {
            return (false, $"Content too long: {doc.Content.Length} > {config.MaxLength.Value}");
        }

        if (config.MinScore.HasValue && doc.Score < config.MinScore.Value)
        {
            return (false, $"Score below threshold: {doc.Score:F2} < {config.MinScore.Value:F2}");
        }

        // Word count filter
        var wordCount = doc.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (config.MinWords.HasValue && wordCount < config.MinWords.Value)
        {
            return (false, $"Too few words: {wordCount} < {config.MinWords.Value}");
        }

        // Safety filters - check blocked patterns
        if (config.EnableSafetyFilter)
        {
            var patterns = config.BlockedPatterns.Count > 0
                ? config.BlockedPatterns
                : DefaultBlockedPatterns.ToList();

            foreach (var pattern in patterns)
            {
                try
                {
                    if (Regex.IsMatch(doc.Content, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
                    {
                        return (false, $"Content matches blocked pattern: {pattern}");
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // Skip pattern on timeout
                }
            }
        }

        // Custom blocked words
        if (config.BlockedWords.Count > 0)
        {
            var contentLower = doc.Content.ToLowerInvariant();
            foreach (var word in config.BlockedWords)
            {
                if (contentLower.Contains(word.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"Content contains blocked word: {word}");
                }
            }
        }

        // Required words filter
        if (config.RequiredWords.Count > 0)
        {
            var contentLower = doc.Content.ToLowerInvariant();
            var missingWords = config.RequiredWords
                .Where(w => !contentLower.Contains(w.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (missingWords.Count > 0)
            {
                return (false, $"Missing required words: {string.Join(", ", missingWords)}");
            }
        }

        // Allowed sources filter
        if (config.AllowedSources.Count > 0 &&
            !config.AllowedSources.Contains(doc.Source, StringComparer.OrdinalIgnoreCase))
        {
            return (false, $"Source not in allowed list: {doc.Source}");
        }

        // Blocked sources filter
        if (config.BlockedSources.Count > 0 &&
            config.BlockedSources.Contains(doc.Source, StringComparer.OrdinalIgnoreCase))
        {
            return (false, $"Source in blocked list: {doc.Source}");
        }

        return (true, string.Empty);
    }

    private static List<DocumentItem> ParseDocuments(JsonDocument payload)
    {
        var documents = new List<DocumentItem>();

        if (payload.RootElement.TryGetProperty("documents", out var docsElement) &&
            docsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var doc in docsElement.EnumerateArray())
            {
                var id = doc.TryGetProperty("id", out var i) ? i.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                var content = doc.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                var score = doc.TryGetProperty("score", out var s) ? s.GetDouble() : 1.0;
                var source = doc.TryGetProperty("source", out var src) ? src.GetString() ?? "unknown" : "unknown";
                documents.Add(new DocumentItem(id, content, score, source));
            }
        }

        return documents;
    }

    private static ContentFilterConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new ContentFilterConfig();
        }

        var root = config.CustomConfig.RootElement;
        var filterConfig = new ContentFilterConfig
        {
            EnableSafetyFilter = !root.TryGetProperty("enableSafetyFilter", out var esf) || esf.GetBoolean(),
            MinLength = root.TryGetProperty("minLength", out var minLen) ? minLen.GetInt32() : null,
            MaxLength = root.TryGetProperty("maxLength", out var maxLen) ? maxLen.GetInt32() : null,
            MinScore = root.TryGetProperty("minScore", out var minScore) ? minScore.GetDouble() : null,
            MinWords = root.TryGetProperty("minWords", out var minWords) ? minWords.GetInt32() : null
        };

        if (root.TryGetProperty("blockedPatterns", out var bp) && bp.ValueKind == JsonValueKind.Array)
        {
            filterConfig.BlockedPatterns = bp.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        if (root.TryGetProperty("blockedWords", out var bw) && bw.ValueKind == JsonValueKind.Array)
        {
            filterConfig.BlockedWords = bw.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        if (root.TryGetProperty("requiredWords", out var rw) && rw.ValueKind == JsonValueKind.Array)
        {
            filterConfig.RequiredWords = rw.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        if (root.TryGetProperty("allowedSources", out var als) && als.ValueKind == JsonValueKind.Array)
        {
            filterConfig.AllowedSources = als.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        if (root.TryGetProperty("blockedSources", out var bs) && bs.ValueKind == JsonValueKind.Array)
        {
            filterConfig.BlockedSources = bs.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        return filterConfig;
    }

    /// <inheritdoc />
    protected override JsonDocument CreateInputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "documents": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "id": { "type": "string" },
                            "content": { "type": "string" },
                            "score": { "type": "number" },
                            "source": { "type": "string" }
                        }
                    }
                }
            },
            "required": ["documents"]
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
            "properties": {
                "documents": { "type": "array" },
                "filteredCount": { "type": "integer" },
                "reasons": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "documentId": { "type": "string" },
                            "reason": { "type": "string" }
                        }
                    }
                }
            },
            "required": ["documents", "filteredCount"]
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
            "properties": {
                "enableSafetyFilter": { "type": "boolean", "default": true },
                "minLength": { "type": "integer", "minimum": 0 },
                "maxLength": { "type": "integer", "minimum": 1 },
                "minScore": { "type": "number", "minimum": 0, "maximum": 1 },
                "minWords": { "type": "integer", "minimum": 0 },
                "blockedPatterns": { "type": "array", "items": { "type": "string" } },
                "blockedWords": { "type": "array", "items": { "type": "string" } },
                "requiredWords": { "type": "array", "items": { "type": "string" } },
                "allowedSources": { "type": "array", "items": { "type": "string" } },
                "blockedSources": { "type": "array", "items": { "type": "string" } }
            }
        }
        """);
    }

    private sealed record DocumentItem(string Id, string Content, double Score, string Source);
    private sealed record FilterReason(string DocumentId, string Reason);

    private sealed class ContentFilterConfig
    {
        public bool EnableSafetyFilter { get; init; } = true;
        public int? MinLength { get; init; }
        public int? MaxLength { get; init; }
        public double? MinScore { get; init; }
        public int? MinWords { get; init; }
        public List<string> BlockedPatterns { get; set; } = [];
        public List<string> BlockedWords { get; set; } = [];
        public List<string> RequiredWords { get; set; } = [];
        public List<string> AllowedSources { get; set; } = [];
        public List<string> BlockedSources { get; set; } = [];
    }
}
