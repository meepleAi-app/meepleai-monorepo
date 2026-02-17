# Generation Plugins

> **Response Creation with Large Language Models**

Generation plugins create human-readable responses from retrieved documents using LLMs. They handle prompt construction, citation inclusion, and response formatting.

## Answer Generator

**Plugin ID**: `generation-answer-v1`

Generates comprehensive answers using retrieved documents as context.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `model` | string | `gpt-4-turbo` | LLM model |
| `maxTokens` | integer | `500` | Maximum response length |
| `temperature` | number | `0.7` | Response creativity |
| `systemPrompt` | string | See below | System instructions |
| `includeCitations` | boolean | `true` | Add source citations |
| `citationFormat` | string | `"inline"` | `"inline"`, `"footnote"`, `"end"` |

**Default System Prompt**:
```
You are a helpful board game assistant. Answer questions about game rules and strategies using only the provided context. If the context doesn't contain enough information, say so clearly. Always be accurate and concise.
```

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
          "metadata": { "type": "object" }
        }
      }
    },
    "conversationHistory": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "role": { "type": "string" },
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
    "response": { "type": "string" },
    "citations": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "documentId": { "type": "string" },
          "excerpt": { "type": "string" },
          "relevance": { "type": "number" }
        }
      }
    },
    "tokensUsed": {
      "type": "object",
      "properties": {
        "prompt": { "type": "integer" },
        "completion": { "type": "integer" },
        "total": { "type": "integer" }
      }
    },
    "confidence": { "type": "number" }
  }
}
```

### Usage Example

```json
{
  "id": "answer-gen",
  "pluginId": "generation-answer-v1",
  "config": {
    "model": "gpt-4-turbo",
    "maxTokens": 750,
    "temperature": 0.5,
    "includeCitations": true,
    "citationFormat": "inline"
  }
}
```

### Custom System Prompt

```json
{
  "id": "answer-gen",
  "pluginId": "generation-answer-v1",
  "config": {
    "systemPrompt": "You are MeepleBot, an expert on board games. Answer questions using the provided rulebook excerpts. Always cite specific sections. If information is missing, recommend checking the official rulebook."
  }
}
```

---

## Summary Generator

**Plugin ID**: `generation-summary-v1`

Creates concise summaries of retrieved documents.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `model` | string | `gpt-3.5-turbo` | LLM model |
| `maxLength` | integer | `200` | Summary length in words |
| `style` | string | `"concise"` | `"concise"`, `"detailed"`, `"bullet"` |
| `focusOnQuery` | boolean | `true` | Emphasize query-relevant info |

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "summary": { "type": "string" },
    "keyPoints": {
      "type": "array",
      "items": { "type": "string" }
    },
    "documentsUsed": { "type": "integer" }
  }
}
```

### Usage Example

```json
{
  "id": "summarizer",
  "pluginId": "generation-summary-v1",
  "config": {
    "maxLength": 150,
    "style": "bullet",
    "focusOnQuery": true
  }
}
```

---

## Streaming Responses

Both generators support streaming for real-time response delivery:

### Configuration

```json
{
  "config": {
    "stream": true,
    "onChunk": "sse"
  }
}
```

### SSE Integration

```typescript
// Frontend consumption
const eventSource = new EventSource(`/api/rag/query?stream=true`);

eventSource.onmessage = (event) => {
  const chunk = JSON.parse(event.data);
  appendToResponse(chunk.text);
};

eventSource.addEventListener('done', (event) => {
  const final = JSON.parse(event.data);
  setCitations(final.citations);
});
```

---

## Prompt Engineering

### Context Window Management

```json
{
  "config": {
    "maxContextTokens": 4000,
    "documentPriority": "score",
    "truncationStrategy": "smart"
  }
}
```

**Truncation Strategies**:
- `"smart"`: Keep complete sentences, prioritize high-scoring docs
- `"simple"`: Cut at token limit
- `"summarize"`: Summarize overflow documents

### Few-Shot Examples

```json
{
  "config": {
    "fewShotExamples": [
      {
        "query": "How many players can play Catan?",
        "response": "Catan can be played with 3-4 players in the base game. With the 5-6 Player Extension, you can play with up to 6 players."
      }
    ]
  }
}
```

---

## Best Practices

### Model Selection

| Use Case | Recommended Model |
|----------|------------------|
| Simple Q&A | `gpt-3.5-turbo` |
| Complex rules | `gpt-4-turbo` |
| Cost-sensitive | `gpt-3.5-turbo` |
| Quality-critical | `gpt-4-turbo` |

### Temperature Settings

| Value | Behavior | Use For |
|-------|----------|---------|
| 0.0-0.3 | Deterministic | Rules, facts |
| 0.3-0.7 | Balanced | General Q&A |
| 0.7-1.0 | Creative | Strategy discussion |

### Citation Best Practices

1. **Always cite**: Users trust cited answers more
2. **Link to sources**: Enable verification
3. **Quote sparingly**: Paraphrase when clearer
4. **Handle missing info**: Clearly state limitations

### Testing

```csharp
[Fact]
public async Task AnswerGenerator_IncludesCitations()
{
    var input = PluginMocks.CreateInputWithPayload("""
        {
            "query": "How do I win in Catan?",
            "documents": [
                {"id": "rules-p5", "content": "The first player to reach 10 victory points wins..."}
            ]
        }
        """);

    var output = await Plugin.ExecuteAsync(input);

    output.Success.Should().BeTrue();
    output.Result!.RootElement.GetProperty("response").GetString()
        .Should().Contain("10");
    output.Result.RootElement.GetProperty("citations").GetArrayLength()
        .Should().BeGreaterThan(0);
}

[Fact]
public async Task AnswerGenerator_RespectsMaxTokens()
{
    var config = new PluginConfig
    {
        Parameters = JsonDocument.Parse("""{"maxTokens": 100}""")
    };

    var output = await Plugin.ExecuteAsync(input, config);

    output.Result!.RootElement.GetProperty("tokensUsed")
        .GetProperty("completion").GetInt32()
        .Should().BeLessThanOrEqualTo(100);
}
```
