# Evaluation Plugins

> **Quality Assessment and Scoring**

Evaluation plugins assess the quality of retrieved documents and generated responses. They enable the "corrective" part of Corrective RAG (CRAG) by identifying when retrieval quality is insufficient.

## Relevance Scorer

**Plugin ID**: `evaluation-relevance-v1`

Scores how relevant each retrieved document is to the original query.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `model` | string | `cross-encoder/ms-marco-MiniLM-L-6-v2` | Scoring model |
| `threshold` | number | `0.5` | Minimum relevance score |
| `batchSize` | integer | `10` | Documents per batch |
| `returnScores` | boolean | `true` | Include individual scores |
| `computeAggregates` | boolean | `true` | Compute mean, min, max |

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "query": { "type": "string" },
    "documents": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "id": { "type": "string" },
          "content": { "type": "string" }
        }
      }
    }
  },
  "required": ["query", "documents"]
}
```

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "scoredDocuments": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "id": { "type": "string" },
          "relevanceScore": { "type": "number" },
          "isRelevant": { "type": "boolean" }
        }
      }
    },
    "aggregates": {
      "type": "object",
      "properties": {
        "averageRelevance": { "type": "number" },
        "minRelevance": { "type": "number" },
        "maxRelevance": { "type": "number" },
        "relevantCount": { "type": "integer" },
        "totalCount": { "type": "integer" }
      }
    },
    "overallQuality": {
      "type": "string",
      "enum": ["high", "medium", "low"]
    }
  }
}
```

### Usage Example

```json
{
  "id": "relevance-scorer",
  "pluginId": "evaluation-relevance-v1",
  "config": {
    "threshold": 0.6,
    "batchSize": 20
  }
}
```

### CRAG Routing Pattern

```json
{
  "id": "e-good-retrieval",
  "source": "relevance-scorer",
  "target": "generation",
  "condition": "output.result.aggregates.averageRelevance >= 0.7",
  "label": "High Quality"
},
{
  "id": "e-poor-retrieval",
  "source": "relevance-scorer",
  "target": "query-rewriter",
  "condition": "output.result.aggregates.averageRelevance < 0.7",
  "label": "Needs Correction"
}
```

---

## Confidence Evaluator

**Plugin ID**: `evaluation-confidence-v1`

Provides an overall confidence score for the retrieval step based on multiple factors.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `weights` | object | See below | Factor weights |
| `minDocuments` | integer | `3` | Minimum docs for high confidence |
| `scoreSpread` | number | `0.2` | Max acceptable score variance |

**Default Weights**:
```json
{
  "weights": {
    "topScore": 0.3,
    "averageScore": 0.25,
    "documentCount": 0.2,
    "scoreConsistency": 0.15,
    "queryMatch": 0.1
  }
}
```

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "confidence": { "type": "number" },
    "factors": {
      "type": "object",
      "properties": {
        "topScore": { "type": "number" },
        "averageScore": { "type": "number" },
        "documentCount": { "type": "number" },
        "scoreConsistency": { "type": "number" },
        "queryMatch": { "type": "number" }
      }
    },
    "recommendation": {
      "type": "string",
      "enum": ["proceed", "augment", "requery", "fallback"]
    }
  }
}
```

### Usage Example

```json
{
  "id": "confidence-eval",
  "pluginId": "evaluation-confidence-v1",
  "config": {
    "minDocuments": 5,
    "weights": {
      "topScore": 0.4,
      "averageScore": 0.3,
      "documentCount": 0.15,
      "scoreConsistency": 0.15
    }
  }
}
```

### Routing by Confidence

```
[Confidence Eval] ─┬─→ "proceed" ──→ [Generation]
                   ├─→ "augment" ──→ [Web Search] → [Merge] → [Generation]
                   ├─→ "requery" ──→ [Query Rewrite] → [Retrieval]
                   └─→ "fallback" ─→ [Fallback Response]
```

---

## CRAG Implementation

Corrective RAG uses evaluation to improve retrieval quality:

### Full CRAG Pipeline

```
[Cache] → [Router] → [Retrieval] → [Relevance Scorer]
                                          │
                          ┌───────────────┴───────────────┐
                          │                               │
                    High Quality                    Low Quality
                          │                               │
                          ▼                               ▼
                    [Generation]                  [Query Rewriter]
                          │                               │
                          ▼                               ▼
                    [Validation]              [Retrieval] (retry)
                                                         │
                                                         ▼
                                              [Relevance Scorer]
                                                         │
                                              (max 2 retries)
```

### Quality Thresholds

| Quality Level | Average Relevance | Action |
|---------------|-------------------|--------|
| High | ≥ 0.8 | Proceed to generation |
| Medium | 0.6 - 0.8 | Proceed with caution |
| Low | < 0.6 | Rewrite query and retry |
| Very Low | < 0.4 | Fallback response |

## Best Practices

### Threshold Tuning

1. **Start conservative**: Higher thresholds (0.7+)
2. **Monitor user feedback**: Adjust based on response quality
3. **Per-category thresholds**: Rules may need higher than strategy

### Performance

- Cross-encoder scoring is expensive (~50ms per document)
- Batch documents for efficiency
- Consider limiting documents before evaluation

### Testing

```csharp
[Fact]
public async Task RelevanceScorer_IdentifiesRelevantDocuments()
{
    // Arrange
    var input = PluginMocks.CreateInputWithPayload("""
        {
            "query": "How do I setup Catan?",
            "documents": [
                {"id": "1", "content": "To setup Catan, first place the hex tiles..."},
                {"id": "2", "content": "The best pizza in New York is..."}
            ]
        }
        """);

    // Act
    var output = await Plugin.ExecuteAsync(input);

    // Assert
    var docs = output.Result!.RootElement.GetProperty("scoredDocuments");
    docs[0].GetProperty("isRelevant").GetBoolean().Should().BeTrue();
    docs[1].GetProperty("isRelevant").GetBoolean().Should().BeFalse();
}

[Fact]
public async Task RelevanceScorer_ComputesCorrectAggregates()
{
    var output = await Plugin.ExecuteAsync(input);

    var aggregates = output.Result!.RootElement.GetProperty("aggregates");
    aggregates.GetProperty("averageRelevance").GetDouble().Should().BeInRange(0, 1);
    aggregates.GetProperty("relevantCount").GetInt32().Should().BeLessThanOrEqualTo(
        aggregates.GetProperty("totalCount").GetInt32()
    );
}
```
