# Routing Plugins

> **Query Classification and Path Selection**

Routing plugins analyze incoming queries and determine the optimal path through the pipeline. They enable branching workflows based on query characteristics.

## Intent Router

**Plugin ID**: `routing-intent-v1`

Classifies queries by intent type to route to specialized retrievers.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `model` | string | `gpt-3.5-turbo` | LLM for classification |
| `intents` | string[] | `["rules", "strategy", "general"]` | Available intent categories |
| `confidenceThreshold` | number | `0.7` | Minimum confidence to classify |
| `defaultIntent` | string | `"general"` | Fallback when uncertain |

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "query": { "type": "string" }
  },
  "required": ["query"]
}
```

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "queryType": { "type": "string" },
    "confidence": { "type": "number" },
    "reasoning": { "type": "string" }
  }
}
```

### Usage Example

```json
{
  "id": "router",
  "pluginId": "routing-intent-v1",
  "config": {
    "intents": ["rules", "strategy", "setup", "general"],
    "confidenceThreshold": 0.8
  }
}
```

### Conditional Edges

```json
{
  "id": "e-rules",
  "source": "router",
  "target": "rules-retrieval",
  "condition": "output.result.queryType === 'rules'"
}
```

---

## Complexity Router

**Plugin ID**: `routing-complexity-v1`

Routes queries based on complexity for appropriate processing depth.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `simpleThreshold` | number | `0.3` | Below = simple query |
| `complexThreshold` | number | `0.7` | Above = complex query |
| `features` | string[] | `["length", "entities", "clauses"]` | Complexity factors |

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "complexity": { "type": "string", "enum": ["simple", "moderate", "complex"] },
    "score": { "type": "number" },
    "factors": {
      "type": "object",
      "properties": {
        "length": { "type": "number" },
        "entityCount": { "type": "number" },
        "clauseCount": { "type": "number" }
      }
    }
  }
}
```

### Usage Example

```json
{
  "id": "complexity-router",
  "pluginId": "routing-complexity-v1",
  "config": {
    "simpleThreshold": 0.25,
    "complexThreshold": 0.75
  }
}
```

### Routing Pattern

```
[Complexity Router] ─┬─→ [Quick Retrieval (simple)]
                     ├─→ [Standard Retrieval (moderate)]
                     └─→ [Deep Retrieval (complex)]
```

---

## Best Practices

### Routing Strategy

1. **Keep it simple**: Start with 2-3 routes, expand as needed
2. **Use confidence**: Only route when confident
3. **Always have default**: Handle unclassified queries
4. **Monitor distribution**: Ensure balanced routing

### Performance

- Routing adds latency (typically 50-200ms)
- Consider caching routing decisions
- Use lightweight models for classification

### Testing

```csharp
[Theory]
[InlineData("How do I set up the board?", "rules")]
[InlineData("What's the best opening strategy?", "strategy")]
[InlineData("Tell me about this game", "general")]
public async Task IntentRouter_ClassifiesCorrectly(string query, string expectedType)
{
    var input = PluginMocks.CreateQueryInput(query);
    var output = await Plugin.ExecuteAsync(input);

    output.Result!.RootElement
        .GetProperty("queryType")
        .GetString()
        .Should().Be(expectedType);
}
```
