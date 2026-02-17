# Plugin Contract Reference

> **Complete IRagPlugin Interface Specification**

## Interface Overview

```csharp
public interface IRagPlugin
{
    // Identity
    string Id { get; }              // Unique ID (lowercase-hyphen)
    string Name { get; }            // Display name
    string Version { get; }         // SemVer (1.0.0)
    PluginCategory Category { get; }  // Role in pipeline

    // Schemas
    JsonDocument InputSchema { get; }   // Expected input structure
    JsonDocument OutputSchema { get; }  // Output structure
    JsonDocument ConfigSchema { get; }  // Config options
    PluginMetadata Metadata { get; }    // Aggregated info

    // Core Methods
    Task<PluginOutput> ExecuteAsync(PluginInput, PluginConfig?, CancellationToken);
    Task<HealthCheckResult> HealthCheckAsync(CancellationToken);
    ValidationResult ValidateConfig(PluginConfig);
    ValidationResult ValidateInput(PluginInput);
}
```

---

## Identity Properties

### Id
| Requirement | Specification |
|-------------|---------------|
| **Format** | Lowercase alphanumeric + hyphens |
| **Pattern** | `^[a-z0-9-]+$` |
| **Length** | 3-100 chars |
| **Uniqueness** | Globally unique |
| **Stability** | Never change between versions |

**Naming**: `<category>-<function>-v<major>`
**Examples**: `routing-intent-v1`, `retrieval-vector-v1`, `generation-answer-v1`

### Version
| Requirement | Specification |
|-------------|---------------|
| **Format** | SemVer |
| **Pattern** | `^\d+\.\d+\.\d+(-[a-zA-Z0-9]+)?$` |
| **Examples** | `1.0.0`, `2.1.0-beta`, `1.0.0-rc1` |

**Semantics**: Major (breaking) | Minor (features) | Patch (fixes)

### Category
| Category | Purpose | Position |
|----------|---------|----------|
| `Routing` | Path determination | Entry |
| `Cache` | Result caching | Early |
| `Retrieval` | Document fetching | Mid |
| `Evaluation` | Quality assessment | After retrieval |
| `Generation` | Response creation | Late |
| `Validation` | Correctness check | Final |
| `Transform` | Data modification | Anywhere |
| `Filter` | Data selection | After retrieval |

---

## Schemas

### InputSchema Template
```json
{
  "$schema": "https://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "query": {
      "type": "string",
      "minLength": 1,
      "maxLength": 10000
    },
    "options": {
      "type": "object",
      "properties": {
        "maxResults": {"type": "integer", "minimum": 1, "default": 10}
      }
    }
  },
  "required": ["query"]
}
```

### OutputSchema by Category

**Routing**:
```json
{"route": "string", "queryType": "string", "confidence": 0.0-1.0}
```

**Retrieval**:
```json
{"documents": [{"id": "str", "content": "str", "score": 0.0}], "totalFound": 0}
```

**Generation**:
```json
{"response": "string", "citations": ["str"], "confidence": 0.0-1.0}
```

### ConfigSchema Standard Fields
```json
{
  "timeoutMs": {"type": "integer", "default": 30000, "max": 300000},
  "maxRetries": {"type": "integer", "default": 3, "max": 10},
  "enableCaching": {"type": "boolean", "default": true},
  "logLevel": {"enum": ["none", "error", "warning", "info", "debug"]}
}
```

---

## Core Methods

### ExecuteAsync

**Behavioral Requirements**:
1. Validate input before processing
2. Respect `config.TimeoutMs`
3. Honor `cancellationToken`
4. Never throw (return failed output)
5. Populate `DurationMs` in metrics
6. Idempotent where possible

**Execution Flow**:
```
1. Apply default config if null
2. Validate config
3. Validate input
4. Start timing
5. Execute core logic (with timeout)
6. Stop timing
7. Populate metrics
8. Return PluginOutput
```

**Return Values**:
| Scenario | Success | ErrorCode | Result |
|----------|---------|-----------|--------|
| Valid execution | `true` | `null` | Populated |
| Validation fail | `false` | `VALIDATION_FAILED` | `null` |
| Timeout | `false` | `TIMEOUT` | `null` |
| Cancelled | `false` | `CANCELLED` | `null` |
| Internal error | `false` | `INTERNAL_ERROR` | `null` |

### HealthCheckAsync

**Status Levels**:
| Status | Condition | Example |
|--------|-----------|---------|
| `Healthy` | All dependencies available | Vector store connected |
| `Degraded` | Operational with limits | Cache down, using fallback |
| `Unhealthy` | Cannot process | LLM API unreachable |

**Check**: External connectivity, config availability, resource availability, quotas

### ValidateConfig / ValidateInput

**Standard Validations**:
- `TimeoutMs > 0` and `≤ 300000`
- `MaxRetries ≥ 0`
- `ExecutionId != Guid.Empty`
- `Payload != null`

**Return**: `ValidationResult` with `IsValid`, `Errors`, `Warnings`

---

## Data Models

### PluginInput
```csharp
{
    Guid ExecutionId                               // Tracing ID
    JsonDocument Payload                           // Input data
    Guid? GameId                                   // Game context
    Guid? UserId                                   // User context
    IReadOnlyDictionary<string, PluginOutput>? PreviousOutputs  // Pipeline data
    IReadOnlyDictionary<string, object>? PipelineContext       // Shared context
}
```

### PluginOutput
```csharp
{
    bool Success                    // Execution status
    Guid ExecutionId                // Matches input
    JsonDocument? Result            // Result data
    string? ErrorMessage            // If failed
    string? ErrorCode               // Programmatic handling
    double? Confidence              // 0.0-1.0
    PluginMetrics Metrics           // Execution stats
}
```

### PluginConfig
```csharp
{
    int TimeoutMs = 30000           // Execution timeout
    int MaxRetries = 3              // Retry attempts
    bool EnableCaching = true       // Cache results
    JsonDocument? Parameters        // Plugin-specific params
}
```

---

## Contract Verification

```csharp
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Testing;

[Fact]
public void Plugin_MeetsContractRequirements()
{
    var plugin = new MyPlugin(logger);
    var result = PluginContractTests.VerifyContract(plugin);
    result.AssertAllPassed();
}
```

---

**See Also**: [Plugin Development](plugin-development-guide.md) | [Testing Guide](testing-guide.md) | [Built-in Plugins](built-in-plugins/)
