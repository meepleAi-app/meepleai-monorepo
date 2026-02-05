// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3419 - Cache Plugins
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Cache;

/// <summary>
/// No-op cache plugin that always passes through.
/// Useful for bypassing cache in pipelines that require a cache node.
/// </summary>
[RagPlugin("cache-none-v1",
    Category = PluginCategory.Cache,
    Name = "No Cache",
    Description = "Bypass plugin that never caches - always returns cache miss",
    Author = "MeepleAI",
    Priority = 10)]
public sealed class CacheNonePlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "cache-none-v1";

    /// <inheritdoc />
    public override string Name => "No Cache";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Cache;

    /// <inheritdoc />
    protected override string Description => "Bypass plugin that never caches";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["cache", "bypass", "passthrough", "no-op"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["bypass"];

    public CacheNonePlugin(ILogger<CacheNonePlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        // Always return cache miss
        var result = JsonDocument.Parse("""{"cacheHit": false}""");

        Logger.LogDebug("Cache bypass: No caching applied");

        return Task.FromResult(new PluginOutput
        {
            ExecutionId = input.ExecutionId,
            Success = true,
            Result = result,
            Metrics = new PluginExecutionMetrics { CacheHit = false }
        });
    }

    /// <inheritdoc />
    protected override JsonDocument CreateInputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "description": "Input for no-cache plugin (accepts any payload)",
            "properties": {},
            "additionalProperties": true
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
            "description": "Output from no-cache plugin",
            "properties": {
                "cacheHit": {
                    "type": "boolean",
                    "const": false,
                    "description": "Always false for bypass cache"
                }
            },
            "required": ["cacheHit"]
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
            "description": "Configuration for no-cache plugin (no configuration needed)",
            "properties": {}
        }
        """);
    }
}
