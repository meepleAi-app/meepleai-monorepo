# Plugin Development Guide

> **Complete guide to creating RAG plugins for MeepleAI**

This guide walks you through creating custom plugins for the RAG pipeline system, from basic implementation to advanced patterns.

## Prerequisites

- .NET 9.0 SDK
- Understanding of C# async/await patterns
- Familiarity with JSON Schema
- (Optional) Knowledge of the MeepleAI RAG architecture

## Table of Contents

1. [Plugin Basics](#plugin-basics)
2. [Implementing IRagPlugin](#implementing-iragplugin)
3. [Using RagPluginBase](#using-ragpluginbase)
4. [Input/Output Design](#inputoutput-design)
5. [Configuration](#configuration)
6. [Health Checks](#health-checks)
7. [Error Handling](#error-handling)
8. [Testing](#testing)
9. [Advanced Patterns](#advanced-patterns)

---

## Plugin Basics

### What is a Plugin?

A plugin is a self-contained component that performs a specific function in the RAG pipeline. Plugins:

- Have a unique identifier and version
- Define their input/output schemas
- Execute asynchronously with cancellation support
- Support health monitoring
- Can be combined into pipelines

### Plugin Categories

Choose the category that best matches your plugin's function:

| Category | When to Use |
|----------|-------------|
| `Routing` | Deciding which path a query takes |
| `Cache` | Storing/retrieving cached data |
| `Retrieval` | Fetching documents from knowledge sources |
| `Evaluation` | Scoring or assessing quality |
| `Generation` | Creating text with LLMs |
| `Validation` | Verifying correctness |
| `Transform` | Modifying data format |
| `Filter` | Selecting or removing items |

---

## Implementing IRagPlugin

### Direct Implementation

For maximum control, implement `IRagPlugin` directly:

```csharp
public class CustomPlugin : IRagPlugin
{
    private readonly ILogger<CustomPlugin> _logger;
    private readonly Lazy<JsonDocument> _inputSchema;
    private readonly Lazy<JsonDocument> _outputSchema;
    private readonly Lazy<JsonDocument> _configSchema;

    public string Id => "custom-plugin-v1";
    public string Name => "Custom Plugin";
    public string Version => "1.0.0";
    public PluginCategory Category => PluginCategory.Transform;

    public JsonDocument InputSchema => _inputSchema.Value;
    public JsonDocument OutputSchema => _outputSchema.Value;
    public JsonDocument ConfigSchema => _configSchema.Value;

    public PluginMetadata Metadata => new()
    {
        Id = Id,
        Name = Name,
        Version = Version,
        Category = Category,
        Description = "Transforms input data",
        Author = "MeepleAI",
        IsEnabled = true,
        IsBuiltIn = false
    };

    public CustomPlugin(ILogger<CustomPlugin> logger)
    {
        _logger = logger;
        _inputSchema = new Lazy<JsonDocument>(CreateInputSchema);
        _outputSchema = new Lazy<JsonDocument>(CreateOutputSchema);
        _configSchema = new Lazy<JsonDocument>(CreateConfigSchema);
    }

    public async Task<PluginOutput> ExecuteAsync(
        PluginInput input,
        PluginConfig? config,
        CancellationToken cancellationToken)
    {
        // Validate input
        var validation = ValidateInput(input);
        if (!validation.IsValid)
        {
            return PluginOutput.Failed(input.ExecutionId, "Invalid input", "VALIDATION_ERROR");
        }

        // Execute logic
        try
        {
            var result = await ProcessAsync(input, cancellationToken);
            return PluginOutput.Successful(input.ExecutionId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin execution failed");
            return PluginOutput.Failed(input.ExecutionId, ex.Message, "EXECUTION_ERROR");
        }
    }

    public Task<HealthCheckResult> HealthCheckAsync(CancellationToken ct)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }

    public ValidationResult ValidateConfig(PluginConfig config)
    {
        return ValidationResult.Success();
    }

    public ValidationResult ValidateInput(PluginInput input)
    {
        if (input.ExecutionId == Guid.Empty)
            return ValidationResult.Failure("ExecutionId is required");
        return ValidationResult.Success();
    }

    private Task<JsonDocument> ProcessAsync(PluginInput input, CancellationToken ct)
    {
        // Your transformation logic
        return Task.FromResult(JsonDocument.Parse("""{"transformed": true}"""));
    }

    private JsonDocument CreateInputSchema() =>
        JsonDocument.Parse("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "data": { "type": "string" }
            },
            "required": ["data"]
        }
        """);

    private JsonDocument CreateOutputSchema() =>
        JsonDocument.Parse("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "transformed": { "type": "boolean" }
            }
        }
        """);

    private JsonDocument CreateConfigSchema() =>
        JsonDocument.Parse("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "setting": { "type": "string", "default": "value" }
            }
        }
        """);
}
```

---

## Using RagPluginBase

### Recommended Approach

`RagPluginBase` provides common functionality out of the box:

```csharp
public class RetrievalPlugin : RagPluginBase
{
    private readonly IVectorStore _vectorStore;

    public override string Id => "retrieval-vector-v1";
    public override string Name => "Vector Retrieval";
    public override string Version => "1.0.0";
    public override PluginCategory Category => PluginCategory.Retrieval;

    // Optional metadata
    protected override string Description => "Retrieves documents using vector similarity";
    protected override string Author => "MeepleAI";
    protected override IReadOnlyList<string> Tags => ["vector", "semantic", "qdrant"];
    protected override IReadOnlyList<string> Capabilities => ["semantic-search", "top-k"];

    public RetrievalPlugin(ILogger<RetrievalPlugin> logger, IVectorStore vectorStore)
        : base(logger)
    {
        _vectorStore = vectorStore;
    }

    protected override async Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        // Get query from input
        var query = input.Payload.RootElement.GetProperty("query").GetString();
        var topK = config.CustomConfig?.RootElement.TryGetProperty("topK", out var k)
            == true ? k.GetInt32() : 5;

        // Perform retrieval
        var results = await _vectorStore.SearchAsync(query!, topK, cancellationToken);

        // Build output
        var output = new
        {
            documents = results.Select(r => new
            {
                r.Id,
                r.Content,
                r.Score
            }),
            totalCount = results.Count
        };

        return PluginOutput.Successful(
            input.ExecutionId,
            JsonDocument.Parse(JsonSerializer.Serialize(output)),
            confidence: results.Average(r => r.Score));
    }

    protected override JsonDocument CreateInputSchema() =>
        JsonDocument.Parse("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The search query"
                }
            },
            "required": ["query"]
        }
        """);

    protected override JsonDocument CreateOutputSchema() =>
        JsonDocument.Parse("""
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
                            "score": { "type": "number" }
                        }
                    }
                },
                "totalCount": { "type": "integer" }
            }
        }
        """);

    protected override JsonDocument CreateConfigSchema() =>
        JsonDocument.Parse("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "topK": {
                    "type": "integer",
                    "default": 5,
                    "minimum": 1,
                    "maximum": 100,
                    "description": "Number of results to return"
                }
            }
        }
        """);

    // Optional: Custom health check
    protected override async Task<HealthCheckResult> PerformHealthCheckAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _vectorStore.PingAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Vector store unavailable: {ex.Message}");
        }
    }

    // Optional: Custom config validation
    protected override ValidationResult ValidateConfigCore(PluginConfig config)
    {
        if (config.CustomConfig?.RootElement.TryGetProperty("topK", out var topK) == true)
        {
            var value = topK.GetInt32();
            if (value < 1 || value > 100)
            {
                return ValidationResult.Failure(new ValidationError
                {
                    Message = "topK must be between 1 and 100",
                    PropertyPath = "topK",
                    Code = "INVALID_TOP_K"
                });
            }
        }
        return ValidationResult.Success();
    }
}
```

### What RagPluginBase Provides

| Feature | Description |
|---------|-------------|
| **Input validation** | Automatic ExecutionId and Payload checks |
| **Config validation** | Timeout, retry, and backoff validation |
| **Timeout handling** | Automatic execution timeout with cancellation |
| **Error handling** | Catches exceptions and returns proper error outputs |
| **Metrics** | Automatic execution duration tracking |
| **Health checks** | Default healthy status with override support |
| **Schema caching** | Lazy initialization of JSON schemas |

---

## Input/Output Design

### Input Structure

```csharp
public sealed record PluginInput
{
    public required Guid ExecutionId { get; init; }      // Unique execution ID
    public required JsonDocument Payload { get; init; }  // Primary data
    public IReadOnlyDictionary<string, JsonDocument> PipelineContext { get; init; }  // Previous outputs
    public IReadOnlyDictionary<string, object> Metadata { get; init; }  // Additional metadata
    public Guid? UserId { get; init; }   // Optional user context
    public Guid? GameId { get; init; }   // Optional game context
}
```

### Output Structure

```csharp
public sealed record PluginOutput
{
    public required Guid ExecutionId { get; init; }   // Must match input
    public required bool Success { get; init; }       // Success/failure
    public JsonDocument? Result { get; init; }        // Result data
    public string? ErrorMessage { get; init; }        // Error info
    public string? ErrorCode { get; init; }           // Error code
    public double? Confidence { get; init; }          // Quality score (0-1)
    public PluginExecutionMetrics Metrics { get; init; }  // Performance data
}
```

### Accessing Pipeline Context

```csharp
protected override Task<PluginOutput> ExecuteCoreAsync(
    PluginInput input, PluginConfig config, CancellationToken ct)
{
    // Access output from a previous "routing" node
    if (input.PipelineContext.TryGetValue("routing", out var routingOutput))
    {
        var queryType = routingOutput.RootElement
            .GetProperty("queryType")
            .GetString();
    }

    // ... rest of implementation
}
```

---

## Configuration

### Standard Configuration

All plugins receive `PluginConfig` with:

```csharp
public record PluginConfig
{
    public bool Enabled { get; init; } = true;
    public int TimeoutMs { get; init; } = 30000;
    public int MaxRetries { get; init; } = 3;
    public int RetryBackoffMs { get; init; } = 1000;
    public bool ExponentialBackoff { get; init; } = true;
    public JsonDocument? CustomConfig { get; init; }  // Plugin-specific
    public int Priority { get; init; } = 100;
    public IReadOnlyList<string> Tags { get; init; } = [];
}
```

### Custom Configuration

Define plugin-specific settings in `CustomConfig`:

```csharp
// In ConfigSchema:
{
    "properties": {
        "model": { "type": "string", "default": "gpt-4" },
        "temperature": { "type": "number", "minimum": 0, "maximum": 2 },
        "maxTokens": { "type": "integer", "minimum": 1 }
    }
}

// Reading in ExecuteCoreAsync:
var model = config.CustomConfig?.RootElement
    .GetProperty("model").GetString() ?? "gpt-4";

var temperature = config.CustomConfig?.RootElement
    .TryGetProperty("temperature", out var temp) == true
    ? temp.GetDouble() : 0.7;
```

---

## Health Checks

### Default Health Check

`RagPluginBase` returns `Healthy` by default.

### Custom Health Check

```csharp
protected override async Task<HealthCheckResult> PerformHealthCheckAsync(
    CancellationToken cancellationToken)
{
    var issues = new List<string>();

    // Check dependencies
    if (!await _cache.IsAvailableAsync(cancellationToken))
        issues.Add("Cache unavailable");

    if (!await _vectorStore.IsAvailableAsync(cancellationToken))
        issues.Add("Vector store unavailable");

    // Return appropriate status
    if (issues.Count == 0)
        return HealthCheckResult.Healthy();

    if (issues.Count == 1)
        return HealthCheckResult.Degraded(issues[0]);

    return HealthCheckResult.Unhealthy(string.Join("; ", issues));
}
```

### Health Status Levels

| Status | Description |
|--------|-------------|
| `Healthy` | All dependencies available |
| `Degraded` | Partial functionality |
| `Unhealthy` | Plugin cannot function |

---

## Error Handling

### Error Codes

Use consistent error codes:

| Code | Description |
|------|-------------|
| `VALIDATION_ERROR` | Input/config validation failed |
| `CONFIG_ERROR` | Configuration invalid |
| `EXECUTION_ERROR` | Runtime error during execution |
| `TIMEOUT` | Execution timed out |
| `DEPENDENCY_ERROR` | External service unavailable |
| `NOT_FOUND` | Required resource not found |
| `RATE_LIMITED` | Too many requests |

### Returning Errors

```csharp
// Validation error
return PluginOutput.Failed(
    input.ExecutionId,
    "Query is required",
    "VALIDATION_ERROR");

// Execution error with details
return PluginOutput.Failed(
    input.ExecutionId,
    $"Vector store query failed: {ex.Message}",
    "DEPENDENCY_ERROR");
```

---

## Testing

### Using the Test Framework

```csharp
public class MyPluginTests : PluginTestHarness<MyPlugin>
{
    protected override MyPlugin CreatePlugin()
    {
        return new MyPlugin(NullLogger.Instance);
    }

    // All contract tests run automatically!

    [Fact]
    public async Task ExecuteAsync_WithCustomInput_ProducesExpectedOutput()
    {
        // Arrange
        var plugin = CreatePlugin();
        var input = PluginMocks.CreateInputWithPayload("""{"query": "test"}""");

        // Act
        var result = await plugin.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement
            .GetProperty("transformed").GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    public async Task PerformanceMeetsRequirements()
    {
        // Assert mean < 100ms and p95 < 200ms
        await AssertExecutionPerformanceAsync(
            maxMeanMs: 100,
            maxP95Ms: 200);
    }
}
```

See the [Testing Guide](testing-guide.md) for comprehensive testing documentation.

---

## Advanced Patterns

### Dependency Injection

```csharp
// Registration
services.AddScoped<IVectorStore, QdrantVectorStore>();
services.AddScoped<RetrievalPlugin>();

// Plugin with injected dependencies
public class RetrievalPlugin : RagPluginBase
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddings;

    public RetrievalPlugin(
        ILogger<RetrievalPlugin> logger,
        IVectorStore vectorStore,
        IEmbeddingService embeddings) : base(logger)
    {
        _vectorStore = vectorStore;
        _embeddings = embeddings;
    }
}
```

### Caching Results

```csharp
protected override async Task<PluginOutput> ExecuteCoreAsync(
    PluginInput input, PluginConfig config, CancellationToken ct)
{
    // Generate cache key
    var query = input.Payload.RootElement.GetProperty("query").GetString()!;
    var cacheKey = $"retrieval:{ComputeHash(query)}";

    // Check cache
    var cached = await _cache.GetAsync<JsonDocument>(cacheKey, ct);
    if (cached != null)
    {
        return PluginOutput.Successful(input.ExecutionId, cached) with
        {
            Metrics = new PluginExecutionMetrics { CacheHit = true }
        };
    }

    // Execute and cache
    var result = await DoRetrievalAsync(query, ct);
    await _cache.SetAsync(cacheKey, result, TimeSpan.FromHours(1), ct);

    return PluginOutput.Successful(input.ExecutionId, result);
}
```

### Streaming Results

For plugins that support streaming (e.g., LLM generation):

```csharp
// Note: Full streaming support requires SSE integration
// This pattern shows how to track incremental progress

protected override async Task<PluginOutput> ExecuteCoreAsync(
    PluginInput input, PluginConfig config, CancellationToken ct)
{
    var prompt = input.Payload.RootElement.GetProperty("prompt").GetString()!;
    var fullResponse = new StringBuilder();
    var tokenCount = 0;

    await foreach (var chunk in _llm.StreamAsync(prompt, ct))
    {
        fullResponse.Append(chunk);
        tokenCount++;

        // Report progress if supported
        Logger.LogDebug("Generated {Tokens} tokens", tokenCount);
    }

    return PluginOutput.Successful(
        input.ExecutionId,
        JsonDocument.Parse(JsonSerializer.Serialize(new { text = fullResponse.ToString() })))
    with
    {
        Metrics = new PluginExecutionMetrics
        {
            OutputTokens = tokenCount
        }
    };
}
```

### Retry Logic

The base class handles retries through configuration. For custom retry behavior:

```csharp
protected override async Task<PluginOutput> ExecuteCoreAsync(
    PluginInput input, PluginConfig config, CancellationToken ct)
{
    var retryPolicy = Policy
        .Handle<HttpRequestException>()
        .Or<TimeoutException>()
        .WaitAndRetryAsync(
            config.MaxRetries,
            attempt => TimeSpan.FromMilliseconds(
                config.ExponentialBackoff
                    ? config.RetryBackoffMs * Math.Pow(2, attempt - 1)
                    : config.RetryBackoffMs));

    return await retryPolicy.ExecuteAsync(async () =>
    {
        // Your retriable operation
        return await DoWorkAsync(input, ct);
    });
}
```

---

## Best Practices

### DO

- ✅ Use semantic versioning for plugin versions
- ✅ Define comprehensive JSON schemas
- ✅ Handle cancellation tokens properly
- ✅ Log at appropriate levels (Debug, Information, Warning, Error)
- ✅ Return meaningful error codes
- ✅ Include confidence scores when applicable
- ✅ Write comprehensive tests

### DON'T

- ❌ Block on async operations
- ❌ Throw exceptions for expected errors (return Failed output)
- ❌ Ignore cancellation tokens
- ❌ Hard-code timeouts (use config)
- ❌ Return null from ExecuteAsync
- ❌ Mutate input data

---

## Next Steps

- [Plugin Contract Reference](plugin-contract.md) - Complete interface documentation
- [Testing Guide](testing-guide.md) - Test your plugins thoroughly
- [Built-in Plugins](built-in-plugins/) - See real-world examples
- [Visual Builder Guide](visual-builder-guide.md) - Use plugins in pipelines
