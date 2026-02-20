# Agent Chat with RAG Integration

## Overview

Complete implementation of agent chat functionality with RAG-powered responses. Enables users to chat with AI agents that have access to game-specific knowledge base documents.

**Status**: ✅ Production Ready
**Version**: 1.0
**Date**: 2026-02-17

---

## Architecture

### System Flow

```
User → /agents/{id} → AgentInfoCard
                         ├─ Status Check (GET /agents/{id}/status)
                         │  ├─ ✅ Ready → Enable Chat Tab
                         │  └─ ❌ Not Ready → Blocking UI
                         │
                         └─ Chat Tab (if ready)
                            ├─ Click "Inizia Conversazione"
                            ├─ Create Thread (POST /chat-threads)
                            │  └─ Pass: agentId, gameId, title
                            │
                            └─ ChatThreadView (embedded)
                               ├─ Send Message
                               ├─ SSE Stream (POST /agents/{id}/chat)
                               │  └─ RAG Pipeline:
                               │     1. Load AgentConfiguration
                               │     2. Validate SelectedDocumentIds
                               │     3. Generate Query Embedding
                               │     4. Vector Search in Qdrant
                               │     5. Filter by Score (≥0.6)
                               │     6. Build Context from Chunks
                               │     7. LLM with RAG Context
                               │     8. Stream Tokens + Citations
                               │
                               └─ Display Response + Citations
```

### Data Model

```
Agent
  ├─ Id: Guid
  ├─ Name: string
  ├─ Type: AgentType
  ├─ IsActive: bool
  └─ → AgentConfiguration (1:N, IsCurrent filter)
       ├─ SelectedDocumentIds: List<Guid> (JSON)
       ├─ LlmProvider: enum
       ├─ LlmModel: string
       ├─ AgentMode: enum
       └─ → VectorDocuments (N:M via SelectedDocumentIds)
            ├─ GameId: Guid
            ├─ PdfDocumentId: Guid
            └─ TotalChunks: int

ChatThread
  ├─ UserId: Guid
  ├─ GameId?: Guid
  ├─ AgentId?: Guid  ← NEW: Links to Agent
  ├─ AgentType?: string
  └─ Messages: List<ChatMessage>
       ├─ Role: user | assistant
       ├─ Content: string
       ├─ CitationsJson?: string
       └─ TokenCount?: int
```

---

## API Reference

### GET /api/v1/agents/{id}/status

**Purpose**: Check if agent is ready for chat (KB validation).

**Authentication**: Required (session)

**Response** (200):
```json
{
  "agentId": "guid",
  "name": "Tutor Agent",
  "isActive": true,
  "isReady": true,
  "hasConfiguration": true,
  "hasDocuments": true,
  "documentCount": 5,
  "ragStatus": "Ready",
  "blockingReason": null
}
```

**Response** (Not Ready):
```json
{
  "agentId": "guid",
  "name": "Test Agent",
  "isActive": true,
  "isReady": false,
  "hasConfiguration": true,
  "hasDocuments": false,
  "documentCount": 0,
  "ragStatus": "Not initialized",
  "blockingReason": "Agent has no documents in Knowledge Base"
}
```

**Error Codes**:
- `404`: Agent not found

---

### POST /api/v1/agents/{id}/chat

**Purpose**: Chat with agent using SSE streaming with RAG context.

**Authentication**: Required (session)

**Request**:
```json
{
  "message": "How do I setup the game?",
  "chatThreadId": "optional-existing-thread-id"
}
```

**Response**: SSE stream with events

**Event Types**:
| Type | Code | Data | Description |
|------|------|------|-------------|
| StateUpdate | 0 | `{ message, chatThreadId }` | Status message |
| Citations | 1 | `[{ documentId, pageNumber, score }]` | RAG citations |
| Token | 7 | `{ token }` | Streamed text token |
| Complete | 4 | `{ totalTokens, chatThreadId, ... }` | Stream complete |
| Error | 5 | `{ errorMessage, errorCode }` | Error occurred |

**Error Codes**:
- `AGENT_NOT_FOUND`: Agent ID invalid
- `AGENT_NOT_CONFIGURED`: No AgentConfiguration found
- `AGENT_NO_DOCUMENTS`: SelectedDocumentIds empty
- `EMBEDDING_FAILED`: Embedding service error
- `SEARCH_FAILED`: Qdrant search error
- `THREAD_NOT_FOUND`: ChatThreadId invalid

**Example SSE Stream**:
```
data: {"type":0,"data":{"message":"Searching knowledge base...","chatThreadId":"abc-123"},"timestamp":"..."}

data: {"type":1,"data":[{"documentId":"doc-1","pageNumber":15,"score":0.89}],"timestamp":"..."}

data: {"type":0,"data":{"message":"Generating response...","chatThreadId":"abc-123"},"timestamp":"..."}

data: {"type":7,"data":{"token":"According"},"timestamp":"..."}

data: {"type":7,"data":{"token":" to"},"timestamp":"..."}

data: {"type":4,"data":{"totalTokens":25,"chatThreadId":"abc-123","promptTokens":12,"completionTokens":13},"timestamp":"..."}
```

---

## Frontend Integration

### useAgentStatus Hook

**Purpose**: Check agent readiness before enabling chat.

**Usage**:
```tsx
import { useAgentStatus } from '@/hooks/useAgentStatus';

function MyComponent({ agentId }) {
  const { status, isLoading, error, refetch } = useAgentStatus(agentId);

  if (isLoading) return <Spinner />;
  if (error) return <Error message={error} />;

  if (!status?.isReady) {
    return <BlockingUI reason={status?.blockingReason} />;
  }

  return <ChatInterface agentId={agentId} />;
}
```

**Return Type**:
```typescript
{
  status: AgentStatus | null;
  isLoading: boolean;
  error: string | null;
  refetch: () => Promise<void>;
}
```

### AgentInfoCard Component

**Location**: `apps/web/src/components/agent/AgentInfoCard.tsx`

**Features**:
- ✅ 3 tabs: Chat, History, Knowledge Base
- ✅ Auto status check on mount
- ✅ Blocking UI if agent not ready
- ✅ Embedded ChatThreadView when ready
- ✅ Fullscreen mode toggle
- ✅ Auto-thread creation on first message

**Props**:
```typescript
{
  agentId: string;
  agentName: string;
  readOnly?: boolean;
  className?: string;
}
```

**States**:
```
Loading → Status Check
   ├─ Not Ready → Blocking UI (⚠️ + Config Link)
   ├─ Ready (no thread) → "Inizia Conversazione" button
   └─ Ready (with thread) → Embedded Chat + Fullscreen toggle
```

---

## Validation Rules

### Agent Readiness Requirements

Agent is **ready for chat** when ALL conditions are met:

1. ✅ `Agent.IsActive == true`
2. ✅ `AgentConfiguration` exists with `IsCurrent == true`
3. ✅ `AgentConfiguration.SelectedDocumentIds.Count > 0`
4. ✅ Documents are in Qdrant (implicit - checked during search)

### Error Handling Matrix

| Condition | Backend Error | Frontend UI |
|-----------|---------------|-------------|
| Agent not found | 404 | "Agent not found" error page |
| Agent inactive | AGENT_NOT_FOUND | N/A (filtered from list) |
| No configuration | AGENT_NOT_CONFIGURED | "Agent non configurato" blocking UI |
| No documents | AGENT_NO_DOCUMENTS | "Agent non ha documenti nella KB" blocking UI |
| Embedding fails | EMBEDDING_FAILED | SSE error event → error banner |
| Search fails | SEARCH_FAILED | SSE error event → error banner |

---

## RAG Pipeline Details

### Step-by-Step Process

**1. Validation** (SendAgentMessageCommandHandler:90-129):
```csharp
// Load agent configuration
var agentConfig = await _dbContext.AgentConfigurations
    .FirstOrDefaultAsync(c => c.AgentId == agentId && c.IsCurrent, ct);

// Parse selected documents
var documentIds = JsonSerializer.Deserialize<List<Guid>>(
    agentConfig.SelectedDocumentIdsJson);

// Validate not empty
if (documentIds.Count == 0) → Error("AGENT_NO_DOCUMENTS")
```

**2. Embedding Generation** (SendAgentMessageCommandHandler:183-194):
```csharp
var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(
    userQuestion, ct);
// Returns: float[] embedding (384 or 768 dimensions)
```

**3. Vector Search** (SendAgentMessageCommandHandler:196-215):
```csharp
var searchResult = await _qdrantService.SearchAsync(
    gameId: thread.GameId?.ToString() ?? "default",
    queryEmbedding: embeddingResult.Embeddings[0],
    limit: 10,
    documentIds: selectedDocumentIds.Select(id => id.ToString()).ToList(),
    ct);
// Returns: List<SearchResultItem> with Score, Text, PdfId, Page
```

**4. Context Building** (SendAgentMessageCommandHandler:217-227, 311-320):
```csharp
var relevantChunks = searchResult.Results
    .Where(r => r.Score >= 0.6)  // Configurable threshold
    .ToList();

var context = BuildContextFromChunks(relevantChunks);
// Format: "[1] (Score: 0.85, Page: 15)\n{chunk text}\n\n---\n\n[2]..."
```

**5. LLM with Context** (SendAgentMessageCommandHandler:248-264):
```csharp
var systemPrompt = "You are {agentName}, a specialized board game AI assistant. " +
    "Answer questions based ONLY on the provided context...";

var userPrompt = $"Context from game documents:\n{ragContext}\n\n" +
    $"Question: {userQuestion}\n\nProvide a clear answer based on the context above.";

await _llmService.GenerateCompletionStreamAsync(systemPrompt, userPrompt, ct);
// Yields: Token events via SSE
```

**6. Citations Emission** (SendAgentMessageCommandHandler:229-242):
```csharp
var citations = relevantChunks.Select(c => new {
    documentId = c.PdfId,
    pageNumber = c.Page,
    score = c.Score
}).ToList();

yield return CreateEvent(StreamingEventType.Citations, citations);
```

---

## Configuration

### Agent Configuration Requirements

For **Player** or **Ledger** modes:
- **MUST** have `SelectedDocumentIds.Count >= 1` (enforced in AgentConfiguration.cs:154)

For **Chat** mode:
- **RECOMMENDED** to have documents for RAG responses
- Falls back to generic LLM if no documents (with user-visible warning)

### Search Parameters

**Configurable** (currently hardcoded in SendAgentMessageCommandHandler:218):
```csharp
var minScore = 0.6;        // Relevance threshold (0.0-1.0)
var limit = 10;            // Max chunks to retrieve
```

**Future Enhancement**: Load from `AgentConfiguration.Strategy` parameters.

---

## Testing

### E2E Test Coverage

**File**: `apps/web/e2e/agent/agent-chat-page-rag.spec.ts`

**Test Cases** (7 scenarios):
1. ✅ Blocking UI when agent has no KB documents
2. ✅ Chat enabled when agent is ready
3. ✅ Thread creation and embedding
4. ✅ RAG-powered SSE streaming with citations
5. ✅ Fullscreen mode toggle functionality
6. ✅ SSE connection maintained during fullscreen
7. ✅ KB tab displays documents correctly

**Error Scenarios** (2 tests):
8. ✅ Agent not found (404 handling)
9. ✅ Status check failure (500 handling)

**Responsive** (1 test):
10. ✅ Mobile viewport adaptation

**Run Tests**:
```bash
cd apps/web
pnpm test:e2e e2e/agent/agent-chat-page-rag.spec.ts
```

---

## Performance Considerations

### RAG Pipeline Latency

**Expected Timings**:
- Embedding generation: ~100-200ms (local sentence-transformers)
- Vector search (Qdrant): ~50-150ms (depends on collection size)
- Context building: <10ms (in-memory)
- LLM streaming: 2-5s (depends on response length + model)

**Total**: ~2.5-5.5s for complete response

### Optimization Opportunities

1. **Parallel Operations**: Embedding + Config loading can be parallelized
2. **Caching**: Cache AgentConfiguration per session
3. **Streaming Start**: Start LLM streaming while still searching (speculative execution)

---

## Troubleshooting

### Common Issues

**"Agent non configurato"**:
- **Cause**: No AgentConfiguration with `IsCurrent = true`
- **Fix**: Admin → Configure agent via `/admin/ai-lab/agents/{id}/edit`

**"Agent non ha documenti nella KB"**:
- **Cause**: `SelectedDocumentIds` is empty
- **Fix**: Admin → Add documents to agent KB

**"Embedding generation failed"**:
- **Cause**: Embedding service down or network error
- **Fix**: Check `embedding-service` container health
- **Check**: `docker logs meepleai-embedding-service`

**"Vector search failed"**:
- **Cause**: Qdrant collection doesn't exist or network error
- **Fix**: Check Qdrant health, verify game documents indexed
- **Check**: `docker logs meepleai-qdrant`

**"No relevant context found"**:
- **Cause**: All chunks scored below minScore (0.6)
- **Behavior**: LLM responds with "No relevant context found in knowledge base"
- **Fix**: Check if documents are properly indexed, adjust minScore threshold

---

## Future Enhancements

### Phase 2 Improvements

1. **Dynamic Parameters**:
   - Load `minScore`, `topK` from AgentConfiguration.Strategy
   - Per-agent tuning of retrieval parameters

2. **Hybrid Search**:
   - Integrate `HybridSearchEngine` (BM25 + Vector + Reranking)
   - Improve relevance with keyword matching

3. **Multi-Turn Context**:
   - Include previous messages in embedding
   - Maintain conversation context for follow-up questions

4. **Citation Click Handling**:
   - Navigate to PDF viewer at specific page
   - Highlight relevant chunk in document

5. **Response Quality Metrics**:
   - Track confidence scores
   - User feedback integration (thumbs up/down)

### Code Refactoring (Task #6)

Extract RAG logic into reusable service:
```csharp
public interface IAgentChatService
{
    IAsyncEnumerable<RagStreamingEvent> StreamChatResponseAsync(
        Guid agentId,
        string userQuestion,
        Guid? chatThreadId = null,
        CancellationToken cancellationToken = default);
}
```

**Benefits**:
- Testability (mock service in unit tests)
- Code reuse (multiple handlers can use same logic)
- Easier to add caching layer

---

## Migration Guide

### For Existing Agent Configurations

**No migration needed** - backward compatible:
- Agents without documents → Blocking UI prevents chat
- Agents with documents → Chat works immediately
- Existing ChatThreads → AgentId nullable, works without it

### For Frontend Components

**Breaking Changes**: None

**New Features Available**:
- Use `useAgentStatus(agentId)` to check readiness
- Use `AgentInfoCard` with embedded chat
- Pass `agentId` in `createThread()` for agent-specific threads

---

## Related Documentation

- **RAG System**: `docs/ai/rag-architecture.md`
- **Agent Configuration**: `docs/features/agent-configuration.md`
- **SSE Streaming**: `docs/api/sse-streaming.md`
- **Vector Search**: `docs/ai/vector-search.md`
- **Chat System**: `docs/features/chat-threads.md`

---

## Changelog

**v1.0 (2026-02-17)** - Initial Implementation:
- ✅ RAG integration in SendAgentMessageCommandHandler
- ✅ Agent status validation endpoint
- ✅ Embedded chat in AgentInfoCard
- ✅ Fullscreen mode support
- ✅ E2E test coverage
- ✅ KB empty blocking UI

**Files Modified**: 11 (5 backend, 5 frontend, 1 test)

**Lines of Code**: ~600 LOC added/modified

**Test Coverage**: 10 E2E scenarios + existing unit tests
