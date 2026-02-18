# Validation Plugins

> **Output Verification and Guardrails**

Validation plugins ensure generated responses meet quality standards. They detect hallucinations, enforce content policies, and verify factual accuracy.

## Hallucination Detector

**Plugin ID**: `validation-hallucination-v1`

Detects claims in the response that aren't supported by source documents.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `model` | string | `gpt-4-turbo` | Verification model |
| `threshold` | number | `0.7` | Minimum support score |
| `checkCitations` | boolean | `true` | Verify cited sources |
| `extractClaims` | boolean | `true` | Extract individual claims |
| `strictMode` | boolean | `false` | Fail on any unsupported claim |

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "response": { "type": "string" },
    "documents": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "id": { "type": "string" },
          "content": { "type": "string" }
        }
      }
    },
    "citations": {
      "type": "array",
      "items": { "type": "string" }
    }
  },
  "required": ["response", "documents"]
}
```

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "isValid": { "type": "boolean" },
    "overallScore": { "type": "number" },
    "claims": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "text": { "type": "string" },
          "supported": { "type": "boolean" },
          "supportScore": { "type": "number" },
          "supportingDocId": { "type": "string" },
          "evidence": { "type": "string" }
        }
      }
    },
    "unsupportedClaims": {
      "type": "array",
      "items": { "type": "string" }
    },
    "citationAccuracy": { "type": "number" }
  }
}
```

### Usage Example

```json
{
  "id": "hallucination-check",
  "pluginId": "validation-hallucination-v1",
  "config": {
    "threshold": 0.8,
    "checkCitations": true,
    "strictMode": false
  }
}
```

### Handling Invalid Responses

```json
{
  "id": "e-valid",
  "source": "hallucination-check",
  "target": "exit",
  "condition": "output.result.isValid === true"
},
{
  "id": "e-invalid",
  "source": "hallucination-check",
  "target": "regeneration",
  "condition": "output.result.isValid === false",
  "dataTransform": "{ flaggedClaims: output.result.unsupportedClaims }"
}
```

---

## Guardrails

**Plugin ID**: `validation-guardrails-v1`

Enforces content policies and safety guidelines.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `policies` | string[] | See below | Active policies |
| `blockOnViolation` | boolean | `true` | Block violating responses |
| `sanitizeOutput` | boolean | `false` | Remove violations instead of blocking |
| `customPolicies` | object[] | `[]` | Custom policy rules |

**Default Policies**:
- `no-harmful-content`: Block dangerous instructions
- `no-personal-info`: Redact PII in responses
- `on-topic`: Ensure game-related content
- `no-competitor-promotion`: Avoid promoting competitors
- `appropriate-language`: Family-friendly content

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "response": { "type": "string" },
    "query": { "type": "string" },
    "context": { "type": "object" }
  },
  "required": ["response"]
}
```

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "isCompliant": { "type": "boolean" },
    "violations": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "policy": { "type": "string" },
          "severity": { "type": "string", "enum": ["low", "medium", "high"] },
          "description": { "type": "string" },
          "location": { "type": "string" }
        }
      }
    },
    "sanitizedResponse": { "type": "string" },
    "blocked": { "type": "boolean" }
  }
}
```

### Usage Example

```json
{
  "id": "guardrails",
  "pluginId": "validation-guardrails-v1",
  "config": {
    "policies": ["no-harmful-content", "on-topic", "appropriate-language"],
    "blockOnViolation": true
  }
}
```

### Custom Policies

```json
{
  "config": {
    "customPolicies": [
      {
        "name": "no-spoilers",
        "description": "Don't reveal game endings",
        "patterns": ["ending", "final boss", "twist"],
        "severity": "medium"
      },
      {
        "name": "cite-sources",
        "description": "Require source citations",
        "rule": "response must contain citations",
        "severity": "low"
      }
    ]
  }
}
```

---

## Fact Checker

**Plugin ID**: `validation-factcheck-v1`

Verifies factual claims against known game data.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `database` | string | `"game-facts"` | Fact database to check against |
| `strictNumbers` | boolean | `true` | Verify numerical claims |
| `fuzzyMatch` | boolean | `true` | Allow close matches |

### Fact Categories

- **Game mechanics**: Player counts, victory conditions
- **Components**: Card counts, dice, tokens
- **Rules**: Turn structure, actions, restrictions
- **Metadata**: Publisher, designer, release year

### Usage Example

```json
{
  "id": "fact-check",
  "pluginId": "validation-factcheck-v1",
  "config": {
    "strictNumbers": true,
    "database": "game-facts"
  }
}
```

---

## Validation Pipeline Pattern

Complete validation flow:

```
[Generation] ──→ [Hallucination Check] ──→ [Fact Check] ──→ [Guardrails] ──→ [Exit]
                        │                       │                │
                        ▼                       ▼                ▼
                  [Regenerate]           [Correct Facts]   [Sanitize/Block]
```

### Multi-Stage Validation

```json
{
  "nodes": [
    {"id": "hallucination", "pluginId": "validation-hallucination-v1"},
    {"id": "factcheck", "pluginId": "validation-factcheck-v1"},
    {"id": "guardrails", "pluginId": "validation-guardrails-v1"}
  ],
  "edges": [
    {"source": "generation", "target": "hallucination"},
    {"source": "hallucination", "target": "factcheck",
     "condition": "output.result.isValid"},
    {"source": "factcheck", "target": "guardrails",
     "condition": "output.result.isValid"},
    {"source": "guardrails", "target": "exit",
     "condition": "output.result.isCompliant"}
  ]
}
```

---

## Best Practices

### Hallucination Prevention

1. **Check before delivery**: Always validate before sending to user
2. **Cite aggressively**: Encourage citations in generation
3. **Limit creativity**: Lower temperature for factual content
4. **Track patterns**: Monitor common hallucination types

### Guardrails Strategy

1. **Layer defenses**: Multiple validation stages
2. **Fail safely**: Block rather than deliver bad content
3. **Log violations**: Track for improvement
4. **Regular updates**: Keep policies current

### Testing

```csharp
[Fact]
public async Task HallucinationDetector_FlagsUnsupportedClaims()
{
    var input = PluginMocks.CreateInputWithPayload("""
        {
            "response": "Catan requires exactly 5 players.",
            "documents": [
                {"id": "1", "content": "Catan can be played with 3-4 players."}
            ]
        }
        """);

    var output = await Plugin.ExecuteAsync(input);

    output.Result!.RootElement.GetProperty("isValid").GetBoolean().Should().BeFalse();
    output.Result.RootElement.GetProperty("unsupportedClaims").GetArrayLength()
        .Should().BeGreaterThan(0);
}

[Fact]
public async Task Guardrails_BlocksOffTopicContent()
{
    var input = PluginMocks.CreateInputWithPayload("""
        {
            "response": "Here's how to cook pasta...",
            "query": "How do I play Catan?"
        }
        """);

    var output = await Plugin.ExecuteAsync(input);

    output.Result!.RootElement.GetProperty("isCompliant").GetBoolean().Should().BeFalse();
    output.Result.RootElement.GetProperty("violations")[0]
        .GetProperty("policy").GetString().Should().Be("on-topic");
}
```

### Performance Considerations

- Hallucination detection is expensive (~1-2s)
- Run only on final responses, not intermediate
- Consider async validation for non-critical checks
- Cache policy decisions where possible
