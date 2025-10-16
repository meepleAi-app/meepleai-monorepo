# CHAT-01: Streaming Responses with Server-Sent Events - Implementation

**Status**: Backend Complete ✅ | Frontend Pending ⏳
**Issue**: #402
**Epic**: EPIC-03 (Chat Interface)
**Effort**: L (2 weeks)

## Overview

Implemented real-time streaming of AI responses using Server-Sent Events (SSE) to provide immediate feedback and reduce perceived latency. Extended proven streaming pattern from Explain agent (API-02) to QA agent with token-by-token delivery from LLM.

## Architecture Summary

### Backend Components

#### 1. LLM Streaming Support
- **Files Modified**:
  - `apps/api/src/Api/Services/ILlmService.cs` - Added `GenerateCompletionStreamAsync()` method
  - `apps/api/src/Api/Services/LlmService.cs` - Implemented OpenRouter streaming
  - `apps/api/src/Api/Services/OllamaLlmService.cs` - Implemented Ollama streaming

**Key Features**:
- Token-by-token streaming via `IAsyncEnumerable<string>`
- Proper SSE format parsing from OpenRouter/Ollama APIs
- Graceful error handling with early yield break
- Cancellation token support

#### 2. Streaming Services
- **Files Created**:
  - `apps/api/src/Api/Services/IStreamingQaService.cs` - Interface for streaming QA
  - `apps/api/src/Api/Services/StreamingQaService.cs` - Implementation

**Event Flow**:
1. `StateUpdate` - "Generating embeddings..."
2. `StateUpdate` - "Searching vector database..."
3. `Citations` - Retrieved snippets from Qdrant
4. `StateUpdate` - "Generating answer..."
5. `Token` (multiple) - Individual tokens from LLM
6. `Complete` - Final metadata (tokens, confidence)

**Cache Integration** (AI-05):
- Check cache before streaming
- If cached, simulate streaming with small delays for UX consistency
- Cache complete response after streaming finishes

#### 3. SSE Endpoints
- **File Modified**: `apps/api/src/Api/Program.cs`

**New Endpoint**:
```http
POST /api/v1/agents/qa/stream
Content-Type: application/json
X-API-Key: <optional>
Cookie: <session-cookie>

{
  "gameId": "uuid",
  "query": "How many players?",
  "chatId": "uuid?" // optional
}
```

**Response** (SSE format):
```
Content-Type: text/event-stream
Cache-Control: no-cache
Connection: keep-alive

data: {"Type":"StateUpdate","Data":{"message":"Generating embeddings..."},"Timestamp":"..."}

data: {"Type":"Citations","Data":{"citations":[...]},"Timestamp":"..."}

data: {"Type":"Token","Data":{"token":"The"},"Timestamp":"..."}

data: {"Type":"Token","Data":{"token":" game"},"Timestamp":"..."}

data: {"Type":"Complete","Data":{"totalTokens":45,"confidence":0.92},"Timestamp":"..."}
```

#### 4. Event Models
- **File Modified**: `apps/api/src/Api/Models/Contracts.cs`

**New Types**:
- `StreamingEventType.Token` - Individual LLM token
- `StreamingToken(string token)` - Token data model

### Service Registration
**File Modified**: `apps/api/src/Api/Program.cs` (line 160)
```csharp
builder.Services.AddScoped<IStreamingQaService, StreamingQaService>(); // CHAT-01
```

## Technical Implementation Details

### LLM Streaming Pattern

**OpenRouter**:
- Uses SSE format: `data: {json}\n\n`
- Sends `[DONE]` message when complete
- Streams delta content in `choices[0].delta.content`

**Ollama**:
- Sends NDJSON (newline-delimited JSON)
- Includes `done: true` in final message
- Streams content in `message.content`

### Error Handling Strategy

**Approach**: Separate error handling from streaming logic to avoid C# yield return limitations

```csharp
// Phase 1: Initiate request with try-catch
HttpResponseMessage? response = null;
try {
    response = await _httpClient.SendAsync(...);
    if (!response.IsSuccessStatusCode) {
        yield break; // Safe here
    }
} catch (Exception ex) {
    // Log error
    yield break; // Safe here
}

// Phase 2: Process stream without try-catch (allows yield return)
using (response) {
    // Stream processing with yield return
}
```

### Observability Integration

**Metrics** (OPS-02):
- `meepleai.rag.requests.total` with `operation=qa-stream`
- `meepleai.ai.tokens.used` with `operation=qa-stream`
- `meepleai.rag.confidence.score` with `operation=qa-stream`

**Logging**:
- Request start/end with correlation ID
- Token count tracking
- Error logging with context
- Chat persistence of complete response

**AI Request Logging** (ADM-01):
- Full request/response logging to `ai_requests` table
- Includes: endpoint, query, answer preview, latency, tokens, confidence

## What's Implemented ✅

### Backend (Complete)
- ✅ Streaming LLM service (`ILlmService.GenerateCompletionStreamAsync`)
- ✅ Streaming QA service (`IStreamingQaService.AskStreamAsync`)
- ✅ SSE endpoint at `/api/v1/agents/qa/stream`
- ✅ Proper SSE headers (Content-Type, Cache-Control, Connection)
- ✅ Token-by-token streaming from OpenRouter
- ✅ Integration with AI-05 response caching
- ✅ Chat persistence of user query and assistant response
- ✅ AI request logging with metrics
- ✅ Error handling with error events
- ✅ Graceful cancellation support
- ✅ Authentication/authorization checks
- ✅ Service registration in DI container
- ✅ Builds successfully

### Frontend (Pending)
- ⏳ Create `useChatStreaming()` React hook
- ⏳ EventSource API integration
- ⏳ Update `chat.tsx` with streaming UI
- ⏳ Incremental rendering of response
- ⏳ Stop button to cancel streaming
- ⏳ Typing indicator animation
- ⏳ Error handling with auto-reconnect
- ⏳ Fallback to regular HTTP if EventSource unavailable

### Testing (Pending)
- ⏳ Backend unit tests for `StreamingQaService`
- ⏳ Backend integration tests for SSE endpoint
- ⏳ Frontend unit tests for `useChatStreaming` hook
- ⏳ E2E tests (submit → stream → stop)
- ⏳ Performance validation (P95 < 500ms first token)

## Files Changed

### Created
1. `apps/api/src/Api/Services/IStreamingQaService.cs`
2. `apps/api/src/Api/Services/StreamingQaService.cs`
3. `docs/issue/chat-01-streaming-sse-implementation.md` (this file)

### Modified
1. `apps/api/src/Api/Services/ILlmService.cs` - Added streaming method
2. `apps/api/src/Api/Services/LlmService.cs` - Implemented OpenRouter streaming
3. `apps/api/src/Api/Services/OllamaLlmService.cs` - Implemented Ollama streaming
4. `apps/api/src/Api/Models/Contracts.cs` - Added Token event type and model
5. `apps/api/src/Api/Program.cs` - Added service registration and SSE endpoint

## Testing the Implementation

### Manual Testing with curl

```bash
# Test streaming QA endpoint
curl -X POST http://localhost:8080/api/v1/agents/qa/stream \
  -H "Content-Type: application/json" \
  -H "Cookie: <your-session-cookie>" \
  -d '{
    "gameId": "tic-tac-toe",
    "query": "How many players can play?"
  }'
```

Expected response:
```
data: {"Type":"StateUpdate","Data":{"message":"Generating embeddings for query..."},"Timestamp":"2025-10-17T..."}

data: {"Type":"StateUpdate","Data":{"message":"Searching vector database..."},"Timestamp":"2025-10-17T..."}

data: {"Type":"Citations","Data":{"citations":[...]},"Timestamp":"2025-10-17T..."}

data: {"Type":"Token","Data":{"token":"Tic"},"Timestamp":"2025-10-17T..."}

data: {"Type":"Token","Data":{"token":"-tac"},"Timestamp":"2025-10-17T..."}

...

data: {"Type":"Complete","Data":{"estimatedReadingTimeMinutes":0,"totalTokens":25,"confidence":0.95},"Timestamp":"2025-10-17T..."}
```

### Frontend Integration Example

```typescript
// apps/web/src/lib/hooks/useChatStreaming.ts
import { useState, useCallback, useRef } from 'react';

interface StreamingEvent {
  Type: string;
  Data: any;
  Timestamp: string;
}

export function useChatStreaming(endpoint: string) {
  const [response, setResponse] = useState('');
  const [isStreaming, setIsStreaming] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const eventSourceRef = useRef<EventSource | null>(null);

  const startStream = useCallback(async (payload: any) => {
    setIsStreaming(true);
    setError(null);
    setResponse('');

    try {
      const eventSource = new EventSource(endpoint);
      eventSourceRef.current = eventSource;

      eventSource.onmessage = (event) => {
        const data: StreamingEvent = JSON.parse(event.data);

        if (data.Type === 'Token') {
          setResponse(prev => prev + data.Data.token);
        } else if (data.Type === 'Complete') {
          setIsStreaming(false);
          eventSource.close();
        } else if (data.Type === 'Error') {
          setError(new Error(data.Data.errorMessage));
          setIsStreaming(false);
          eventSource.close();
        }
      };

      eventSource.onerror = () => {
        setError(new Error('Stream connection error'));
        setIsStreaming(false);
        eventSource.close();
      };
    } catch (err) {
      setError(err as Error);
      setIsStreaming(false);
    }
  }, [endpoint]);

  const stopStream = useCallback(() => {
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
      setIsStreaming(false);
    }
  }, []);

  return { response, isStreaming, error, startStream, stopStream };
}
```

## Next Steps

### Priority 1: Frontend Implementation
1. Create `useChatStreaming` React hook
2. Update `chat.tsx` with streaming UI
3. Add stop button and typing indicator
4. Implement error handling with retry

### Priority 2: Testing
1. Write backend unit tests
2. Write backend integration tests
3. Write frontend unit tests
4. Write E2E tests for full flow

### Priority 3: Performance Validation
1. Measure P95 latency for first token
2. Verify 20-50 tokens/second throughput
3. Load test with 50 concurrent streams
4. Optimize if needed

## References

- **Issue**: #402 - CHAT-01: Implement streaming responses with Server-Sent Events
- **Related**: #299 - API-02: Streaming Explain agent (reference implementation)
- **Docs**:
  - `CLAUDE.md` - Project architecture
  - `docs/observability.md` - OPS-01 logging and health checks
  - `docs/ops-02-opentelemetry-design.md` - OPS-02 metrics and tracing

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>
