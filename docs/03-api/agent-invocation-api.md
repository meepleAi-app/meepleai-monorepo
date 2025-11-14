# Agent Invocation API

**Issue**: #867 - Game Master Agent Integration
**Status**: Implemented
**Version**: 1.0

## Overview

The Agent Invocation API allows clients to invoke AI agents for intelligent knowledge retrieval and game rules interpretation. Agents use configurable strategies to orchestrate vector search, quality tracking, and confidence scoring within the KnowledgeBase bounded context.

## Architecture

**Bounded Context**: KnowledgeBase
**Pattern**: DDD with CQRS (MediatR)
**Decision**: ADR-004 (Bounded Context as Workspace)

### Components

```
┌─────────────────────────────────────────────────────┐
│         KnowledgeBase Bounded Context               │
├─────────────────────────────────────────────────────┤
│ Domain:                                             │
│  • Agent (aggregate)                                │
│  • AgentInvocationContext (value object)           │
│  • AgentOrchestrationService (domain service)      │
│                                                     │
│ Application:                                        │
│  • InvokeAgentCommand (MediatR command)            │
│  • InvokeAgentCommandHandler (orchestration)       │
│  • AgentResponseDto (response DTO)                 │
│                                                     │
│ HTTP:                                               │
│  • POST /api/v1/agents/{id}/invoke                 │
└─────────────────────────────────────────────────────┘
```

## Endpoint

### Invoke Agent

Executes an AI agent with the given query and context.

**Endpoint**: `POST /api/v1/agents/{id}/invoke`

**Authentication**: Required (cookie or API key)

**Authorization**: All authenticated users

**Request**:
```json
{
  "query": "What are the rules for moving pawns in chess?",
  "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "chatThreadId": "7c9e6679-7425-40de-944b-e07fc1f90ae7"
}
```

**Request Parameters**:
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `query` | string | Yes | Natural language query |
| `gameId` | guid | No | Game context for filtering embeddings |
| `chatThreadId` | guid | No | Chat thread for conversation continuity |

**Response** (200 OK):
```json
{
  "invocationId": "9c7e8679-8425-40de-944b-e07fc1f90ae9",
  "agentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "agentName": "Game Master",
  "agentType": "RulesInterpreter",
  "query": "What are the rules for moving pawns in chess?",
  "results": [
    {
      "vectorDocumentId": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
      "textContent": "Pawns move forward one square...",
      "pageNumber": 5,
      "relevanceScore": 0.92,
      "rank": 1,
      "searchMethod": "vector"
    }
  ],
  "confidence": 0.92,
  "qualityLevel": "High",
  "executedAt": "2025-11-14T10:30:00Z",
  "resultCount": 10
}
```

**Response Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `invocationId` | guid | Unique invocation identifier |
| `agentId` | guid | ID of the agent that processed the query |
| `agentName` | string | Name of the agent |
| `agentType` | string | Agent type (RAG, RulesInterpreter, Citation, etc.) |
| `query` | string | Original user query |
| `results` | array | Search results with relevance scores |
| `confidence` | double | Overall confidence score (0.0-1.0) |
| `qualityLevel` | string | Quality classification (High/Medium/Low) |
| `executedAt` | datetime | Timestamp of execution |
| `resultCount` | int | Number of results returned |

**Quality Levels**:
- **High**: Confidence ≥ 0.80
- **Medium**: Confidence ≥ 0.50 and < 0.80
- **Low**: Confidence < 0.50

**Error Responses**:

400 Bad Request - Invalid query or inactive agent:
```json
{
  "error": "Agent is not active: Game Master"
}
```

404 Not Found - Agent doesn't exist:
```json
{
  "error": "Agent not found: 3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

500 Internal Server Error - System failure:
```json
{
  "detail": "Failed to generate query embedding",
  "title": "Agent invocation failed",
  "status": 500
}
```

## Usage Examples

### Basic Invocation (TypeScript)

```typescript
// Invoke agent with game context
const response = await fetch(`/api/v1/agents/${agentId}/invoke`, {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json'
  },
  credentials: 'include',
  body: JSON.stringify({
    query: "How do I setup the game board?",
    gameId: "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  })
});

const result = await response.json();
console.log(`Confidence: ${result.confidence}`);
console.log(`Quality: ${result.qualityLevel}`);
console.log(`Results: ${result.resultCount}`);
```

### With Chat Context

```typescript
// Continue conversation in chat thread
const response = await fetch(`/api/v1/agents/${agentId}/invoke`, {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json'
  },
  credentials: 'include',
  body: JSON.stringify({
    query: "And what about the scoring rules?",
    chatThreadId: "7c9e6679-7425-40de-944b-e07fc1f90ae7"
  })
});
```

### Error Handling

```typescript
try {
  const response = await fetch(`/api/v1/agents/${agentId}/invoke`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ query })
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.error || error.title);
  }

  const result = await response.json();

  // Check quality
  if (result.qualityLevel === "Low") {
    console.warn("Low confidence response - verify results");
  }

  return result;
} catch (error) {
  console.error("Agent invocation failed:", error);
  throw error;
}
```

## Agent Selection Logic

The `AgentOrchestrationService` automatically selects the appropriate agent based on query classification:

| Query Type | Patterns | Selected Agent |
|------------|----------|----------------|
| **Rules Interpretation** | "rule", "legal move", "can i", "allowed to", "phase", "turn", "setup" | RulesInterpreter |
| **Citation Verification** | "source", "citation", "where did", "reference" | CitationAgent |
| **Confidence Assessment** | "how sure", "confident", "certain", "accuracy" | ConfidenceAgent |
| **Conversation Continuation** | "and ", "but ", "also ", "you said", "earlier" | ConversationAgent |
| **General Question** | Default | RagAgent |

**Fallback Logic**:
1. Try specialized agent for query type
2. Fall back to any active RAG agent
3. Use most recently invoked active agent

## Domain Services Integration

The agent invocation uses the following KnowledgeBase domain services:

1. **VectorSearchDomainService**: Performs vector similarity search
   - Uses cosine similarity with floating-point clamping
   - Returns top-K results above minimum score threshold
   - Ranks results by relevance

2. **QualityTrackingDomainService**: Calculates confidence scores
   - Weighted average of top search results
   - Rank-based weighting (higher rank = lower weight)
   - 5-layer validation support (ADR-001)

## Performance Considerations

### Candidate Embeddings

- **With GameId**: Filters to game-specific embeddings (recommended)
- **Without GameId**: Returns empty results (safety measure)
- **With ChatThreadId**: Not yet supported (logged warning)

### Strategy Parameters

Agents use configurable strategies with parameters:

```csharp
AgentStrategy.HybridSearch(
    vectorWeight: 0.7,   // Vector search weight (default)
    topK: 10,            // Number of results
    minScore: 0.70       // Minimum relevance threshold
);
```

### Execution Time

- Typical: 50-200ms (with game context)
- Without context: <10ms (empty results)
- Embedding generation: 30-100ms (OpenRouter API call)

## Monitoring & Logging

**Logged Events**:
```
Information: "Invoking agent {AgentId} with query: {Query}"
Debug: "Retrieved agent: {AgentName} (Type: {AgentType}, Strategy: {Strategy})"
Debug: "Generated query embedding: {Dimensions} dimensions"
Debug: "Retrieved {Count} candidate embeddings"
Information: "Agent invocation completed: InvocationId={InvocationId}, Results={ResultCount}, Confidence={Confidence:F3}"
Warning: "No candidate embeddings found for GameId: {GameId}"
Error: "Failed to invoke agent {AgentId}: {ErrorMessage}"
```

**Metrics to Track**:
- Invocation count per agent
- Average confidence scores
- Response times
- Empty result rate
- Error rate

## Security

**Authentication**: All requests require active session or valid API key

**Authorization**: All authenticated users can invoke agents (no role restrictions)

**Input Validation**:
- Query: Non-empty string, trimmed
- GameId/ChatThreadId: Valid GUID format
- Agent: Must be active

**Rate Limiting**: Inherits from global API rate limits

## Related Endpoints

- `GET /api/v1/agents` - List all agents
- `GET /api/v1/agents/{id}` - Get agent details
- `POST /api/v1/agents` - Create agent (Admin only)
- `PUT /api/v1/agents/{id}/configure` - Configure strategy (Admin only)

## Testing

Run agent invocation tests:
```bash
dotnet test --filter "FullyQualifiedName~AgentOrchestrationServiceTests|FullyQualifiedName~InvokeAgentCommandHandlerTests"
```

## Future Enhancements

- [ ] ChatThread-based candidate filtering (requires schema update)
- [ ] Multi-agent consensus (invoke multiple agents, merge results)
- [ ] Agent performance analytics dashboard
- [ ] Streaming responses for real-time feedback
- [ ] Agent warm-up/caching for frequently used agents

## References

- **ADR-004**: AI Agents Bounded Context Architecture
- **Issue #866**: AI Agents Entity & Configuration (prerequisite)
- **Issue #869**: Move Validation Agent (follows same pattern)
