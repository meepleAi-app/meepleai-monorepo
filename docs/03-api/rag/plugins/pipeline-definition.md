# Pipeline Definition Schema

> **JSON Schema for RAG Pipeline Definitions**

Pipeline definitions describe how plugins connect and interact to form complete RAG workflows. This document specifies the JSON schema used by the Visual Pipeline Builder and DAG Orchestrator.

## Overview

A pipeline definition is a directed acyclic graph (DAG) consisting of:
- **Nodes**: Plugin instances with configuration
- **Edges**: Connections between nodes with optional conditions
- **Metadata**: Pipeline-level information

```
┌─────────────────────────────────────────────────────────┐
│                   Pipeline Definition                    │
├─────────────────────────────────────────────────────────┤
│  Metadata: id, name, version, description               │
│                                                          │
│  Nodes[]:                                                │
│    ┌───────────┐    ┌───────────┐    ┌───────────┐     │
│    │  Routing  │───▶│ Retrieval │───▶│Generation │     │
│    │   Node    │    │   Node    │    │   Node    │     │
│    └───────────┘    └───────────┘    └───────────┘     │
│                           │                              │
│                           ▼                              │
│                     ┌───────────┐                        │
│                     │   Cache   │                        │
│                     │   Node    │                        │
│                     └───────────┘                        │
│                                                          │
│  Edges[]: source → target (with conditions)              │
└─────────────────────────────────────────────────────────┘
```

## Complete Schema

```json
{
  "$schema": "https://json-schema.org/draft-07/schema#",
  "title": "RAG Pipeline Definition",
  "description": "Schema for defining RAG pipelines as directed acyclic graphs",
  "type": "object",
  "required": ["id", "name", "version", "nodes", "edges"],
  "properties": {
    "id": {
      "type": "string",
      "pattern": "^[a-z0-9-]+$",
      "minLength": 3,
      "maxLength": 100,
      "description": "Unique pipeline identifier"
    },
    "name": {
      "type": "string",
      "minLength": 1,
      "maxLength": 200,
      "description": "Human-readable pipeline name"
    },
    "version": {
      "type": "string",
      "pattern": "^\\d+\\.\\d+\\.\\d+(-[a-zA-Z0-9]+)?$",
      "description": "Semantic version"
    },
    "description": {
      "type": "string",
      "maxLength": 2000,
      "description": "Pipeline purpose and behavior"
    },
    "nodes": {
      "type": "array",
      "minItems": 1,
      "items": { "$ref": "#/definitions/PipelineNode" },
      "description": "Plugin instances in the pipeline"
    },
    "edges": {
      "type": "array",
      "items": { "$ref": "#/definitions/PipelineEdge" },
      "description": "Connections between nodes"
    },
    "metadata": {
      "$ref": "#/definitions/PipelineMetadata"
    },
    "variables": {
      "type": "object",
      "additionalProperties": true,
      "description": "Pipeline-level variables for dynamic configuration"
    }
  },
  "definitions": {
    "PipelineNode": {
      "type": "object",
      "required": ["id", "pluginId"],
      "properties": {
        "id": {
          "type": "string",
          "pattern": "^[a-zA-Z0-9_-]+$",
          "description": "Unique node identifier within pipeline"
        },
        "pluginId": {
          "type": "string",
          "description": "Reference to registered plugin"
        },
        "name": {
          "type": "string",
          "description": "Display name (defaults to plugin name)"
        },
        "config": {
          "type": "object",
          "description": "Plugin-specific configuration"
        },
        "position": {
          "$ref": "#/definitions/Position",
          "description": "Visual position in builder"
        },
        "enabled": {
          "type": "boolean",
          "default": true,
          "description": "Whether node is active"
        },
        "retryPolicy": {
          "$ref": "#/definitions/RetryPolicy"
        },
        "timeout": {
          "type": "integer",
          "minimum": 1000,
          "maximum": 300000,
          "description": "Node-specific timeout override (ms)"
        }
      }
    },
    "PipelineEdge": {
      "type": "object",
      "required": ["id", "source", "target"],
      "properties": {
        "id": {
          "type": "string",
          "description": "Unique edge identifier"
        },
        "source": {
          "type": "string",
          "description": "Source node ID"
        },
        "target": {
          "type": "string",
          "description": "Target node ID"
        },
        "sourceHandle": {
          "type": "string",
          "description": "Output port on source node"
        },
        "targetHandle": {
          "type": "string",
          "description": "Input port on target node"
        },
        "condition": {
          "type": "string",
          "description": "JavaScript expression for conditional routing"
        },
        "label": {
          "type": "string",
          "description": "Edge label for display"
        },
        "dataTransform": {
          "type": "string",
          "description": "JavaScript expression to transform data"
        }
      }
    },
    "Position": {
      "type": "object",
      "required": ["x", "y"],
      "properties": {
        "x": { "type": "number" },
        "y": { "type": "number" }
      }
    },
    "RetryPolicy": {
      "type": "object",
      "properties": {
        "maxRetries": {
          "type": "integer",
          "minimum": 0,
          "maximum": 10,
          "default": 3
        },
        "backoffMs": {
          "type": "integer",
          "minimum": 100,
          "default": 1000
        },
        "backoffMultiplier": {
          "type": "number",
          "minimum": 1,
          "maximum": 5,
          "default": 2
        },
        "retryableErrors": {
          "type": "array",
          "items": { "type": "string" },
          "default": ["TIMEOUT", "TRANSIENT_ERROR"]
        }
      }
    },
    "PipelineMetadata": {
      "type": "object",
      "properties": {
        "author": { "type": "string" },
        "createdAt": { "type": "string", "format": "date-time" },
        "updatedAt": { "type": "string", "format": "date-time" },
        "tags": {
          "type": "array",
          "items": { "type": "string" }
        },
        "category": {
          "type": "string",
          "enum": ["rules", "strategy", "general", "custom"]
        },
        "isTemplate": { "type": "boolean", "default": false },
        "documentation": { "type": "string" }
      }
    }
  }
}
```

---

## Node Definition

### Basic Node

```json
{
  "id": "retrieval-1",
  "pluginId": "retrieval-vector-v1",
  "name": "Vector Search",
  "config": {
    "topK": 10,
    "similarityThreshold": 0.7,
    "includeMetadata": true
  },
  "position": { "x": 300, "y": 200 }
}
```

### Node Properties

| Property | Required | Type | Description |
|----------|----------|------|-------------|
| `id` | Yes | string | Unique within pipeline (used in edges) |
| `pluginId` | Yes | string | Registered plugin identifier |
| `name` | No | string | Display name override |
| `config` | No | object | Plugin configuration |
| `position` | No | Position | Visual builder coordinates |
| `enabled` | No | boolean | Active state (default: true) |
| `retryPolicy` | No | RetryPolicy | Node-specific retry settings |
| `timeout` | No | integer | Timeout override (ms) |

### Node Configuration

Configuration merges with plugin defaults:

```json
{
  "id": "cache-1",
  "pluginId": "cache-semantic-v1",
  "config": {
    "similarityThreshold": 0.95,
    "maxCacheAge": 3600,
    "namespace": "game-rules",
    "parameters": {
      "embeddingModel": "text-embedding-3-small"
    }
  }
}
```

### Special Nodes

**Entry Node** (implicit):
```json
{
  "id": "entry",
  "pluginId": "system-entry-v1",
  "config": {
    "validateInput": true,
    "normalizeQuery": true
  }
}
```

**Exit Node** (implicit):
```json
{
  "id": "exit",
  "pluginId": "system-exit-v1",
  "config": {
    "aggregateResults": true,
    "includeMetrics": true
  }
}
```

---

## Edge Definition

### Basic Edge

```json
{
  "id": "e1",
  "source": "routing-1",
  "target": "retrieval-1"
}
```

### Conditional Edge

```json
{
  "id": "e-rules",
  "source": "routing-1",
  "target": "retrieval-rules",
  "condition": "output.queryType === 'rules'",
  "label": "Rules Query"
}
```

### Edge with Data Transform

```json
{
  "id": "e-transform",
  "source": "retrieval-1",
  "target": "evaluation-1",
  "dataTransform": "{ documents: output.result.documents.slice(0, 5) }"
}
```

### Edge Properties

| Property | Required | Type | Description |
|----------|----------|------|-------------|
| `id` | Yes | string | Unique edge identifier |
| `source` | Yes | string | Source node ID |
| `target` | Yes | string | Target node ID |
| `sourceHandle` | No | string | Specific output port |
| `targetHandle` | No | string | Specific input port |
| `condition` | No | string | JavaScript condition expression |
| `label` | No | string | Display label |
| `dataTransform` | No | string | Data transformation expression |

---

## Condition Expressions

### Available Context

```javascript
// In condition expressions, you have access to:
{
  output: {           // Previous node's output
    success: true,
    result: { ... },
    confidence: 0.85
  },
  input: {            // Pipeline input
    query: "...",
    gameId: "..."
  },
  context: {          // Pipeline context
    variables: { ... }
  }
}
```

### Condition Examples

**By Query Type**:
```javascript
output.result.queryType === 'rules'
```

**By Confidence**:
```javascript
output.confidence >= 0.8
```

**By Document Count**:
```javascript
output.result.documents.length > 0
```

**Complex Conditions**:
```javascript
output.success && output.result.relevanceScore > 0.7 && input.gameId !== null
```

**Negation (Else Branch)**:
```javascript
!(output.result.queryType === 'rules')  // Everything except rules
```

### Condition Best Practices

1. **Keep conditions simple**: Complex logic should be in plugins
2. **Always handle else**: Ensure all paths have coverage
3. **Use confidence scores**: Route based on quality
4. **Test conditions**: Validate with sample data

---

## Data Transforms

### Transform Syntax

```javascript
// Input: previous node's output
// Output: transformed data for next node

// Pass through specific fields
{ query: input.query, documents: output.result.documents }

// Filter documents
{ documents: output.result.documents.filter(d => d.score > 0.7) }

// Limit results
{ documents: output.result.documents.slice(0, 5) }

// Add metadata
{ ...output.result, transformedAt: new Date().toISOString() }
```

### Common Transforms

**Top-K Selection**:
```javascript
{ documents: output.result.documents.slice(0, 3) }
```

**Score Filtering**:
```javascript
{ documents: output.result.documents.filter(d => d.score >= 0.8) }
```

**Field Extraction**:
```javascript
{ content: output.result.documents.map(d => d.content).join('\n') }
```

**Merge with Input**:
```javascript
{ query: input.query, context: output.result, gameId: input.gameId }
```

---

## Complete Pipeline Examples

### Simple Linear Pipeline

```json
{
  "id": "simple-rag-pipeline",
  "name": "Simple RAG Pipeline",
  "version": "1.0.0",
  "description": "Basic retrieve-then-generate pipeline",
  "nodes": [
    {
      "id": "retrieval",
      "pluginId": "retrieval-vector-v1",
      "config": { "topK": 5 }
    },
    {
      "id": "generation",
      "pluginId": "generation-answer-v1",
      "config": { "maxTokens": 500 }
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

### Routing Pipeline

```json
{
  "id": "routed-rag-pipeline",
  "name": "Query-Routed RAG Pipeline",
  "version": "1.0.0",
  "description": "Routes queries to specialized retrievers",
  "nodes": [
    {
      "id": "router",
      "pluginId": "routing-intent-v1",
      "position": { "x": 100, "y": 200 }
    },
    {
      "id": "rules-retrieval",
      "pluginId": "retrieval-vector-v1",
      "name": "Rules Retriever",
      "config": { "namespace": "rules", "topK": 10 },
      "position": { "x": 300, "y": 100 }
    },
    {
      "id": "strategy-retrieval",
      "pluginId": "retrieval-vector-v1",
      "name": "Strategy Retriever",
      "config": { "namespace": "strategy", "topK": 5 },
      "position": { "x": 300, "y": 300 }
    },
    {
      "id": "generation",
      "pluginId": "generation-answer-v1",
      "position": { "x": 500, "y": 200 }
    }
  ],
  "edges": [
    {
      "id": "e-rules",
      "source": "router",
      "target": "rules-retrieval",
      "condition": "output.result.queryType === 'rules'",
      "label": "Rules"
    },
    {
      "id": "e-strategy",
      "source": "router",
      "target": "strategy-retrieval",
      "condition": "output.result.queryType === 'strategy'",
      "label": "Strategy"
    },
    {
      "id": "e-rules-gen",
      "source": "rules-retrieval",
      "target": "generation"
    },
    {
      "id": "e-strategy-gen",
      "source": "strategy-retrieval",
      "target": "generation"
    }
  ]
}
```

### Full CRAG Pipeline

```json
{
  "id": "crag-pipeline",
  "name": "Corrective RAG Pipeline",
  "version": "1.0.0",
  "description": "Full CRAG implementation with evaluation and correction",
  "variables": {
    "relevanceThreshold": 0.7,
    "maxRetries": 2
  },
  "nodes": [
    {
      "id": "cache",
      "pluginId": "cache-semantic-v1",
      "config": { "similarityThreshold": 0.95 }
    },
    {
      "id": "router",
      "pluginId": "routing-intent-v1"
    },
    {
      "id": "retrieval",
      "pluginId": "retrieval-hybrid-v1",
      "config": { "topK": 10, "hybridAlpha": 0.7 }
    },
    {
      "id": "evaluation",
      "pluginId": "evaluation-relevance-v1"
    },
    {
      "id": "rewrite",
      "pluginId": "transform-query-rewrite-v1"
    },
    {
      "id": "generation",
      "pluginId": "generation-answer-v1"
    },
    {
      "id": "validation",
      "pluginId": "validation-hallucination-v1"
    }
  ],
  "edges": [
    { "id": "e1", "source": "cache", "target": "router",
      "condition": "!output.result.cacheHit" },
    { "id": "e-cache-hit", "source": "cache", "target": "exit",
      "condition": "output.result.cacheHit" },
    { "id": "e2", "source": "router", "target": "retrieval" },
    { "id": "e3", "source": "retrieval", "target": "evaluation" },
    { "id": "e4-good", "source": "evaluation", "target": "generation",
      "condition": "output.result.averageRelevance >= 0.7",
      "label": "Relevant" },
    { "id": "e4-poor", "source": "evaluation", "target": "rewrite",
      "condition": "output.result.averageRelevance < 0.7",
      "label": "Needs Correction" },
    { "id": "e5", "source": "rewrite", "target": "retrieval" },
    { "id": "e6", "source": "generation", "target": "validation" }
  ],
  "metadata": {
    "author": "MeepleAI Team",
    "tags": ["crag", "evaluation", "correction"],
    "category": "general"
  }
}
```

---

## Validation Rules

### DAG Validation

The orchestrator validates pipelines on save:

1. **Acyclic**: No cycles allowed (infinite loops)
2. **Connected**: All nodes reachable from entry
3. **Complete**: All paths lead to exit
4. **Referenced**: All edge nodes exist
5. **Plugin Exists**: All pluginIds are registered

### Schema Validation

```typescript
interface ValidationResult {
  valid: boolean;
  errors: ValidationError[];
  warnings: ValidationWarning[];
}

interface ValidationError {
  code: string;
  message: string;
  path: string;  // JSON path to error
  nodeId?: string;
  edgeId?: string;
}
```

### Common Validation Errors

| Code | Description | Resolution |
|------|-------------|------------|
| `CYCLE_DETECTED` | Pipeline contains cycles | Remove circular edges |
| `ORPHAN_NODE` | Node not connected | Add edges to/from node |
| `DEAD_END` | Path doesn't reach exit | Add edge to downstream node |
| `MISSING_NODE` | Edge references unknown node | Fix node ID |
| `UNKNOWN_PLUGIN` | Plugin not registered | Register or fix pluginId |
| `INVALID_CONDITION` | Condition syntax error | Fix JavaScript expression |
| `DUPLICATE_ID` | Node/edge ID not unique | Use unique identifiers |

---

## Pipeline Variables

### Definition

```json
{
  "variables": {
    "topK": 10,
    "relevanceThreshold": 0.7,
    "namespace": "game-rules",
    "debug": false
  }
}
```

### Usage in Node Config

```json
{
  "id": "retrieval",
  "pluginId": "retrieval-vector-v1",
  "config": {
    "topK": "${variables.topK}",
    "namespace": "${variables.namespace}"
  }
}
```

### Usage in Conditions

```javascript
output.result.score >= context.variables.relevanceThreshold
```

### Runtime Override

Variables can be overridden at execution time:

```csharp
var result = await orchestrator.ExecuteAsync(
    pipelineId: "crag-pipeline",
    input: queryInput,
    variableOverrides: new Dictionary<string, object>
    {
        ["topK"] = 20,
        ["debug"] = true
    }
);
```

---

## Versioning

### Pipeline Versions

```json
{
  "id": "my-pipeline",
  "version": "2.0.0",
  "metadata": {
    "previousVersions": ["1.0.0", "1.1.0"],
    "breakingChanges": ["Changed router output schema"]
  }
}
```

### Migration Between Versions

When pipeline structure changes:

1. Create new version with updated schema
2. Document breaking changes
3. Keep previous version for active executions
4. Migrate saved results if needed

---

## Best Practices

### Pipeline Design

1. **Start simple**: Linear pipelines first, add branching as needed
2. **Use meaningful IDs**: `rules-retrieval` not `node-1`
3. **Document conditions**: Use labels on conditional edges
4. **Handle all paths**: Ensure every branch converges
5. **Set appropriate timeouts**: Consider cumulative execution time

### Performance

1. **Minimize nodes**: Each node adds latency
2. **Use caching**: Add cache nodes for repeated queries
3. **Parallel where possible**: Orchestrator runs independent branches in parallel
4. **Configure retries wisely**: Balance reliability vs. latency

### Maintainability

1. **Version your pipelines**: Use semantic versioning
2. **Document changes**: Track modifications in metadata
3. **Use variables**: Centralize configuration values
4. **Test thoroughly**: Use preview mode before deployment

---

## Related Documentation

- [Plugin Contract](plugin-contract.md) - Plugin interface specification
- [Visual Builder Guide](visual-builder-guide.md) - UI for creating pipelines
- [Plugin Development Guide](plugin-development-guide.md) - Creating custom plugins
