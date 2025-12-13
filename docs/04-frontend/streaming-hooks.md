# Streaming Hooks Documentation

**Issue**: #1451
**Status**: ✅ Complete
**Last Updated**: 2025-12-13T10:59:23.970Z

## Overview

This document describes the unified streaming hooks architecture implemented to consolidate duplicate streaming implementations (`useChatStreaming` + `useChatStream`) into a single, maintainable solution.

## Architecture

### Unified Hook: `useChatStreaming`

The main hook that consumers interact with. It provides a unified interface for both real SSE streaming and mock streaming modes.

**Location**: `apps/web/src/lib/hooks/useChatStreaming.ts`

**Features**:
- ✅ Single unified interface for both modes
- ✅ Environment variable mode selection
- ✅ Zero breaking changes for existing consumers
- ✅ Identical API for mock and real modes
- ✅ Comprehensive TypeScript support

### Helper Hooks

#### `useRealStreaming`

Real Server-Sent Events (SSE) implementation using fetch with ReadableStream.

**Location**: `apps/web/src/lib/hooks/useRealStreaming.ts`

**Features**:
- Fetch API with ReadableStream for SSE
- Event-driven state updates (token, stateUpdate, citations, complete, error, heartbeat, followUpQuestions)
- Proper cancellation with AbortController
- Citation and snippet support (#859)
- Follow-up questions support (CHAT-02)

#### `useMockStreaming`

Mock streaming implementation using setTimeout to simulate word-by-word streaming.

**Location**: `apps/web/src/lib/hooks/useMockStreaming.ts`

**Features**:
- Simulated word-by-word streaming
- Configurable typing speed (15 words/second)
- Random mock responses in Italian
- Mock citations and follow-up questions
- Controlled cancellation and cleanup

## Usage

### Basic Usage (Real SSE Mode - Default)

```tsx
import { useChatStreaming } from '@/lib/hooks/useChatStreaming';

function MyComponent() {
  const [streamingState, streamingControls] = useChatStreaming({
    onComplete: (answer, snippets, metadata) => {
      console.log('Streaming complete:', answer);
      console.log('Citations:', metadata.citations);
      console.log('Follow-up questions:', metadata.followUpQuestions);
    },
    onError: (error) => {
      console.error('Streaming error:', error);
    }
  });

  const handleAsk = () => {
    streamingControls.startStreaming(gameId, userQuery, chatId, 'Hybrid');
  };

  return (
    <div>
      {streamingState.isStreaming && (
        <div>
          <p>{streamingState.state}</p>
          <p>{streamingState.currentAnswer}</p>
        </div>
      )}
      <button onClick={handleAsk} disabled={streamingState.isStreaming}>
        Ask Question
      </button>
    </div>
  );
}
```

### Mock Mode (Development/Testing)

#### Option 1: Programmatic Mode Selection

```tsx
import { useChatStreaming } from '@/lib/hooks/useChatStreaming';

function MyComponent() {
  const [streamingState, streamingControls] = useChatStreaming({
    useMock: true, // Force mock mode
    onComplete: (answer) => {
      console.log('Mock streaming complete:', answer);
    }
  });

  // ... rest of component
}
```

#### Option 2: Environment Variable

```bash
# .env.local
NEXT_PUBLIC_USE_MOCK_STREAMING=true
```

```tsx
import { useChatStreaming } from '@/lib/hooks/useChatStreaming';

function MyComponent() {
  // Automatically uses mock mode based on environment variable
  const [streamingState, streamingControls] = useChatStreaming({
    onComplete: (answer) => {
      console.log('Streaming complete:', answer);
    }
  });

  // ... rest of component
}
```

## API Reference

### `useChatStreaming(options?): [StreamingState, StreamingControls]`

#### Parameters

- `options` (optional): Configuration object
  - `useMock?: boolean` - Force mock mode (default: reads from `NEXT_PUBLIC_USE_MOCK_STREAMING`)
  - `onComplete?: (answer, snippets, metadata) => void` - Callback when streaming completes
  - `onError?: (error) => void` - Callback when streaming errors occur

#### Returns

Array tuple `[state, controls]`:

**State Object**:
```typescript
{
  state: string | null;           // Current processing state
  currentAnswer: string;          // Accumulated answer text
  snippets: Snippet[];            // Legacy snippets (for backward compatibility)
  citations: Citation[];          // PDF citations from RAG (#859)
  followUpQuestions: string[];    // Suggested follow-up questions (CHAT-02)
  totalTokens: number;            // Token count
  confidence: number | null;      // Confidence score (0-1)
  isStreaming: boolean;           // Streaming status
  error: string | null;           // Error message if any
}
```

**Controls Object**:
```typescript
{
  startStreaming: (gameId, query, chatId?, searchMode?) => void;
  stopStreaming: () => void;
  reset: () => void;
}
```

### `startStreaming(gameId, query, chatId?, searchMode?)`

Start streaming a response.

**Parameters**:
- `gameId: string` - Game ID for context
- `query: string` - User's question
- `chatId?: string` - Optional chat thread ID
- `searchMode?: string` - Search mode ('Hybrid', 'Vector', 'Keyword') - default: 'Hybrid' (AI-14)

### `stopStreaming()`

Stop the current stream immediately.

### `reset()`

Reset the hook to initial state (stops streaming + clears all state).

## Migration Guide

### Migrating from `useChatStream`

The old `useChatStream` hook is deprecated and will be removed in Sprint 5.

**Before**:
```tsx
import { useChatStream } from '@/store/chat';

function MyComponent() {
  const { isStreaming, streamedContent, error, startStream, stopStream } = useChatStream(chatId);

  const handleSend = async () => {
    await startStream(userMessage);
  };

  return (
    <div>
      {isStreaming && <p>{streamedContent}</p>}
      {error && <p>Error: {error}</p>}
    </div>
  );
}
```

**After**:
```tsx
import { useChatStreaming } from '@/lib/hooks/useChatStreaming';

function MyComponent() {
  const [state, controls] = useChatStreaming({
    useMock: true, // Enable mock mode (similar to old useChatStream behavior)
    onComplete: (answer) => {
      // Handle completion
    }
  });

  const handleSend = () => {
    controls.startStreaming(gameId, userMessage, chatId);
  };

  return (
    <div>
      {state.isStreaming && <p>{state.currentAnswer}</p>}
      {state.error && <p>Error: {state.error}</p>}
    </div>
  );
}
```

**Key Changes**:
1. Import from `@/lib/hooks/useChatStreaming` instead of `@/store/chat`
2. Use tuple destructuring `[state, controls]` instead of object destructuring
3. Access properties via `state.*` and `controls.*`
4. Use `state.currentAnswer` instead of `streamedContent`
5. Add `useMock: true` option to maintain mock behavior
6. Pass `gameId` as first parameter to `startStreaming`

## When to Use Each Mode

### Real SSE Mode (Production)

**Use When**:
- ✅ Interacting with real backend
- ✅ Production environment
- ✅ Integration tests with backend
- ✅ E2E tests

**Pros**:
- Real streaming behavior
- Actual backend integration
- Real citations and metadata
- Production-accurate performance

**Cons**:
- Requires backend to be running
- Network dependency
- Slower for unit tests

### Mock Mode (Development/Testing)

**Use When**:
- ✅ Unit testing components
- ✅ Frontend development without backend
- ✅ Storybook stories
- ✅ Rapid prototyping
- ✅ Demos without backend access

**Pros**:
- No backend dependency
- Fast and predictable
- Configurable responses
- Offline development

**Cons**:
- Simulated behavior only
- Not production-accurate
- Mock data only

## Environment Variables

### `NEXT_PUBLIC_USE_MOCK_STREAMING`

Controls default streaming mode.

**Type**: `"true" | "false"`
**Default**: `undefined` (uses real SSE)

**Example**:
```bash
# .env.development
NEXT_PUBLIC_USE_MOCK_STREAMING=true

# .env.production
NEXT_PUBLIC_USE_MOCK_STREAMING=false
```

**Priority Order**:
1. Explicit `useMock` prop (highest)
2. `NEXT_PUBLIC_USE_MOCK_STREAMING` environment variable
3. Default behavior: real SSE (lowest)

## Testing

### Testing with Mock Mode

```tsx
import { render, screen } from '@testing-library/react';
import { useChatStreaming } from '@/lib/hooks/useChatStreaming';

function TestComponent() {
  const [state, controls] = useChatStreaming({ useMock: true });

  return (
    <div>
      <button onClick={() => controls.startStreaming('game-1', 'How to play?')}>
        Ask
      </button>
      {state.isStreaming && <p>Streaming...</p>}
      <p>{state.currentAnswer}</p>
    </div>
  );
}

test('should stream mock response', async () => {
  render(<TestComponent />);

  const button = screen.getByText('Ask');
  fireEvent.click(button);

  await waitFor(() => {
    expect(screen.getByText(/Streaming/)).toBeInTheDocument();
  });

  await waitFor(() => {
    expect(screen.getByText(/Ecco la risposta/)).toBeInTheDocument();
  }, { timeout: 5000 });
});
```

### Testing with Real SSE Mode

```tsx
import { renderHook, act } from '@testing-library/react';
import { useChatStreaming } from '@/lib/hooks/useChatStreaming';

test('should handle real SSE stream', async () => {
  global.fetch = jest.fn(() => Promise.resolve({
    ok: true,
    body: createMockReadableStream([
      'event: token\ndata: {"token":"Hello"}\n\n',
      'event: complete\ndata: {"totalTokens":1}\n\n'
    ])
  }));

  const { result } = renderHook(() => useChatStreaming());

  act(() => {
    result.current[1].startStreaming('game-1', 'test query');
  });

  await waitFor(() => {
    expect(result.current[0].currentAnswer).toBe('Hello');
    expect(result.current[0].isStreaming).toBe(false);
  });
});
```

## Error Handling

### Handling Errors in Real Mode

```tsx
const [state, controls] = useChatStreaming({
  onError: (error) => {
    // Log error
    console.error('Streaming error:', error);

    // Show user-friendly message
    toast.error('Failed to get answer. Please try again.');

    // Track error
    analytics.track('streaming_error', { error });
  }
});
```

### Common Errors

| Error | Cause | Solution |
|-------|-------|----------|
| `HTTP error! status: 401` | Unauthorized | Check authentication |
| `HTTP error! status: 404` | Endpoint not found | Verify backend is running |
| `No response body` | Empty response | Check backend response format |
| `Network error` | Connection failed | Check network/CORS |
| `Stream cancelled by user` | User stopped stream | Normal - no action needed |

## Performance Considerations

### Real SSE Mode

- **Latency**: ~100-300ms first token
- **Throughput**: ~5-15 tokens/second
- **Memory**: ~1-2MB per active stream
- **Network**: Persistent connection while streaming

### Mock Mode

- **Latency**: ~200ms initial delay
- **Throughput**: 15 words/second (configurable)
- **Memory**: <100KB per stream
- **Network**: None

## Best Practices

### ✅ DO

- Use real SSE mode in production
- Use mock mode for unit tests and development
- Handle errors gracefully with `onError`
- Clean up on unmount (automatic with hooks)
- Use `reset()` when changing contexts
- Test both modes in integration tests

### ❌ DON'T

- Don't use mock mode in production
- Don't ignore errors from `onError`
- Don't start multiple streams without stopping previous
- Don't forget to handle loading states
- Don't hardcode mode - use environment variables
- Don't mix old `useChatStream` with new `useChatStreaming`

## Troubleshooting

### Mock mode not activating

**Problem**: Hook uses real SSE even with `NEXT_PUBLIC_USE_MOCK_STREAMING=true`

**Solution**:
1. Restart dev server (env vars require restart)
2. Check env variable name (must start with `NEXT_PUBLIC_`)
3. Verify value is exactly `"true"` (string)
4. Use explicit `useMock: true` prop as override

### Real mode fails with CORS error

**Problem**: Network error or CORS policy violation

**Solution**:
1. Ensure backend is running
2. Check `NEXT_PUBLIC_API_BASE` environment variable
3. Verify backend CORS configuration allows frontend origin
4. Check `credentials: 'include'` is set (automatic in hook)

### Streaming stops unexpectedly

**Problem**: Stream ends before completion

**Solution**:
1. Check backend logs for errors
2. Verify `complete` event is sent by backend
3. Check network tab for connection drops
4. Ensure `stopStreaming()` isn't called prematurely

## Related Documentation

- [API Specification](../03-api/board-game-ai-api-specification.md)
- [Hybrid RAG Architecture](../01-architecture/adr/adr-001-hybrid-rag.md)
- [Issue #859: PDF Citations](../07-project-management/issues/0859-pdf-citations.md)
- [Issue #1083: Chat Implementation](../07-project-management/issues/1083-chat-implementation.md)
- [Issue #1451: Streaming Consolidation](../../.github/ISSUES_TEMPLATES_CR/05-streaming-hooks-consolidation.md)

## Changelog

### 2025-01-21 - Issue #1451

- ✅ Created unified `useChatStreaming` hook
- ✅ Extracted `useRealStreaming` helper
- ✅ Extracted `useMockStreaming` helper
- ✅ Added `NEXT_PUBLIC_USE_MOCK_STREAMING` environment variable
- ✅ Deprecated `useChatStream` (removal in Sprint 5)
- ✅ Zero breaking changes for existing consumers
- ✅ Comprehensive documentation created
- ✅ Tests updated for both modes

### Previous

- 2024-xx-xx - Issue #1083: Initial `useChatStream` implementation
- 2024-xx-xx - Issue #1006: `useChatStreaming` for real SSE
- 2024-xx-xx - Issue #859: Added PDF citations support

---

**Maintainers**: Engineering Lead
**Last Review**: 2025-01-21
**Next Review**: Sprint 5 (before `useChatStream` removal)

