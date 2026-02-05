# Plugin Contract Reference

> **Complete IRagPlugin Interface Documentation**

The `IRagPlugin` interface defines the contract that all RAG plugins must implement. This document provides exhaustive documentation of each member, expected behaviors, and implementation requirements.

## Interface Definition

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;

/// <summary>
/// Defines the contract for RAG pipeline plugins.
/// All plugins must implement this interface to participate in pipeline execution.
/// </summary>
public interface IRagPlugin
{
    // ═══════════════════════════════════════════════════════════════════
    // IDENTITY PROPERTIES
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Unique identifier for the plugin.
    /// Must be globally unique across all plugins.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable name for display in UI.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Semantic version of the plugin.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Category determining the plugin's role in pipelines.
    /// </summary>
    PluginCategory Category { get; }

    // ═══════════════════════════════════════════════════════════════════
    // SCHEMA PROPERTIES
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// JSON Schema defining expected input structure.
    /// </summary>
    JsonDocument InputSchema { get; }

    /// <summary>
    /// JSON Schema defining output structure.
    /// </summary>
    JsonDocument OutputSchema { get; }

    /// <summary>
    /// JSON Schema defining configuration options.
    /// </summary>
    JsonDocument ConfigSchema { get; }

    /// <summary>
    /// Aggregated metadata for discovery and UI rendering.
    /// </summary>
    PluginMetadata Metadata { get; }

    // ═══════════════════════════════════════════════════════════════════
    // CORE METHODS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Executes the plugin's primary logic.
    /// </summary>
    Task<PluginOutput> ExecuteAsync(
        PluginInput input,
        PluginConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs health check to verify plugin readiness.
    /// </summary>
    Task<HealthCheckResult> HealthCheckAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates configuration before execution.
    /// </summary>
    ValidationResult ValidateConfig(PluginConfig config);

    /// <summary>
    /// Validates input before execution.
    /// </summary>
    ValidationResult ValidateInput(PluginInput input);
}
```

---

## Identity Properties

### Id

**Type**: `string`

**Purpose**: Globally unique identifier for plugin registration and pipeline references.

**Requirements**:
| Requirement | Specification |
|-------------|---------------|
| Format | Lowercase alphanumeric with hyphens only |
| Pattern | `^[a-z0-9-]+$` |
| Length | 3-100 characters |
| Uniqueness | Must be unique across all registered plugins |
| Stability | Should not change between versions |

**Naming Convention**:
```
<category>-<function>-v<major>

Examples:
- routing-intent-v1
- cache-semantic-v1
- retrieval-vector-v1
- evaluation-relevance-v1
- generation-answer-v1
```

**Implementation**:
```csharp
public override string Id => "retrieval-vector-v1";
```

---

### Name

**Type**: `string`

**Purpose**: Human-readable display name for UI and logs.

**Requirements**:
| Requirement | Specification |
|-------------|---------------|
| Length | 1-200 characters |
| Content | Descriptive, clear purpose indication |
| Localization | English by default, i18n keys supported |

**Examples**:
```csharp
public override string Name => "Vector Similarity Retrieval";
public override string Name => "Intent-Based Query Router";
public override string Name => "Semantic Cache Lookup";
```

---

### Version

**Type**: `string`

**Purpose**: Semantic version for compatibility tracking and migration.

**Requirements**:
| Requirement | Specification |
|-------------|---------------|
| Format | Semantic Versioning (SemVer) |
| Pattern | `^\d+\.\d+\.\d+(-[a-zA-Z0-9]+)?$` |
| Examples | `1.0.0`, `2.1.0-beta`, `1.0.0-rc1` |

**Version Semantics**:
- **Major**: Breaking changes to input/output schemas
- **Minor**: New features, backward-compatible
- **Patch**: Bug fixes, no API changes

**Implementation**:
```csharp
public override string Version => "1.0.0";
```

---

### Category

**Type**: `PluginCategory` (enum)

**Purpose**: Determines plugin's role and valid positions in pipelines.

**Available Categories**:

| Category | Value | Purpose | Typical Position |
|----------|-------|---------|------------------|
| `Routing` | 0 | Query path determination | Pipeline entry |
| `Cache` | 1 | Result caching | Early pipeline |
| `Retrieval` | 2 | Document fetching | Mid pipeline |
| `Evaluation` | 3 | Quality assessment | After retrieval |
| `Generation` | 4 | Response creation | Late pipeline |
| `Validation` | 5 | Correctness verification | Late pipeline |
| `Transform` | 6 | Data modification | Anywhere |
| `Filter` | 7 | Data selection/removal | After retrieval |

**Category Rules**:
```yaml
routing:
  max_per_pipeline: 1
  typical_inputs: [query]
  typical_outputs: [route, queryType, metadata]

cache:
  position: early
  bypass_conditions: [cache_miss, force_refresh]

retrieval:
  requires: [vector_store_connection]
  outputs: [documents, scores]

evaluation:
  inputs: [query, documents]
  outputs: [relevance_scores, confidence]

generation:
  requires: [llm_connection]
  outputs: [response, citations]

validation:
  position: final
  outputs: [is_valid, issues]
```

---

## Schema Properties

### InputSchema

**Type**: `JsonDocument`

**Purpose**: JSON Schema defining the expected structure of `PluginInput.Payload`.

**Requirements**:
- Must be valid JSON Schema (Draft 7 recommended)
- Should include `$schema` reference
- Must define `type` property
- Should include `description` for documentation
- Should define `required` properties

**Standard Schema Template**:
```json
{
  "$schema": "https://json-schema.org/draft-07/schema#",
  "type": "object",
  "title": "Plugin Input Schema",
  "description": "Expected input structure for the plugin",
  "properties": {
    "query": {
      "type": "string",
      "description": "The user's query text",
      "minLength": 1,
      "maxLength": 10000
    },
    "options": {
      "type": "object",
      "description": "Optional processing options",
      "properties": {
        "maxResults": {
          "type": "integer",
          "minimum": 1,
          "maximum": 100,
          "default": 10
        }
      }
    }
  },
  "required": ["query"]
}
```

**Implementation**:
```csharp
protected override JsonDocument CreateInputSchema()
{
    return JsonDocument.Parse("""
        {
            "$schema": "https://json-schema.org/draft-07/schema#",
            "type": "object",
            "properties": {
                "query": { "type": "string", "minLength": 1 },
                "topK": { "type": "integer", "minimum": 1, "default": 10 }
            },
            "required": ["query"]
        }
        """);
}
```

---

### OutputSchema

**Type**: `JsonDocument`

**Purpose**: JSON Schema defining the structure of `PluginOutput.Result`.

**Standard Output Schemas by Category**:

**Routing Output**:
```json
{
  "type": "object",
  "properties": {
    "route": { "type": "string" },
    "queryType": { "type": "string" },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 }
  },
  "required": ["route"]
}
```

**Retrieval Output**:
```json
{
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
          "metadata": { "type": "object" }
        }
      }
    },
    "totalFound": { "type": "integer" }
  },
  "required": ["documents"]
}
```

**Generation Output**:
```json
{
  "type": "object",
  "properties": {
    "response": { "type": "string" },
    "citations": {
      "type": "array",
      "items": { "type": "string" }
    },
    "confidence": { "type": "number" }
  },
  "required": ["response"]
}
```

---

### ConfigSchema

**Type**: `JsonDocument`

**Purpose**: JSON Schema defining plugin-specific configuration options.

**Standard Fields**:
All plugins inherit these standard configuration fields:
```json
{
  "type": "object",
  "properties": {
    "timeoutMs": {
      "type": "integer",
      "minimum": 1,
      "maximum": 300000,
      "default": 30000,
      "description": "Execution timeout in milliseconds"
    },
    "maxRetries": {
      "type": "integer",
      "minimum": 0,
      "maximum": 10,
      "default": 3,
      "description": "Maximum retry attempts on transient failures"
    },
    "enableCaching": {
      "type": "boolean",
      "default": true,
      "description": "Whether to cache results"
    },
    "logLevel": {
      "type": "string",
      "enum": ["none", "error", "warning", "info", "debug"],
      "default": "info"
    }
  }
}
```

**Plugin-Specific Configuration Example**:
```json
{
  "type": "object",
  "properties": {
    "timeoutMs": { "type": "integer", "default": 30000 },
    "vectorStore": {
      "type": "string",
      "enum": ["qdrant", "pinecone", "weaviate"],
      "default": "qdrant"
    },
    "similarityThreshold": {
      "type": "number",
      "minimum": 0,
      "maximum": 1,
      "default": 0.7
    },
    "embeddingModel": {
      "type": "string",
      "default": "text-embedding-3-small"
    }
  }
}
```

---

### Metadata

**Type**: `PluginMetadata`

**Purpose**: Aggregated information for discovery, UI rendering, and documentation.

**PluginMetadata Structure**:
```csharp
public sealed record PluginMetadata
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required PluginCategory Category { get; init; }
    public string? Description { get; init; }
    public string? Author { get; init; }
    public string[]? Tags { get; init; }
    public string? DocumentationUrl { get; init; }
    public string? IconUrl { get; init; }
    public Dictionary<string, object>? CustomMetadata { get; init; }
}
```

**Requirements**:
- `Id`, `Name`, `Version`, `Category` MUST match the plugin's direct properties
- `Description` should be clear and concise (1-2 sentences)
- `Tags` enable search and filtering in the Visual Builder

**Implementation** (handled by RagPluginBase):
```csharp
public virtual PluginMetadata Metadata => new()
{
    Id = Id,
    Name = Name,
    Version = Version,
    Category = Category,
    Description = GetDescription(),
    Tags = GetTags()
};

protected virtual string? GetDescription() => null;
protected virtual string[]? GetTags() => null;
```

---

## Core Methods

### ExecuteAsync

**Signature**:
```csharp
Task<PluginOutput> ExecuteAsync(
    PluginInput input,
    PluginConfig? config = null,
    CancellationToken cancellationToken = default);
```

**Purpose**: Performs the plugin's primary processing logic.

**Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `input` | `PluginInput` | Input data including payload and context |
| `config` | `PluginConfig?` | Optional configuration (defaults applied if null) |
| `cancellationToken` | `CancellationToken` | Cancellation signal |

**Return**: `PluginOutput` containing success status, result data, and metrics.

**Behavioral Requirements**:

1. **Validation First**: Must validate input before processing
2. **Timeout Handling**: Must respect `config.TimeoutMs`
3. **Cancellation**: Must honor `cancellationToken`
4. **Error Handling**: Must not throw exceptions (return failed output instead)
5. **Metrics**: Must populate `DurationMs` in output
6. **Idempotency**: Should be idempotent where possible

**Execution Flow**:
```
┌─────────────────────────────────────────────────────────┐
│ ExecuteAsync                                            │
├─────────────────────────────────────────────────────────┤
│ 1. Apply default config if null                         │
│ 2. Validate configuration                               │
│ 3. Validate input                                       │
│ 4. Start timing                                         │
│ 5. Execute core logic (with timeout)                    │
│ 6. Stop timing                                          │
│ 7. Populate metrics                                     │
│ 8. Return PluginOutput                                  │
└─────────────────────────────────────────────────────────┘
```

**Return Values**:

| Scenario | Success | ErrorCode | Result |
|----------|---------|-----------|--------|
| Valid execution | `true` | `null` | Populated |
| Validation failure | `false` | `VALIDATION_FAILED` | `null` |
| Timeout | `false` | `TIMEOUT` | `null` |
| Cancellation | `false` | `CANCELLED` | `null` |
| Internal error | `false` | `INTERNAL_ERROR` | `null` |

**Example Implementation**:
```csharp
public override async Task<PluginOutput> ExecuteAsync(
    PluginInput input,
    PluginConfig? config = null,
    CancellationToken cancellationToken = default)
{
    config ??= PluginConfig.Default();
    var stopwatch = Stopwatch.StartNew();

    try
    {
        // Validate
        var configValidation = ValidateConfig(config);
        if (!configValidation.IsValid)
            return PluginOutput.Failed(input.ExecutionId,
                configValidation.Errors.First(), "CONFIG_INVALID");

        var inputValidation = ValidateInput(input);
        if (!inputValidation.IsValid)
            return PluginOutput.Failed(input.ExecutionId,
                inputValidation.Errors.First(), "INPUT_INVALID");

        // Execute with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(config.TimeoutMs);

        var result = await ExecuteCoreAsync(input, config, cts.Token);

        stopwatch.Stop();
        return result with { Metrics = result.Metrics with { DurationMs = stopwatch.ElapsedMilliseconds } };
    }
    catch (OperationCanceledException)
    {
        return PluginOutput.Failed(input.ExecutionId, "Operation cancelled", "CANCELLED");
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Plugin execution failed");
        return PluginOutput.Failed(input.ExecutionId, ex.Message, "INTERNAL_ERROR");
    }
}
```

---

### HealthCheckAsync

**Signature**:
```csharp
Task<HealthCheckResult> HealthCheckAsync(
    CancellationToken cancellationToken = default);
```

**Purpose**: Verifies the plugin is ready to process requests.

**Return**: `HealthCheckResult` with status and diagnostic information.

**HealthCheckResult Structure**:
```csharp
public sealed record HealthCheckResult
{
    public required HealthStatus Status { get; init; }
    public string? Message { get; init; }
    public long CheckDurationMs { get; init; }
    public Dictionary<string, object>? Details { get; init; }
}

public enum HealthStatus
{
    Healthy = 0,    // Fully operational
    Degraded = 1,   // Operational with issues
    Unhealthy = 2   // Not operational
}
```

**Health Check Requirements**:

| Status | Condition | Example |
|--------|-----------|---------|
| `Healthy` | All dependencies available | Vector store connected |
| `Degraded` | Operational with limitations | Cache unavailable, using fallback |
| `Unhealthy` | Cannot process requests | LLM API unreachable |

**What to Check**:
- External service connectivity (databases, APIs)
- Required configuration availability
- Resource availability (memory, connections)
- License/quota status

**Example Implementation**:
```csharp
public override async Task<HealthCheckResult> HealthCheckAsync(
    CancellationToken cancellationToken = default)
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
        // Check vector store connection
        var isConnected = await _vectorStore.PingAsync(cancellationToken);

        stopwatch.Stop();

        return new HealthCheckResult
        {
            Status = isConnected ? HealthStatus.Healthy : HealthStatus.Unhealthy,
            Message = isConnected ? "Vector store connected" : "Vector store unreachable",
            CheckDurationMs = stopwatch.ElapsedMilliseconds,
            Details = new Dictionary<string, object>
            {
                ["vectorStoreConnected"] = isConnected,
                ["vectorStoreType"] = _vectorStore.GetType().Name
            }
        };
    }
    catch (Exception ex)
    {
        return new HealthCheckResult
        {
            Status = HealthStatus.Unhealthy,
            Message = $"Health check failed: {ex.Message}",
            CheckDurationMs = stopwatch.ElapsedMilliseconds
        };
    }
}
```

---

### ValidateConfig

**Signature**:
```csharp
ValidationResult ValidateConfig(PluginConfig config);
```

**Purpose**: Validates configuration before execution.

**Return**: `ValidationResult` with validation status and error details.

**ValidationResult Structure**:
```csharp
public sealed record ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(params string[] errors) =>
        new() { IsValid = false, Errors = errors };
}
```

**Standard Validations**:
```csharp
public virtual ValidationResult ValidateConfig(PluginConfig config)
{
    var errors = new List<string>();

    // Standard validations
    if (config.TimeoutMs <= 0)
        errors.Add("TimeoutMs must be positive");

    if (config.MaxRetries < 0)
        errors.Add("MaxRetries cannot be negative");

    if (config.TimeoutMs > 300000) // 5 minutes max
        errors.Add("TimeoutMs cannot exceed 300000 (5 minutes)");

    // Plugin-specific validations
    errors.AddRange(ValidatePluginConfig(config));

    return errors.Count == 0
        ? ValidationResult.Success()
        : ValidationResult.Failure(errors.ToArray());
}
```

---

### ValidateInput

**Signature**:
```csharp
ValidationResult ValidateInput(PluginInput input);
```

**Purpose**: Validates input before processing.

**Standard Validations**:
```csharp
public virtual ValidationResult ValidateInput(PluginInput input)
{
    var errors = new List<string>();

    // Standard validations
    if (input.ExecutionId == Guid.Empty)
        errors.Add("ExecutionId cannot be empty");

    if (input.Payload == null)
        errors.Add("Payload cannot be null");

    // Plugin-specific validations
    errors.AddRange(ValidatePluginInput(input));

    return errors.Count == 0
        ? ValidationResult.Success()
        : ValidationResult.Failure(errors.ToArray());
}
```

---

## Data Models

### PluginInput

```csharp
public sealed record PluginInput
{
    /// <summary>
    /// Unique identifier for this execution (for tracing).
    /// </summary>
    public required Guid ExecutionId { get; init; }

    /// <summary>
    /// The actual input data as JSON.
    /// </summary>
    public required JsonDocument Payload { get; init; }

    /// <summary>
    /// Optional game context for game-specific queries.
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// Optional user context for personalization.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Outputs from previous pipeline nodes.
    /// Key is the node ID, value is the output.
    /// </summary>
    public IReadOnlyDictionary<string, PluginOutput>? PreviousOutputs { get; init; }

    /// <summary>
    /// Pipeline-level context passed through all nodes.
    /// </summary>
    public IReadOnlyDictionary<string, object>? PipelineContext { get; init; }
}
```

### PluginOutput

```csharp
public sealed record PluginOutput
{
    /// <summary>
    /// Whether execution completed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The execution ID (matches input).
    /// </summary>
    public required Guid ExecutionId { get; init; }

    /// <summary>
    /// The result data if successful.
    /// </summary>
    public JsonDocument? Result { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0) for result quality.
    /// </summary>
    public double? Confidence { get; init; }

    /// <summary>
    /// Execution metrics.
    /// </summary>
    public PluginMetrics Metrics { get; init; } = new();

    // Factory methods
    public static PluginOutput Successful(Guid executionId, JsonDocument result, double? confidence = null);
    public static PluginOutput Failed(Guid executionId, string errorMessage, string? errorCode = null);
}
```

### PluginConfig

```csharp
public sealed record PluginConfig
{
    /// <summary>
    /// Execution timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Maximum retry attempts.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Whether to enable caching.
    /// </summary>
    public bool EnableCaching { get; init; } = true;

    /// <summary>
    /// Plugin-specific parameters.
    /// </summary>
    public JsonDocument? Parameters { get; init; }

    public static PluginConfig Default() => new();
}
```

---

## Contract Verification

Use the testing framework to verify contract compliance:

```csharp
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Testing;

[Fact]
public void Plugin_MeetsContractRequirements()
{
    // Arrange
    var plugin = new MyPlugin(logger);

    // Act
    var result = PluginContractTests.VerifyContract(plugin);

    // Assert
    result.AssertAllPassed();
}
```

---

## Related Documentation

- [Plugin Development Guide](plugin-development-guide.md) - How to create plugins
- [Testing Guide](testing-guide.md) - Testing your plugins
- [Pipeline Definition Schema](pipeline-definition.md) - How plugins connect in pipelines
- [Built-in Plugins](built-in-plugins/) - Reference implementations
