# Filter Plugins

> **Document Selection and Removal**

Filter plugins select or remove documents from the pipeline based on various criteria. They help reduce noise and focus on the most relevant content.

## Deduplication Filter

**Plugin ID**: `filter-dedupe-v1`

Removes duplicate or near-duplicate documents from the result set.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `strategy` | string | `"semantic"` | `"exact"`, `"semantic"`, `"fuzzy"` |
| `threshold` | number | `0.95` | Similarity threshold for duplicates |
| `keepStrategy` | string | `"highest-score"` | Which duplicate to keep |
| `compareFields` | string[] | `["content"]` | Fields to compare |

### Deduplication Strategies

| Strategy | Description | Use Case |
|----------|-------------|----------|
| `exact` | Identical content hash | Exact copies |
| `semantic` | Embedding similarity | Same meaning, different words |
| `fuzzy` | Character-level similarity | Typos, minor variations |

### Input Schema

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
          "score": { "type": "number" }
        }
      }
    }
  },
  "required": ["documents"]
}
```

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "documents": {
      "type": "array",
      "items": { "type": "object" }
    },
    "removedCount": { "type": "integer" },
    "duplicateGroups": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "kept": { "type": "string" },
          "removed": { "type": "array", "items": { "type": "string" } }
        }
      }
    }
  }
}
```

### Usage Example

```json
{
  "id": "dedupe",
  "pluginId": "filter-dedupe-v1",
  "config": {
    "strategy": "semantic",
    "threshold": 0.92,
    "keepStrategy": "highest-score"
  }
}
```

### Keep Strategies

| Strategy | Behavior |
|----------|----------|
| `highest-score` | Keep document with best retrieval score |
| `longest` | Keep document with most content |
| `first` | Keep first encountered |
| `most-metadata` | Keep document with most metadata |

---

## Threshold Filter

**Plugin ID**: `filter-threshold-v1`

Filters documents based on score thresholds.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `minScore` | number | `0.5` | Minimum score to keep |
| `maxScore` | number | `1.0` | Maximum score (for diversity) |
| `scoreField` | string | `"score"` | Field containing score |
| `softThreshold` | boolean | `false` | Gradual filtering near threshold |

### Input Schema

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
          "score": { "type": "number" }
        }
      }
    }
  },
  "required": ["documents"]
}
```

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "documents": {
      "type": "array",
      "items": { "type": "object" }
    },
    "filteredCount": { "type": "integer" },
    "thresholdUsed": { "type": "number" }
  }
}
```

### Usage Example

```json
{
  "id": "score-filter",
  "pluginId": "filter-threshold-v1",
  "config": {
    "minScore": 0.7
  }
}
```

### Adaptive Thresholding

```json
{
  "config": {
    "adaptive": true,
    "minDocuments": 3,
    "fallbackThreshold": 0.5
  }
}
```

When `adaptive: true`:
- If all docs below threshold, lower threshold to keep `minDocuments`
- Ensures at least some results are returned

---

## Diversity Filter

**Plugin ID**: `filter-diversity-v1`

Ensures result diversity by limiting similar documents.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxSimilarity` | number | `0.8` | Max similarity between kept docs |
| `diversityField` | string | `null` | Field for categorical diversity |
| `maxPerCategory` | integer | `3` | Max docs per category |

### Usage Example

```json
{
  "id": "diversity",
  "pluginId": "filter-diversity-v1",
  "config": {
    "maxSimilarity": 0.75,
    "diversityField": "source_section",
    "maxPerCategory": 2
  }
}
```

### Diversity Strategies

**Semantic Diversity**:
```json
{
  "config": {
    "maxSimilarity": 0.7
  }
}
// Removes docs too similar to already-selected ones
```

**Categorical Diversity**:
```json
{
  "config": {
    "diversityField": "document_type",
    "maxPerCategory": 2
  }
}
// Ensures variety of document types (rules, examples, FAQ)
```

---

## Metadata Filter

**Plugin ID**: `filter-metadata-v1`

Filters documents based on metadata field values.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `filters` | object[] | `[]` | Filter conditions |
| `mode` | string | `"all"` | `"all"` (AND) or `"any"` (OR) |

### Filter Conditions

```json
{
  "config": {
    "filters": [
      { "field": "source", "operator": "equals", "value": "rulebook" },
      { "field": "page", "operator": "lessThan", "value": 50 },
      { "field": "section", "operator": "contains", "value": "setup" }
    ],
    "mode": "all"
  }
}
```

### Available Operators

| Operator | Description |
|----------|-------------|
| `equals` | Exact match |
| `notEquals` | Not equal |
| `contains` | String contains |
| `startsWith` | String starts with |
| `endsWith` | String ends with |
| `greaterThan` | Numeric comparison |
| `lessThan` | Numeric comparison |
| `in` | Value in list |
| `notIn` | Value not in list |
| `exists` | Field exists |
| `notExists` | Field doesn't exist |

---

## Filter Pipeline Patterns

### Quality-First Pipeline

```
[Retrieval] → [Threshold Filter] → [Dedupe] → [Diversity] → [Generation]
                 (min: 0.6)         (0.92)      (0.75)
```

### Source-Based Pipeline

```
[Retrieval] → [Metadata Filter] → [Dedupe] → [Generation]
               (source=rulebook)
```

### Cascading Filters

```
[Retrieval (top 50)]
    ↓
[Threshold Filter (>0.5)] → 30 docs
    ↓
[Deduplication] → 20 docs
    ↓
[Diversity Filter] → 10 docs
    ↓
[Reranker (top 5)] → 5 docs
    ↓
[Generation]
```

---

## Best Practices

### Filter Ordering

1. **Threshold first**: Remove obviously bad docs
2. **Dedupe next**: Remove redundancy
3. **Diversity last**: Ensure variety in remaining

### Threshold Selection

| Content Type | Recommended Threshold |
|--------------|----------------------|
| Exact rules | 0.75-0.85 |
| Strategy tips | 0.60-0.70 |
| FAQ | 0.70-0.80 |
| Examples | 0.65-0.75 |

### Testing

```csharp
[Fact]
public async Task DedupeFilter_RemovesDuplicates()
{
    var input = PluginMocks.CreateInputWithPayload("""
        {
            "documents": [
                {"id": "1", "content": "Setup the board with tiles.", "score": 0.9},
                {"id": "2", "content": "Set up the board with tiles.", "score": 0.8}
            ]
        }
        """);

    var output = await Plugin.ExecuteAsync(input);

    var docs = output.Result!.RootElement.GetProperty("documents");
    docs.GetArrayLength().Should().Be(1);
    output.Result.RootElement.GetProperty("removedCount").GetInt32().Should().Be(1);
}

[Fact]
public async Task ThresholdFilter_RemovesLowScoreDocs()
{
    var input = PluginMocks.CreateInputWithPayload("""
        {
            "documents": [
                {"id": "1", "content": "Relevant", "score": 0.9},
                {"id": "2", "content": "Marginal", "score": 0.6},
                {"id": "3", "content": "Irrelevant", "score": 0.3}
            ]
        }
        """);

    var config = new PluginConfig
    {
        Parameters = JsonDocument.Parse("""{"minScore": 0.5}""")
    };

    var output = await Plugin.ExecuteAsync(input, config);

    var docs = output.Result!.RootElement.GetProperty("documents");
    docs.GetArrayLength().Should().Be(2);
}

[Fact]
public async Task DiversityFilter_EnsuresVariety()
{
    var input = PluginMocks.CreateInputWithPayload("""
        {
            "documents": [
                {"id": "1", "content": "Setup step 1", "section": "setup"},
                {"id": "2", "content": "Setup step 2", "section": "setup"},
                {"id": "3", "content": "Setup step 3", "section": "setup"},
                {"id": "4", "content": "Victory condition", "section": "victory"}
            ]
        }
        """);

    var config = new PluginConfig
    {
        Parameters = JsonDocument.Parse("""{"diversityField": "section", "maxPerCategory": 2}""")
    };

    var output = await Plugin.ExecuteAsync(input, config);

    // Should have max 2 from "setup", plus 1 from "victory"
    var docs = output.Result!.RootElement.GetProperty("documents");
    docs.GetArrayLength().Should().Be(3);
}
```
