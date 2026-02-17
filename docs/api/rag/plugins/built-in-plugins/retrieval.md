# Retrieval Plugins

> **Document Fetching from Vector and Keyword Stores**

Retrieval plugins fetch relevant documents from knowledge stores based on query semantics, keywords, or hybrid approaches.

## Vector Search

**Plugin ID**: `retrieval-vector-v1`

Performs semantic similarity search using vector embeddings.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `topK` | integer | `10` | Maximum documents to return |
| `similarityThreshold` | number | `0.7` | Minimum similarity score |
| `namespace` | string | `null` | Filter by namespace (e.g., "rules", "strategy") |
| `includeMetadata` | boolean | `true` | Include document metadata |
| `embeddingModel` | string | `text-embedding-3-small` | Model for query embedding |

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "query": { "type": "string" },
    "gameId": { "type": "string", "format": "uuid" },
    "filters": {
      "type": "object",
      "additionalProperties": true
    }
  },
  "required": ["query"]
}
```

### Output Schema

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
    "totalFound": { "type": "integer" },
    "queryVector": { "type": "array", "items": { "type": "number" } }
  }
}
```

### Usage Example

```json
{
  "id": "vector-search",
  "pluginId": "retrieval-vector-v1",
  "config": {
    "topK": 5,
    "similarityThreshold": 0.75,
    "namespace": "rules",
    "includeMetadata": true
  }
}
```

---

## Hybrid Search

**Plugin ID**: `retrieval-hybrid-v1`

Combines vector similarity with keyword matching for better recall.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `topK` | integer | `10` | Maximum documents to return |
| `alpha` | number | `0.7` | Weight for vector vs keyword (0=keyword, 1=vector) |
| `vectorWeight` | number | `0.7` | Alternative to alpha |
| `keywordWeight` | number | `0.3` | Alternative to alpha |
| `namespace` | string | `null` | Filter by namespace |
| `fusionMethod` | string | `"rrf"` | Score fusion: `"rrf"` or `"weighted"` |

### Output Schema

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
          "vectorScore": { "type": "number" },
          "keywordScore": { "type": "number" },
          "fusedScore": { "type": "number" },
          "metadata": { "type": "object" }
        }
      }
    },
    "totalFound": { "type": "integer" }
  }
}
```

### Usage Example

```json
{
  "id": "hybrid-search",
  "pluginId": "retrieval-hybrid-v1",
  "config": {
    "topK": 10,
    "alpha": 0.6,
    "fusionMethod": "rrf"
  }
}
```

### When to Use Hybrid

- **Specific terms matter**: Game names, rule keywords
- **Both meaning and exact match**: "How many victory points to win?"
- **Diverse document types**: Rules (precise) + strategy (semantic)

---

## Keyword Search

**Plugin ID**: `retrieval-keyword-v1`

Traditional BM25 keyword-based search for precise term matching.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `topK` | integer | `10` | Maximum documents to return |
| `b` | number | `0.75` | BM25 document length normalization |
| `k1` | number | `1.2` | BM25 term frequency saturation |
| `analyzer` | string | `"standard"` | Text analyzer |
| `fields` | string[] | `["content", "title"]` | Fields to search |

### Output Schema

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
          "highlights": {
            "type": "array",
            "items": { "type": "string" }
          }
        }
      }
    },
    "totalFound": { "type": "integer" }
  }
}
```

### Usage Example

```json
{
  "id": "keyword-search",
  "pluginId": "retrieval-keyword-v1",
  "config": {
    "topK": 20,
    "fields": ["content", "title", "section_name"]
  }
}
```

---

## Comparison

| Feature | Vector | Hybrid | Keyword |
|---------|--------|--------|---------|
| Semantic understanding | ✅ High | ✅ High | ❌ None |
| Exact term matching | ❌ Poor | ✅ Good | ✅ Excellent |
| Latency | Medium | Higher | Low |
| Best for | Conceptual queries | General use | Specific terms |

## Best Practices

### Document Quality

1. **Chunk appropriately**: 200-500 tokens per chunk
2. **Include metadata**: Section titles, page numbers
3. **Clean text**: Remove formatting artifacts

### Performance Tuning

- **Start with hybrid**: Good balance for most use cases
- **Tune alpha**: Higher for conceptual, lower for factual
- **Use namespaces**: Partition by document type
- **Set thresholds**: Filter low-quality results early

### Testing

```csharp
[Fact]
public async Task VectorSearch_ReturnsRelevantDocuments()
{
    var input = PluginMocks.CreateQueryInput("How do I set up Catan?");
    var output = await Plugin.ExecuteAsync(input);

    output.Success.Should().BeTrue();

    var docs = output.Result!.RootElement.GetProperty("documents");
    docs.GetArrayLength().Should().BeGreaterThan(0);

    // Top document should be highly relevant
    docs[0].GetProperty("score").GetDouble().Should().BeGreaterThan(0.8);
}
```
