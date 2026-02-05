# Transform Plugins

> **Data Modification and Enrichment**

Transform plugins modify data as it flows through the pipeline. They can rewrite queries, rerank documents, or enrich data with additional context.

## Query Rewriter

**Plugin ID**: `transform-rewrite-v1`

Improves queries for better retrieval performance using LLM-based reformulation.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `model` | string | `gpt-3.5-turbo` | LLM for rewriting |
| `strategy` | string | `"expand"` | `"expand"`, `"clarify"`, `"decompose"` |
| `maxVariations` | integer | `3` | Number of query variations |
| `preserveIntent` | boolean | `true` | Maintain original meaning |
| `addSynonyms` | boolean | `true` | Include synonym terms |

### Rewrite Strategies

| Strategy | Description | Example |
|----------|-------------|---------|
| `expand` | Add related terms | "setup Catan" → "setup Catan board tiles initial placement" |
| `clarify` | Remove ambiguity | "how to play" → "how to play a turn in Catan" |
| `decompose` | Split complex queries | "setup and win" → ["setup", "win conditions"] |

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "query": { "type": "string" },
    "gameContext": { "type": "string" }
  },
  "required": ["query"]
}
```

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "originalQuery": { "type": "string" },
    "rewrittenQuery": { "type": "string" },
    "variations": {
      "type": "array",
      "items": { "type": "string" }
    },
    "addedTerms": {
      "type": "array",
      "items": { "type": "string" }
    },
    "confidence": { "type": "number" }
  }
}
```

### Usage Example

```json
{
  "id": "query-rewriter",
  "pluginId": "transform-rewrite-v1",
  "config": {
    "strategy": "expand",
    "maxVariations": 2,
    "addSynonyms": true
  }
}
```

### CRAG Integration

```
[Evaluation] ──(low quality)──→ [Query Rewriter] ──→ [Retrieval]
```

---

## Document Reranker

**Plugin ID**: `transform-rerank-v1`

Re-orders documents using cross-encoder models for improved relevance ordering.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `model` | string | `cross-encoder/ms-marco-MiniLM-L-6-v2` | Reranking model |
| `topK` | integer | `5` | Documents to keep after reranking |
| `batchSize` | integer | `32` | Documents per batch |
| `minScore` | number | `0.0` | Minimum score threshold |

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
          "content": { "type": "string" },
          "score": { "type": "number" }
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
    "documents": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "id": { "type": "string" },
          "content": { "type": "string" },
          "originalScore": { "type": "number" },
          "rerankedScore": { "type": "number" },
          "rankChange": { "type": "integer" }
        }
      }
    },
    "rerankedCount": { "type": "integer" },
    "filteredCount": { "type": "integer" }
  }
}
```

### Usage Example

```json
{
  "id": "reranker",
  "pluginId": "transform-rerank-v1",
  "config": {
    "topK": 5,
    "minScore": 0.3
  }
}
```

### Two-Stage Retrieval

```
[Vector Search (top 20)] ──→ [Reranker (top 5)] ──→ [Generation]
```

---

## Context Enricher

**Plugin ID**: `transform-enrich-v1`

Adds metadata and context to documents or queries.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `enrichments` | string[] | `["gameInfo"]` | Data to add |
| `gameInfoFields` | string[] | `["name", "publisher"]` | Game info fields |
| `documentMetadata` | boolean | `true` | Add doc metadata |

### Available Enrichments

- `gameInfo`: Add game name, publisher, year
- `sectionContext`: Add document section headers
- `relatedDocs`: Add links to related documents
- `userHistory`: Add user's previous questions

### Usage Example

```json
{
  "id": "enricher",
  "pluginId": "transform-enrich-v1",
  "config": {
    "enrichments": ["gameInfo", "sectionContext"],
    "gameInfoFields": ["name", "publisher", "complexity"]
  }
}
```

---

## Best Practices

### Query Rewriting

1. **Use for poor retrieval**: Trigger after low evaluation scores
2. **Limit variations**: 2-3 variations usually sufficient
3. **Preserve intent**: Don't change what user is asking
4. **Track improvements**: Measure retrieval quality before/after

### Reranking

1. **Retrieve more, rerank down**: Vector top-20 → Rerank top-5
2. **Balance cost/quality**: Cross-encoders are slower
3. **Consider caching**: Rerank results are cacheable
4. **Monitor rank changes**: Large changes indicate vector issues

### Testing

```csharp
[Fact]
public async Task QueryRewriter_ExpandsQuery()
{
    var input = PluginMocks.CreateInputWithPayload("""
        {"query": "setup"}
        """);

    var output = await Plugin.ExecuteAsync(input);

    output.Success.Should().BeTrue();
    var rewritten = output.Result!.RootElement.GetProperty("rewrittenQuery").GetString();
    rewritten!.Length.Should().BeGreaterThan("setup".Length);
}

[Fact]
public async Task Reranker_ImprovesScoringOrder()
{
    var input = PluginMocks.CreateInputWithPayload("""
        {
            "query": "victory conditions",
            "documents": [
                {"id": "1", "content": "Pizza recipes...", "score": 0.8},
                {"id": "2", "content": "To win, reach 10 points...", "score": 0.6}
            ]
        }
        """);

    var output = await Plugin.ExecuteAsync(input);

    var docs = output.Result!.RootElement.GetProperty("documents");
    // Relevant doc should now be first
    docs[0].GetProperty("id").GetString().Should().Be("2");
}
```
