# RAG Plugin System

> **Modular, extensible plugin architecture for RAG pipelines**

The MeepleAI RAG Plugin System enables flexible construction of AI pipelines through composable, reusable components. Plugins can be combined using the Visual Pipeline Builder to create custom workflows for different use cases.

## Quick Start

Create your first plugin in under 10 minutes:

```csharp
public class MyPlugin : RagPluginBase
{
    public override string Id => "my-plugin-v1";
    public override string Name => "My Plugin";
    public override string Version => "1.0.0";
    public override PluginCategory Category => PluginCategory.Transform;

    public MyPlugin(ILogger logger) : base(logger) { }

    protected override async Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        // Your plugin logic here
        var result = JsonDocument.Parse("""{"processed": true}""");
        return PluginOutput.Successful(input.ExecutionId, result);
    }

    protected override JsonDocument CreateInputSchema() =>
        CreateBasicSchema("object", "Input description");

    protected override JsonDocument CreateOutputSchema() =>
        CreateBasicSchema("object", "Output description");

    protected override JsonDocument CreateConfigSchema() =>
        CreateBasicSchema("object", "Config description");
}
```

## Documentation Structure

| Document | Description |
|----------|-------------|
| [Plugin Development Guide](plugin-development-guide.md) | Complete guide to creating plugins |
| [Plugin Contract Reference](plugin-contract.md) | IRagPlugin interface documentation |
| [Pipeline Definition Schema](pipeline-definition.md) | JSON schema for pipeline definitions |
| [Visual Builder Guide](visual-builder-guide.md) | Using the drag-drop pipeline builder |
| [Testing Guide](testing-guide.md) | Testing plugins with the test framework |
| [Built-in Plugins](built-in-plugins/) | Reference for included plugins |

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     Visual Pipeline Builder                      │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────────────┐  │
│  │   Canvas    │  │ Plugin       │  │ Node Config            │  │
│  │ (React Flow)│  │ Palette      │  │ Panel                  │  │
│  └─────────────┘  └──────────────┘  └────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Pipeline Definition (JSON)                    │
│  {nodes: [...], edges: [...], metadata: {...}}                  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      DAG Orchestrator                            │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────────────┐  │
│  │ Validator   │  │ Condition    │  │ Execution              │  │
│  │ (DAG check) │  │ Evaluator    │  │ Engine                 │  │
│  └─────────────┘  └──────────────┘  └────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        Plugin Registry                           │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐   │
│  │Routing  │ │Cache    │ │Retrieval│ │Evaluation│ │Generation│   │
│  │Plugins  │ │Plugins  │ │Plugins  │ │Plugins  │ │Plugins  │   │
│  └─────────┘ └─────────┘ └─────────┘ └─────────┘ └─────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## Plugin Categories

| Category | Purpose | Examples |
|----------|---------|----------|
| **Routing** | Determine query path through pipeline | Intent detection, complexity routing |
| **Cache** | Cache queries and results | Semantic cache, exact match cache |
| **Retrieval** | Fetch relevant documents | Vector search, hybrid search |
| **Evaluation** | Assess quality | Relevance scoring, confidence |
| **Generation** | Generate responses | Answer generation, summaries |
| **Validation** | Verify correctness | Input sanitization, guardrails |
| **Transform** | Modify data | Query rewriting, reranking |
| **Filter** | Select/remove data | Deduplication, thresholding |

## Key Concepts

### Plugin Contract

Every plugin implements `IRagPlugin`:

```csharp
public interface IRagPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    PluginCategory Category { get; }

    JsonDocument InputSchema { get; }
    JsonDocument OutputSchema { get; }
    JsonDocument ConfigSchema { get; }
    PluginMetadata Metadata { get; }

    Task<PluginOutput> ExecuteAsync(PluginInput input, PluginConfig? config, CancellationToken ct);
    Task<HealthCheckResult> HealthCheckAsync(CancellationToken ct);
    ValidationResult ValidateConfig(PluginConfig config);
    ValidationResult ValidateInput(PluginInput input);
}
```

### Pipeline Definition

Pipelines are defined as directed acyclic graphs (DAGs):

```json
{
  "id": "my-pipeline",
  "name": "Custom RAG Pipeline",
  "nodes": [
    {"id": "n1", "pluginId": "routing-intent-v1", "config": {}},
    {"id": "n2", "pluginId": "retrieval-vector-v1", "config": {}}
  ],
  "edges": [
    {"source": "n1", "target": "n2", "condition": "queryType == 'rules'"}
  ]
}
```

### Execution Flow

1. **Validation**: DAG structure and plugin configs validated
2. **Topological Sort**: Determine execution order
3. **Condition Evaluation**: Check edge conditions at runtime
4. **Plugin Execution**: Run plugins with timeout and error handling
5. **Result Aggregation**: Collect outputs and metrics

## Getting Started

1. **[Read the Development Guide](plugin-development-guide.md)** - Learn plugin architecture
2. **[Explore Built-in Plugins](built-in-plugins/)** - See real examples
3. **[Use the Visual Builder](visual-builder-guide.md)** - Create pipelines visually
4. **[Write Tests](testing-guide.md)** - Ensure quality with the test framework

## Related Documentation

- [RAG Architecture Overview](../00-overview.md)
- [Layer 1: Routing](../02-layer1-routing.md)
- [Layer 2: Caching](../03-layer2-caching.md)
- [Layer 3: Retrieval](../04-layer3-retrieval.md)
- [Layer 4: Evaluation](../05-layer4-crag-evaluation.md)
- [Layer 5: Generation](../06-layer5-generation.md)
- [Layer 6: Validation](../07-layer6-validation.md)
