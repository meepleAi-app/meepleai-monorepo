# Migration Guide

> **Migrating from Fixed Pipeline to Plugin Architecture**

This guide helps you migrate existing RAG implementations to the new plugin-based architecture. It covers converting hard-coded pipelines to composable plugins.

## Overview

### Before: Fixed Pipeline

```csharp
// Old architecture - monolithic, hard-coded flow
public class RagService
{
    public async Task<string> ProcessQueryAsync(string query)
    {
        // 1. Fixed routing logic
        var queryType = ClassifyQuery(query);

        // 2. Hard-coded retrieval
        var docs = await _vectorStore.SearchAsync(query, topK: 10);

        // 3. Inline evaluation
        var relevantDocs = docs.Where(d => d.Score > 0.7);

        // 4. Fixed generation
        var response = await _llm.GenerateAsync(query, relevantDocs);

        return response;
    }
}
```

### After: Plugin Architecture

```csharp
// New architecture - composable, configurable
public class RagPipelineService
{
    private readonly IPipelineOrchestrator _orchestrator;

    public async Task<PipelineResult> ProcessQueryAsync(string query, string pipelineId)
    {
        var input = new PipelineInput { Query = query };
        return await _orchestrator.ExecuteAsync(pipelineId, input);
    }
}

// Pipeline defined in JSON, configurable via UI
{
  "id": "crag-pipeline",
  "nodes": [
    { "id": "router", "pluginId": "routing-intent-v1" },
    { "id": "retrieval", "pluginId": "retrieval-vector-v1" },
    { "id": "evaluation", "pluginId": "evaluation-relevance-v1" },
    { "id": "generation", "pluginId": "generation-answer-v1" }
  ]
}
```

## Migration Steps

### Step 1: Identify Pipeline Components

Map your existing code to plugin categories:

| Existing Code | Plugin Category | Plugin Type |
|---------------|-----------------|-------------|
| Query classification | Routing | `routing-intent-v1` |
| Caching logic | Cache | `cache-semantic-v1` |
| Vector search | Retrieval | `retrieval-vector-v1` |
| Score filtering | Filter | `filter-threshold-v1` |
| Relevance checking | Evaluation | `evaluation-relevance-v1` |
| LLM generation | Generation | `generation-answer-v1` |
| Output validation | Validation | `validation-hallucination-v1` |

### Step 2: Extract Configuration

Move hard-coded values to plugin configuration:

**Before**:
```csharp
var docs = await _vectorStore.SearchAsync(query, topK: 10);
var filtered = docs.Where(d => d.Score > 0.7);
```

**After**:
```json
{
  "id": "retrieval",
  "pluginId": "retrieval-vector-v1",
  "config": {
    "topK": 10,
    "similarityThreshold": 0.7
  }
}
```

### Step 3: Create Pipeline Definition

Build the JSON pipeline definition:

```json
{
  "id": "migrated-pipeline",
  "name": "Migrated RAG Pipeline",
  "version": "1.0.0",
  "nodes": [
    {
      "id": "retrieval",
      "pluginId": "retrieval-vector-v1",
      "config": {
        "topK": 10,
        "similarityThreshold": 0.7
      }
    },
    {
      "id": "generation",
      "pluginId": "generation-answer-v1",
      "config": {
        "model": "gpt-4-turbo",
        "maxTokens": 500
      }
    }
  ],
  "edges": [
    {
      "id": "e1",
      "source": "retrieval",
      "target": "generation"
    }
  ]
}
```

### Step 4: Update Service Layer

Replace direct implementation with orchestrator calls:

**Before**:
```csharp
public class GameAssistantService
{
    private readonly IVectorStore _vectorStore;
    private readonly ILlmService _llm;

    public async Task<string> AskAsync(string question, Guid gameId)
    {
        var docs = await _vectorStore.SearchAsync(question, gameId);
        return await _llm.GenerateAsync(question, docs);
    }
}
```

**After**:
```csharp
public class GameAssistantService
{
    private readonly IPipelineOrchestrator _orchestrator;

    public async Task<AssistantResponse> AskAsync(string question, Guid gameId)
    {
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = JsonDocument.Parse($$$"""{"query": "{{{question}}}"}"""),
            GameId = gameId
        };

        var result = await _orchestrator.ExecuteAsync("game-assistant-pipeline", input);

        return new AssistantResponse
        {
            Answer = result.FinalOutput.Result.GetProperty("response").GetString(),
            Citations = ExtractCitations(result)
        };
    }
}
```

### Step 5: Migrate Custom Logic to Plugins

Convert custom processing to plugins:

**Before** (inline logic):
```csharp
private string ClassifyQuery(string query)
{
    if (query.Contains("setup") || query.Contains("start"))
        return "rules";
    if (query.Contains("strategy") || query.Contains("best"))
        return "strategy";
    return "general";
}
```

**After** (custom plugin):
```csharp
public class SimpleQueryRouter : RagPluginBase
{
    public override string Id => "routing-simple-v1";
    public override string Name => "Simple Query Router";
    public override string Version => "1.0.0";
    public override PluginCategory Category => PluginCategory.Routing;

    public SimpleQueryRouter(ILogger logger) : base(logger) { }

    protected override Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input, PluginConfig config, CancellationToken ct)
    {
        var query = input.Payload.RootElement.GetProperty("query").GetString()!;

        var queryType = query.ToLower() switch
        {
            var q when q.Contains("setup") || q.Contains("start") => "rules",
            var q when q.Contains("strategy") || q.Contains("best") => "strategy",
            _ => "general"
        };

        var result = JsonDocument.Parse($$$"""
            {
                "queryType": "{{{queryType}}}",
                "confidence": 1.0
            }
            """);

        return Task.FromResult(PluginOutput.Successful(input.ExecutionId, result));
    }

    protected override JsonDocument CreateInputSchema() =>
        JsonDocument.Parse("""{"type":"object","properties":{"query":{"type":"string"}}}""");

    protected override JsonDocument CreateOutputSchema() =>
        JsonDocument.Parse("""{"type":"object","properties":{"queryType":{"type":"string"}}}""");

    protected override JsonDocument CreateConfigSchema() =>
        CreateBasicSchema("object", "No configuration required");
}
```

---

## Common Migration Patterns

### Pattern 1: Simple Linear Pipeline

**Before**:
```csharp
var docs = await Search(query);
var response = await Generate(query, docs);
return response;
```

**After**:
```json
{
  "nodes": [
    { "id": "search", "pluginId": "retrieval-vector-v1" },
    { "id": "generate", "pluginId": "generation-answer-v1" }
  ],
  "edges": [
    { "source": "search", "target": "generate" }
  ]
}
```

### Pattern 2: Conditional Routing

**Before**:
```csharp
if (IsRulesQuery(query))
    docs = await SearchRules(query);
else
    docs = await SearchStrategy(query);
```

**After**:
```json
{
  "nodes": [
    { "id": "router", "pluginId": "routing-intent-v1" },
    { "id": "rules-search", "pluginId": "retrieval-vector-v1", "config": { "namespace": "rules" } },
    { "id": "strategy-search", "pluginId": "retrieval-vector-v1", "config": { "namespace": "strategy" } }
  ],
  "edges": [
    { "source": "router", "target": "rules-search", "condition": "output.result.queryType === 'rules'" },
    { "source": "router", "target": "strategy-search", "condition": "output.result.queryType !== 'rules'" }
  ]
}
```

### Pattern 3: Caching Layer

**Before**:
```csharp
var cached = await _cache.GetAsync(query);
if (cached != null) return cached;

var result = await ProcessQuery(query);
await _cache.SetAsync(query, result);
return result;
```

**After**:
```json
{
  "nodes": [
    { "id": "cache", "pluginId": "cache-semantic-v1" },
    { "id": "process", "pluginId": "retrieval-vector-v1" }
  ],
  "edges": [
    { "source": "cache", "target": "exit", "condition": "output.result.cacheHit" },
    { "source": "cache", "target": "process", "condition": "!output.result.cacheHit" }
  ]
}
```

### Pattern 4: Quality Gates

**Before**:
```csharp
var docs = await Search(query);
if (docs.Average(d => d.Score) < 0.7)
{
    query = await RewriteQuery(query);
    docs = await Search(query);
}
```

**After**:
```json
{
  "nodes": [
    { "id": "search", "pluginId": "retrieval-vector-v1" },
    { "id": "evaluate", "pluginId": "evaluation-relevance-v1" },
    { "id": "rewrite", "pluginId": "transform-rewrite-v1" }
  ],
  "edges": [
    { "source": "search", "target": "evaluate" },
    { "source": "evaluate", "target": "generate", "condition": "output.result.averageRelevance >= 0.7" },
    { "source": "evaluate", "target": "rewrite", "condition": "output.result.averageRelevance < 0.7" },
    { "source": "rewrite", "target": "search" }
  ]
}
```

---

## Dependency Injection Setup

### Register Plugin Services

```csharp
// Program.cs or Startup.cs
services.AddRagPluginSystem(options =>
{
    // Register built-in plugins
    options.RegisterBuiltInPlugins();

    // Register custom plugins
    options.RegisterPlugin<SimpleQueryRouter>();
    options.RegisterPlugin<CustomRetrievalPlugin>();

    // Configure orchestrator
    options.ConfigureOrchestrator(orchestrator =>
    {
        orchestrator.DefaultTimeout = TimeSpan.FromSeconds(30);
        orchestrator.MaxConcurrentNodes = 5;
    });
});
```

### Register Dependencies

```csharp
// Plugin dependencies
services.AddSingleton<IVectorStore, QdrantVectorStore>();
services.AddSingleton<IEmbeddingService, OpenAIEmbeddingService>();
services.AddSingleton<ILlmService, OpenRouterLlmService>();

// Pipeline storage
services.AddSingleton<IPipelineRepository, PostgresPipelineRepository>();
```

---

## Testing Migrated Pipelines

### Unit Test Plugins

```csharp
public class MigratedRouterTests : PluginTestHarness<SimpleQueryRouter>
{
    protected override SimpleQueryRouter CreatePlugin() => new(Logger);

    [Theory]
    [InlineData("How do I setup the game?", "rules")]
    [InlineData("What's the best strategy?", "strategy")]
    [InlineData("Tell me about this game", "general")]
    public async Task ClassifiesQueriesCorrectly(string query, string expected)
    {
        var input = PluginMocks.CreateQueryInput(query);
        var output = await Plugin.ExecuteAsync(input);

        output.Result!.RootElement
            .GetProperty("queryType").GetString()
            .Should().Be(expected);
    }
}
```

### Integration Test Pipelines

```csharp
[Fact]
public async Task MigratedPipeline_ProducesEquivalentResults()
{
    // Arrange
    var query = "How do I setup Catan?";

    // Old implementation
    var oldService = new LegacyRagService(_vectorStore, _llm);
    var oldResult = await oldService.ProcessQueryAsync(query);

    // New implementation
    var newResult = await _orchestrator.ExecuteAsync("migrated-pipeline",
        new PluginInput { Query = query });

    // Assert - results should be equivalent
    newResult.Success.Should().BeTrue();
    // Compare response quality, not exact match (LLM outputs vary)
    newResult.FinalOutput.Result.GetProperty("response").GetString()
        .Should().ContainAny("setup", "tiles", "board");
}
```

### Compare Performance

```csharp
[Fact]
public async Task MigratedPipeline_MeetsPerformanceTargets()
{
    var benchmark = await PluginBenchmarks.RunBenchmarkAsync(
        _orchestrator,
        "migrated-pipeline",
        () => PluginMocks.CreateQueryInput("test query"),
        options: BenchmarkOptions.Standard
    );

    // Should not be significantly slower than original
    benchmark.MeanMs.Should().BeLessThan(OriginalImplementationMeanMs * 1.2);
}
```

---

## Rollback Strategy

### Feature Flag Approach

```csharp
public class GameAssistantService
{
    private readonly IFeatureFlags _flags;
    private readonly LegacyRagService _legacy;
    private readonly IPipelineOrchestrator _orchestrator;

    public async Task<string> AskAsync(string question)
    {
        if (_flags.IsEnabled("use-plugin-pipeline"))
        {
            var result = await _orchestrator.ExecuteAsync("game-assistant", input);
            return result.FinalOutput.Result.GetProperty("response").GetString();
        }
        else
        {
            return await _legacy.ProcessQueryAsync(question);
        }
    }
}
```

### Gradual Rollout

```csharp
public async Task<string> AskAsync(string question, Guid userId)
{
    // Gradual rollout by user percentage
    var useNewPipeline = _rollout.IsUserInGroup(userId, "plugin-pipeline", percentage: 10);

    if (useNewPipeline)
    {
        try
        {
            return await ExecutePluginPipeline(question);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin pipeline failed, falling back");
            _metrics.Increment("pipeline.fallback");
            return await _legacy.ProcessQueryAsync(question);
        }
    }

    return await _legacy.ProcessQueryAsync(question);
}
```

---

## Checklist

### Pre-Migration

- [ ] Document existing pipeline behavior
- [ ] Identify all configuration values
- [ ] List custom logic components
- [ ] Set up testing infrastructure
- [ ] Define success metrics

### Migration

- [ ] Create plugin definitions for custom logic
- [ ] Build pipeline JSON definition
- [ ] Register plugins with DI
- [ ] Update service layer
- [ ] Add logging and monitoring

### Post-Migration

- [ ] Run comparison tests
- [ ] Validate performance
- [ ] Deploy with feature flag
- [ ] Monitor error rates
- [ ] Gradual rollout
- [ ] Remove legacy code (after stable period)

---

## Common Issues

### Issue: Different Output Format

**Problem**: New pipeline returns different JSON structure.

**Solution**: Add adapter layer or update consumers:
```csharp
// Adapter to maintain backward compatibility
public class PipelineResponseAdapter
{
    public LegacyResponse ToLegacyFormat(PipelineResult result)
    {
        return new LegacyResponse
        {
            Answer = result.FinalOutput.Result.GetProperty("response").GetString(),
            Sources = ExtractSourcesFromCitations(result)
        };
    }
}
```

### Issue: Performance Regression

**Problem**: Plugin overhead causes slower responses.

**Solution**:
1. Profile individual plugins
2. Add caching where appropriate
3. Optimize plugin configuration
4. Consider plugin-level parallelization

### Issue: Lost Custom Behavior

**Problem**: Custom logic not captured by standard plugins.

**Solution**: Create custom plugin preserving original behavior:
```csharp
// Preserve exact original logic in custom plugin
public class LegacyBehaviorPlugin : RagPluginBase
{
    protected override Task<PluginOutput> ExecuteCoreAsync(...)
    {
        // Copy original implementation exactly
    }
}
```

---

## Related Documentation

- [Plugin Development Guide](plugin-development-guide.md) - Creating custom plugins
- [Pipeline Definition Schema](pipeline-definition.md) - JSON pipeline format
- [Testing Guide](testing-guide.md) - Testing strategies
- [Visual Builder Guide](visual-builder-guide.md) - UI for pipeline creation
