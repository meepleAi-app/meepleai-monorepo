/**
 * useAgentChatStream Hook Tests
 *
 * Tests:
 * 1. SSE event parsing with camelCase (matching backend SseJsonOptions)
 * 2. Race condition prevention via request ID tracking
 * 3. State management (streaming, errors, completion)
 * 4. Abort handling on stopStreaming/agent switch
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { toast } from 'sonner';
import { QA_TOO_MANY_REQUESTS_MESSAGE } from '@/components/chat-unified/qaStreamErrors';
import { steppedFetch } from '@/test-utils/sse-stepped-stream';
import { useAgentChatStream } from '../useAgentChatStream';

// Mock sonner toast
vi.mock('sonner', () => ({
  toast: {
    info: vi.fn(),
    error: vi.fn(),
    success: vi.fn(),
  },
}));

// ─── Helpers ─────────────────────────────────────────────

/** Build a camelCase SSE data line matching backend SseJsonOptions (camelCase) */
function sseEvent(type: number, data: unknown): string {
  return `data: ${JSON.stringify({ type, data, timestamp: new Date().toISOString() })}\n\n`;
}

/** Create a ReadableStream from SSE event strings */
function createSSEStream(events: string[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder();
  let index = 0;
  return new ReadableStream({
    pull(controller) {
      if (index < events.length) {
        controller.enqueue(encoder.encode(events[index]));
        index++;
      } else {
        controller.close();
      }
    },
  });
}

/** #4335: what `onError` hands over when the stream failed before any word of the answer. */
const NOTHING_ARRIVED = { partialAnswer: '', citations: [] };

// StreamingEventType enum values
const EventType = {
  StateUpdate: 0,
  Citations: 1,
  Token: 7,
  Complete: 4,
  Error: 5,
  Heartbeat: 6,
  FollowUpQuestions: 8,
  ModelDowngrade: 21,
  // Debug events (Issue #4916) - names must match useAgentChatStream.ts constants
  DebugAgentRouter: 10,
  DebugStrategySelected: 11,
  DebugRetrievalStart: 12,
  DebugRetrievalResults: 13,
  DebugPluginExecution: 14,
  DebugValidationLayer: 15,
  DebugPromptContext: 16,
  DebugCostUpdate: 17,
} as const;

// ─── Tests ───────────────────────────────────────────────

describe('useAgentChatStream', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn();
    globalThis.fetch = fetchMock;
    vi.mocked(toast.info).mockClear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ─── Issue #4139: the request target ──────────────────────────────────────
  //
  // These are the tests the issue asked for, and they exist because a mocked
  // `fetch` resolves against ANY string: the hook posted to
  // /api/v1/agents/{agentId}/chat for its whole life and every test here stayed
  // green, because none of them looked at the URL. What is asserted is therefore
  // the requested path itself.
  //
  // The list below is the set of paths the backend actually mounts under
  // /agents. Re-derive it with:
  //
  //   grep -rn 'Map\(Post\|Get\|Put\|Delete\)("/agents' apps/api/src/Api --include=*.cs
  //
  // If this list and the hook disagree, the hook is wrong — that is the whole
  // point of keeping it here.
  describe('request target (#4139)', () => {
    const MOUNTED_AGENT_POST_ROUTES = [
      '/api/v1/agents/qa',
      '/api/v1/agents/qa/stream',
      '/api/v1/agents/explain',
      '/api/v1/agents/explain/stream',
      '/api/v1/agents/setup',
      '/api/v1/agents/feedback',
      '/api/v1/agents/player-mode/suggest',
      '/api/v1/agents/user',
      '/api/v1/agents/create-with-setup',
      '/api/v1/agents/quick-create',
    ];

    function captureRequest() {
      const stream = createSSEStream([sseEvent(EventType.Complete, { totalTokens: 1 })]);
      fetchMock.mockResolvedValueOnce({ ok: true, body: stream });
      return () => {
        expect(fetchMock).toHaveBeenCalled();
        const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
        return { url, body: JSON.parse(String(init.body)) as Record<string, unknown> };
      };
    }

    it('posts to a path the backend actually mounts', async () => {
      const read = captureRequest();
      const { result } = renderHook(() => useAgentChatStream());

      await act(async () => {
        result.current.sendMessage('game-1', 'Quante azioni per turno?');
      });

      const { url } = read();
      const path = new URL(url, 'http://localhost').pathname;
      expect(MOUNTED_AGENT_POST_ROUTES).toContain(path);
    });

    it('posts to /api/v1/agents/qa/stream specifically', async () => {
      const read = captureRequest();
      const { result } = renderHook(() => useAgentChatStream());

      await act(async () => {
        result.current.sendMessage('game-1', 'Quante azioni per turno?');
      });

      expect(new URL(read().url, 'http://localhost').pathname).toBe('/api/v1/agents/qa/stream');
    });

    // The route that was there before. Asserted as an absence so a revert is red.
    it('never posts to the unmounted /agents/{id}/chat', async () => {
      const read = captureRequest();
      const { result } = renderHook(() => useAgentChatStream());

      await act(async () => {
        result.current.sendMessage('game-1', 'Quante azioni per turno?');
      });

      expect(read().url).not.toMatch(/\/agents\/[^/]+\/chat$/);
    });

    // QaRequest (the endpoint's DTO) reads gameId + query + chatId. A body shaped
    // for the old route would be accepted by a mock and rejected by the server.
    it('sends the body shape QaRequest expects', async () => {
      const read = captureRequest();
      const { result } = renderHook(() => useAgentChatStream());

      await act(async () => {
        result.current.sendMessage('game-1', 'Quante azioni per turno?', 'thread-9');
      });

      const { body } = read();
      expect(body).toMatchObject({
        gameId: 'game-1',
        query: 'Quante azioni per turno?',
        chatId: 'thread-9',
      });
      expect(body).not.toHaveProperty('message');
      expect(body).not.toHaveProperty('agentId');
      expect(body).not.toHaveProperty('chatThreadId');
    });

    it('omits chatId when no thread is given', async () => {
      const read = captureRequest();
      const { result } = renderHook(() => useAgentChatStream());

      await act(async () => {
        result.current.sendMessage('game-1', 'Domanda senza thread');
      });

      expect(read().body).not.toHaveProperty('chatId');
    });
  });

  it('initializes with default state', () => {
    const { result } = renderHook(() => useAgentChatStream());

    expect(result.current.state).toEqual({
      statusMessage: null,
      currentAnswer: '',
      followUpQuestions: [],
      citations: [],
      isStreaming: false,
      error: null,
      tierLimit: null,
      chatThreadId: null,
      totalTokens: 0,
      debugSteps: [],
      modelDowngrade: null,
      strategyTier: null,
      executionId: null,
      connectionStatus: 'idle',
      retryCount: 0,
    });
  });

  it('parses camelCase SSE events from backend', async () => {
    const events = [
      sseEvent(EventType.StateUpdate, { message: 'Starting chat', chatThreadId: 'thread-123' }),
      sseEvent(EventType.Token, { token: 'Hello' }),
      sseEvent(EventType.Token, { token: ' world' }),
      sseEvent(EventType.Complete, { totalTokens: 42, chatThreadId: 'thread-123' }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({
      ok: true,
      body: stream,
    });

    const onComplete = vi.fn();
    const { result } = renderHook(() => useAgentChatStream({ onComplete }));

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi', 'thread-123');
      // Wait for stream processing
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(onComplete).toHaveBeenCalledWith(
      'Hello world',
      expect.objectContaining({
        totalTokens: 42,
        chatThreadId: 'thread-123',
      })
    );
    expect(result.current.state.isStreaming).toBe(false);
    expect(result.current.state.currentAnswer).toBe('Hello world');
  });

  it('rejects PascalCase events (ensures camelCase compliance)', async () => {
    // Simulate WRONG format (PascalCase) - should be silently ignored
    const wrongEvent = `data: ${JSON.stringify({
      Type: EventType.Token,
      Data: { token: 'should not appear' },
      Timestamp: new Date().toISOString(),
    })}\n\n`;

    const events = [wrongEvent];
    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({
      ok: true,
      body: stream,
    });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    // PascalCase event.Type is undefined → falls to default case → no token appended
    expect(result.current.state.currentAnswer).toBe('');
  });

  it('handles HTTP errors', async () => {
    fetchMock.mockResolvedValueOnce({
      ok: false,
      status: 500,
    });

    const onError = vi.fn();
    const { result } = renderHook(() => useAgentChatStream({ onError }));

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    // First failure triggers retry — state shows disconnected, not final error
    // The connection goes to 'disconnected' and schedules a retry
    expect(result.current.state.connectionStatus).toBe('disconnected');
    expect(result.current.state.isStreaming).toBe(true); // still trying
  });

  describe('a 429 on the answer route (#4388)', () => {
    // Un test rosso non deve lasciare orologio e fetch finti a quelli che seguono.
    afterEach(() => {
      vi.useRealTimers();
      vi.unstubAllGlobals();
    });

    /**
     * #4388: il backend tiene aperte al massimo due risposte per utente e rifiuta la terza con 429.
     * Non è una connessione caduta: ritentare dopo due secondi arriverebbe mentre le altre due sono
     * ancora in corso, e «HTTP 429» non dice all'utente che cosa fare.
     */
    it('does not retry a 429, and tells the user to wait for an answer in progress', async () => {
      vi.useFakeTimers();
      const fetch429 = vi.fn(async () => ({ ok: false, status: 429 }));
      vi.stubGlobal('fetch', fetch429);
      const onError = vi.fn();

      const { result } = renderHook(() => useAgentChatStream({ onError }));
      act(() => {
        result.current.sendMessage('game-1', 'Quante carte si pescano?');
      });
      await vi.runAllTimersAsync();

      expect(fetch429).toHaveBeenCalledTimes(1);
      expect(result.current.state.retryCount).toBe(0);
      expect(result.current.state.error).toBe(QA_TOO_MANY_REQUESTS_MESSAGE);
      expect(result.current.state.isStreaming).toBe(false);
      expect(result.current.state.connectionStatus).toBe('error');
      expect(onError).toHaveBeenCalledWith(QA_TOO_MANY_REQUESTS_MESSAGE, NOTHING_ARRIVED);
    });

    it('lets the next question through after a 429', async () => {
      vi.useFakeTimers();
      const fetchSequence = vi
        .fn()
        .mockResolvedValueOnce({ ok: false, status: 429 })
        .mockResolvedValueOnce({
          ok: true,
          body: createSSEStream([
            sseEvent(EventType.Token, { token: 'Sette carte.' }),
            sseEvent(EventType.Complete, { totalTokens: 1 }),
          ]),
        });
      vi.stubGlobal('fetch', fetchSequence);

      const { result } = renderHook(() => useAgentChatStream());
      act(() => {
        result.current.sendMessage('game-1', 'prima');
      });
      await vi.runAllTimersAsync();
      expect(result.current.state.error).toBe(QA_TOO_MANY_REQUESTS_MESSAGE);

      act(() => {
        result.current.sendMessage('game-1', 'seconda');
      });
      await vi.runAllTimersAsync();

      expect(fetchSequence).toHaveBeenCalledTimes(2);
      expect(result.current.state.error).toBeNull();
      expect(result.current.state.currentAnswer).toBe('Sette carte.');
    });
  });

  it('handles SSE error events', async () => {
    const events = [
      sseEvent(EventType.Error, { errorMessage: 'Agent not found', errorCode: 'AGENT_NOT_FOUND' }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const onError = vi.fn();
    const { result } = renderHook(() => useAgentChatStream({ onError }));

    await act(async () => {
      result.current.sendMessage('agent-bad', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.error).toBe('Agent not found');
    expect(onError).toHaveBeenCalledWith('Agent not found', NOTHING_ARRIVED);
  });

  it('sets isStreaming true during active stream', async () => {
    // Create a stream that doesn't close immediately
    let resolveStream: (() => void) | undefined;
    const streamPromise = new Promise<void>(resolve => {
      resolveStream = resolve;
    });

    const stream = new ReadableStream<Uint8Array>({
      async pull(controller) {
        await streamPromise;
        controller.close();
      },
    });

    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 10));
    });

    expect(result.current.state.isStreaming).toBe(true);

    // Cleanup
    resolveStream?.();
  });

  it('accumulates follow-up questions', async () => {
    const events = [
      sseEvent(EventType.Token, { token: 'Answer' }),
      sseEvent(EventType.FollowUpQuestions, { questions: ['Q1?', 'Q2?'] }),
      sseEvent(EventType.Complete, { totalTokens: 5 }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const onComplete = vi.fn();
    const { result } = renderHook(() => useAgentChatStream({ onComplete }));

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(onComplete).toHaveBeenCalledWith(
      'Answer',
      expect.objectContaining({
        followUpQuestions: ['Q1?', 'Q2?'],
      })
    );
  });

  it('resets state on reset()', async () => {
    const events = [
      sseEvent(EventType.Token, { token: 'partial' }),
      sseEvent(EventType.Complete, { totalTokens: 1 }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.currentAnswer).toBe('partial');

    act(() => {
      result.current.reset();
    });

    expect(result.current.state.currentAnswer).toBe('');
    expect(result.current.state.isStreaming).toBe(false);
  });

  // Issue #4139: this asserted the body of the unmounted /agents/{id}/chat route
  // ({ message, chatThreadId }). The target is now /agents/qa/stream, whose DTO
  // (QaRequest) reads gameId + query + chatId.
  it('includes chatId in request body when a thread is provided', async () => {
    const stream = createSSEStream([sseEvent(EventType.Complete, { totalTokens: 0 })]);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('game-1', 'Hello', 'existing-thread');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    const fetchCall = fetchMock.mock.calls[0];
    const requestBody = JSON.parse(fetchCall[1].body);
    expect(requestBody).toEqual({
      gameId: 'game-1',
      query: 'Hello',
      chatId: 'existing-thread',
    });
  });

  it('ignores heartbeat events silently', async () => {
    const events = [
      sseEvent(EventType.Heartbeat, null),
      sseEvent(EventType.Token, { token: 'ok' }),
      sseEvent(EventType.Complete, { totalTokens: 1 }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.currentAnswer).toBe('ok');
  });

  it('handles malformed JSON gracefully', async () => {
    const events = [
      'data: {invalid json}\n\n',
      sseEvent(EventType.Token, { token: 'ok' }),
      sseEvent(EventType.Complete, { totalTokens: 1 }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    // Should skip malformed line and continue
    expect(result.current.state.currentAnswer).toBe('ok');
  });

  // ─── Debug Events (Issue #4916) ───────────────────────────

  it('captures DebugRetrievalResults event in debugSteps', async () => {
    const payload = { filteredCount: 5, totalResults: 10, latencyMs: 120 };
    const events = [
      sseEvent(EventType.DebugRetrievalResults, payload),
      sseEvent(EventType.Complete, { totalTokens: 1 }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.debugSteps).toHaveLength(1);
    expect(result.current.state.debugSteps[0].type).toBe(EventType.DebugRetrievalResults);
    expect(result.current.state.debugSteps[0].payload).toMatchObject(payload);
    expect(result.current.state.debugSteps[0].name).toBe('Retrieval Results');
  });

  it('accumulates multiple debug events in order', async () => {
    const events = [
      sseEvent(EventType.DebugRetrievalStart, { query: 'test', gameId: 'g1' }),
      sseEvent(EventType.DebugRetrievalResults, {
        filteredCount: 3,
        totalResults: 8,
        latencyMs: 80,
      }),
      sseEvent(EventType.DebugPromptContext, {
        systemPrompt: 'You are an AI',
        userPromptPreview: 'What are the rules?',
        estimatedPromptTokens: 200,
      }),
      sseEvent(EventType.Complete, { totalTokens: 5 }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.debugSteps).toHaveLength(3);
    expect(result.current.state.debugSteps[0].type).toBe(EventType.DebugRetrievalStart);
    expect(result.current.state.debugSteps[1].type).toBe(EventType.DebugRetrievalResults);
    expect(result.current.state.debugSteps[2].type).toBe(EventType.DebugPromptContext);
  });

  it('captures DebugCostUpdate with token breakdown', async () => {
    const payload = {
      model: 'claude-3-haiku',
      promptTokens: 500,
      completionTokens: 150,
      totalTokens: 650,
      confidence: 0.88,
    };
    const events = [
      sseEvent(EventType.DebugCostUpdate, payload),
      sseEvent(EventType.Complete, { totalTokens: 650 }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.debugSteps).toHaveLength(1);
    expect(result.current.state.debugSteps[0].type).toBe(EventType.DebugCostUpdate);
    expect(result.current.state.debugSteps[0].payload).toMatchObject(payload);
    expect(result.current.state.debugSteps[0].name).toBe('Cost Update');
  });

  it('clears debugSteps on reset()', async () => {
    const events = [
      sseEvent(EventType.DebugRetrievalResults, {
        filteredCount: 5,
        totalResults: 10,
        latencyMs: 50,
      }),
      sseEvent(EventType.Complete, { totalTokens: 1 }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.debugSteps).toHaveLength(1);

    act(() => {
      result.current.reset();
    });

    expect(result.current.state.debugSteps).toHaveLength(0);
  });

  // ─── Model Downgrade Events ─────────────────────────────

  it('handles ModelDowngrade event for free user with upgrade message', async () => {
    const events = [
      sseEvent(EventType.Token, { token: 'Ollama response' }),
      sseEvent(EventType.ModelDowngrade, {
        originalModel: 'meta-llama/llama-3.3-70b-instruct:free',
        fallbackModel: 'llama3:8b',
        reason: '429 rate limited',
        isLocalFallback: true,
        upgradeMessage: 'Passa a Premium per modelli più affidabili.',
      }),
      sseEvent(EventType.Complete, { totalTokens: 10 }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.modelDowngrade).not.toBeNull();
    expect(result.current.state.modelDowngrade!.originalModel).toBe(
      'meta-llama/llama-3.3-70b-instruct:free'
    );
    expect(result.current.state.modelDowngrade!.fallbackModel).toBe('llama3:8b');
    expect(result.current.state.modelDowngrade!.isLocalFallback).toBe(true);
    expect(result.current.state.modelDowngrade!.upgradeMessage).toContain('Premium');
  });

  it('handles ModelDowngrade event for premium user without upgrade link', async () => {
    const events = [
      sseEvent(EventType.Token, { token: 'GPT response' }),
      sseEvent(EventType.ModelDowngrade, {
        originalModel: 'anthropic/claude-3.5-haiku',
        fallbackModel: 'openai/gpt-4o-mini',
        reason: 'provider_unavailable',
        isLocalFallback: false,
        upgradeMessage: null,
      }),
      sseEvent(EventType.Complete, { totalTokens: 20 }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.modelDowngrade).not.toBeNull();
    expect(result.current.state.modelDowngrade!.isLocalFallback).toBe(false);
    expect(result.current.state.modelDowngrade!.upgradeMessage).toBeNull();
  });

  // ─── Task 3: Model Downgrade Toast ───────────────────────

  it('shows rate_limited toast when ModelDowngrade reason is rate_limited', async () => {
    const events = [
      sseEvent(EventType.ModelDowngrade, {
        originalModel: 'meta-llama/llama-3.3-70b-instruct:free',
        fallbackModel: 'llama3:8b',
        reason: 'rate_limited',
        isLocalFallback: true,
        upgradeMessage: null,
      }),
      sseEvent(EventType.Complete, { totalTokens: 5 }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(toast.info).toHaveBeenCalledWith(
      'Modello temporaneamente cambiato per limiti di utilizzo',
      { duration: 5000 }
    );
  });

  it('shows generic downgrade toast when ModelDowngrade reason is not rate_limited', async () => {
    const events = [
      sseEvent(EventType.ModelDowngrade, {
        originalModel: 'anthropic/claude-3.5-haiku',
        fallbackModel: 'openai/gpt-4o-mini',
        reason: 'provider_unavailable',
        isLocalFallback: false,
        upgradeMessage: null,
      }),
      sseEvent(EventType.Complete, { totalTokens: 10 }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(toast.info).toHaveBeenCalledWith(
      'Modello alternativo in uso per garantire la risposta',
      { duration: 5000 }
    );
  });

  // ─── Task 4: Rate Limit User-Friendly Error Messages ─────

  it('shows Italian rate_limited message for errorCode rate_limited', async () => {
    const events = [
      sseEvent(EventType.Error, {
        errorMessage: 'Rate limit exceeded',
        errorCode: 'rate_limited',
      }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const onError = vi.fn();
    const { result } = renderHook(() => useAgentChatStream({ onError }));

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.error).toBe(
      'Hai raggiunto il limite di messaggi. Riprova tra qualche minuto.'
    );
    expect(onError).toHaveBeenCalledWith(
      'Hai raggiunto il limite di messaggi. Riprova tra qualche minuto.',
      NOTHING_ARRIVED
    );
  });

  // #4166 (ADR-095 D6): il gateway ferma la chiamata al tetto di spesa e lo dice, in italiano
  it.each([
    [
      'AI_BUDGET_DAILY_EXHAUSTED',
      'Il servizio AI è in pausa fino a domani: è stato raggiunto il budget di spesa del giorno.',
    ],
    [
      'AI_BUDGET_MONTHLY_EXHAUSTED',
      'Il servizio AI è in pausa fino al mese prossimo: è stato raggiunto il budget di spesa del mese.',
    ],
  ])('shows the Italian budget message for errorCode %s', async (errorCode, expected) => {
    const events = [
      sseEvent(EventType.Error, {
        errorMessage: "The AI service is paused: today's AI budget has been reached.",
        errorCode,
      }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const onError = vi.fn();
    const { result } = renderHook(() => useAgentChatStream({ onError }));

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.error).toBe(expected);
    expect(onError).toHaveBeenCalledWith(expected, NOTHING_ARRIVED);
  });

  /**
   * #4168 (5g-b): la domanda fermata dalla quota del piano. Il backend manda un messaggio tecnico
   * («Tier limit exceeded: AgentQuery (3/3)»): il pannello mostra la spiegazione, con i numeri del
   * piano e l'ora in cui il conteggio riparte.
   */
  it('explains the plan quota for errorCode TIER_LIMIT_EXCEEDED', async () => {
    const events = [
      sseEvent(EventType.Error, {
        errorMessage: 'Tier limit exceeded: AgentQuery (3/3)',
        errorCode: 'TIER_LIMIT_EXCEEDED',
        limitType: 'AgentQuery',
        current: 3,
        max: 3,
        upgradeUrl: '/pricing',
      }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const onError = vi.fn();
    const { result } = renderHook(() => useAgentChatStream({ onError }));

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    const explained =
      /^Hai usato tutte le domande di oggi previste dal tuo piano \(3 su 3\)\. Il conteggio riparte alle \d{2}:\d{2} \(mezzanotte UTC\)\.$/;
    expect(result.current.state.error).toMatch(explained);
    expect(onError).toHaveBeenCalledWith(expect.stringMatching(explained), NOTHING_ARRIVED);
    // Chi sa mostrare l'avviso con il link trova i numeri accanto alla frase.
    expect(result.current.state.tierLimit).toEqual({ current: 3, max: 3, upgradeUrl: '/pricing' });
  });

  it('carries no plan quota for another error', async () => {
    const stream = createSSEStream([
      sseEvent(EventType.Error, { errorMessage: 'Agent not found', errorCode: 'AGENT_NOT_FOUND' }),
    ]);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.error).toBe('Agent not found');
    expect(result.current.state.tierLimit).toBeNull();
  });

  /** L'avviso riguarda la domanda appena fermata: con la successiva se ne va. */
  // #4244 (consegna D): le fonti arrivano in un evento a parte, prima dei token. Chi mostra la
  // risposta le riceve insieme a lei, e la domanda dopo riparte senza.
  it('hands the sources of the answer to onComplete, and forgets them with the next question', async () => {
    const coup = {
      text: 'Deal two to each player.',
      source: 'PDF:pdf-coup',
      page: 1,
      line: 0,
      score: 0.9,
    };
    fetchMock.mockResolvedValueOnce({
      ok: true,
      body: createSSEStream([
        sseEvent(EventType.Citations, { citations: [coup] }),
        sseEvent(EventType.Token, { token: 'Due carte.' }),
        sseEvent(EventType.Complete, { totalTokens: 2 }),
      ]),
    });
    const onComplete = vi.fn();
    const { result } = renderHook(() => useAgentChatStream({ onComplete }));

    await act(async () => {
      result.current.sendMessage('game-1', 'Quante carte?');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(onComplete).toHaveBeenLastCalledWith(
      'Due carte.',
      expect.objectContaining({ citations: [coup] })
    );
    expect(result.current.state.citations).toEqual([coup]);

    fetchMock.mockResolvedValueOnce({
      ok: true,
      body: createSSEStream([
        sseEvent(EventType.Token, { token: 'Due monete.' }),
        sseEvent(EventType.Complete, { totalTokens: 2 }),
      ]),
    });
    await act(async () => {
      result.current.sendMessage('game-1', 'E le monete?');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(onComplete).toHaveBeenLastCalledWith(
      'Due monete.',
      expect.objectContaining({ citations: [] })
    );
  });

  // #4244: lo stream cade dopo le fonti e prima dei token, l'hook riprova, e il secondo tentativo
  // risponde senza fonti (per esempio una risposta servita senza recupero). Le fonti del tentativo
  // caduto non sono di questa risposta.
  it('does not hand the sources of a dropped attempt to the answer of the retry', async () => {
    vi.useFakeTimers();
    try {
      const encoder = new TextEncoder();
      let sent = false;
      const dropsAfterTheSources = new ReadableStream<Uint8Array>({
        pull(controller) {
          if (!sent) {
            sent = true;
            controller.enqueue(
              encoder.encode(
                sseEvent(EventType.Citations, {
                  citations: [{ text: 'x', source: 'PDF:pdf-coup', page: 1, line: 0, score: 0.9 }],
                })
              )
            );
          } else {
            controller.error(new TypeError('network error'));
          }
        },
      });
      fetchMock
        .mockResolvedValueOnce({ ok: true, body: dropsAfterTheSources })
        .mockResolvedValueOnce({
          ok: true,
          body: createSSEStream([
            sseEvent(EventType.Token, { token: 'Due carte.' }),
            sseEvent(EventType.Complete, { totalTokens: 2 }),
          ]),
        });
      const onComplete = vi.fn();
      const { result } = renderHook(() => useAgentChatStream({ onComplete }));

      act(() => {
        result.current.sendMessage('game-1', 'Quante carte?');
      });
      await vi.runAllTimersAsync();

      expect(fetchMock).toHaveBeenCalledTimes(2);
      expect(onComplete).toHaveBeenCalledWith(
        'Due carte.',
        expect.objectContaining({ citations: [] })
      );
    } finally {
      vi.useRealTimers();
    }
  });

  // Un evento malformato non deve far cadere chi rende le fonti: si scarta.
  it('drops sources that are not a list', async () => {
    fetchMock.mockResolvedValueOnce({
      ok: true,
      body: createSSEStream([
        sseEvent(EventType.Citations, { citations: 'PDF:pdf-coup' }),
        sseEvent(EventType.Token, { token: 'Due carte.' }),
        sseEvent(EventType.Complete, { totalTokens: 2 }),
      ]),
    });
    const onComplete = vi.fn();
    const { result } = renderHook(() => useAgentChatStream({ onComplete }));

    await act(async () => {
      result.current.sendMessage('game-1', 'Quante carte?');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(onComplete).toHaveBeenCalledWith(
      'Due carte.',
      expect.objectContaining({ citations: [] })
    );
  });

  it('forgets the plan quota with the next question', async () => {
    fetchMock.mockResolvedValueOnce({
      ok: true,
      body: createSSEStream([
        sseEvent(EventType.Error, {
          errorMessage: 'Tier limit exceeded: AgentQuery (3/3)',
          errorCode: 'TIER_LIMIT_EXCEEDED',
          current: 3,
          max: 3,
          upgradeUrl: '/pricing',
        }),
      ]),
    });
    const { result } = renderHook(() => useAgentChatStream());
    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });
    expect(result.current.state.tierLimit).not.toBeNull();

    fetchMock.mockResolvedValueOnce({
      ok: true,
      body: createSSEStream([
        sseEvent(EventType.Token, { token: 'Cinque carte.' }),
        sseEvent(EventType.Complete, { totalTokens: 3 }),
      ]),
    });
    await act(async () => {
      result.current.sendMessage('agent-1', 'E adesso?');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.tierLimit).toBeNull();
    expect(result.current.state.error).toBeNull();
  });

  it('shows Italian provider_unavailable message for errorCode provider_unavailable', async () => {
    const events = [
      sseEvent(EventType.Error, {
        errorMessage: 'Provider temporarily unavailable',
        errorCode: 'provider_unavailable',
      }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const onError = vi.fn();
    const { result } = renderHook(() => useAgentChatStream({ onError }));

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.error).toBe(
      'Il servizio AI è temporaneamente non disponibile. Riprova tra poco.'
    );
    expect(onError).toHaveBeenCalledWith(
      'Il servizio AI è temporaneamente non disponibile. Riprova tra poco.',
      NOTHING_ARRIVED
    );
  });

  it('falls back to errorMessage for unknown errorCode', async () => {
    const events = [
      sseEvent(EventType.Error, {
        errorMessage: 'Agent not found',
        errorCode: 'AGENT_NOT_FOUND',
      }),
    ];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.error).toBe('Agent not found');
  });

  it('uses generic Italian fallback when no errorMessage and unknown errorCode', async () => {
    const events = [sseEvent(EventType.Error, { errorCode: 'SOME_UNKNOWN' })];

    const stream = createSSEStream(events);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.error).toBe('Si è verificato un errore. Riprova.');
  });

  it('resets modelDowngrade on new message send', async () => {
    // First message triggers downgrade
    const events1 = [
      sseEvent(EventType.Token, { token: 'first' }),
      sseEvent(EventType.ModelDowngrade, {
        originalModel: 'model-a',
        fallbackModel: 'model-b',
        reason: 'error',
        isLocalFallback: false,
        upgradeMessage: null,
      }),
      sseEvent(EventType.Complete, { totalTokens: 1 }),
    ];

    const stream1 = createSSEStream(events1);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream1 });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hi');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.modelDowngrade).not.toBeNull();

    // Second message resets state (including modelDowngrade)
    const events2 = [
      sseEvent(EventType.Token, { token: 'second' }),
      sseEvent(EventType.Complete, { totalTokens: 1 }),
    ];
    const stream2 = createSSEStream(events2);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream2 });

    await act(async () => {
      result.current.sendMessage('agent-1', 'Another question');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.modelDowngrade).toBeNull();
    expect(result.current.state.currentAnswer).toBe('second');
  });

  it('retries exactly 2 times on connection error then surfaces final error', async () => {
    vi.useFakeTimers();
    const fetchMock = vi.fn(async () => {
      throw new TypeError('Failed to fetch');
    });
    vi.stubGlobal('fetch', fetchMock);

    const { result } = renderHook(() => useAgentChatStream());
    act(() => {
      result.current.sendMessage('agent-id', 'hi');
    });
    await vi.runAllTimersAsync();

    expect(result.current.state.retryCount).toBe(2);
    // 1 initial + 2 retries = 3 fetch calls
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(result.current.state.error).not.toBeNull();
    vi.useRealTimers();
  });

  /**
   * #4333: chi usa l'hook mette queste funzioni nelle dipendenze di un effetto, e ne chiama una
   * (`stopStreaming`) dal cleanup. Se cambiano a ogni render, il cleanup gira a ogni render e ferma
   * lo stream appena partito.
   */
  describe('the functions it returns (#4333)', () => {
    it('stay the same from one render to the next', () => {
      const { result, rerender } = renderHook(() => useAgentChatStream());
      const first = result.current;

      rerender();

      expect(result.current.sendMessage).toBe(first.sendMessage);
      expect(result.current.stopStreaming).toBe(first.stopStreaming);
      expect(result.current.reset).toBe(first.reset);
    });

    it('stay the same while a stream changes the state', async () => {
      const stream = createSSEStream([
        sseEvent(EventType.Token, { token: 'partial' }),
        sseEvent(EventType.Complete, { totalTokens: 1 }),
      ]);
      fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

      const { result } = renderHook(() => useAgentChatStream());
      const first = result.current;

      await act(async () => {
        result.current.sendMessage('game-1', 'Hi');
        await new Promise(resolve => setTimeout(resolve, 50));
      });

      // Lo stato è cambiato davvero: senza questo, il confronto sotto non direbbe niente.
      expect(result.current.state.currentAnswer).toBe('partial');
      expect(result.current.sendMessage).toBe(first.sendMessage);
      expect(result.current.stopStreaming).toBe(first.stopStreaming);
      expect(result.current.reset).toBe(first.reset);
    });

    /** Chi ha in mano la funzione di un render passato ferma comunque lo stream di adesso. */
    it('stopStreaming taken from an earlier render still stops the stream in flight', async () => {
      let signal: AbortSignal | null | undefined;
      fetchMock.mockImplementationOnce(async (_url: unknown, init?: RequestInit) => {
        signal = init?.signal;
        return { ok: true, body: new ReadableStream<Uint8Array>({ start() {} }) };
      });

      const { result } = renderHook(() => useAgentChatStream());
      const stopFromTheFirstRender = result.current.stopStreaming;

      await act(async () => {
        result.current.sendMessage('game-1', 'Hi');
        await new Promise(resolve => setTimeout(resolve, 20));
      });
      expect(signal?.aborted).toBe(false);
      expect(result.current.state.isStreaming).toBe(true);

      act(() => stopFromTheFirstRender());

      expect(signal?.aborted).toBe(true);
      expect(result.current.state.isStreaming).toBe(false);
    });
  });

  /**
   * #4335: la risposta si chiude senza `Complete` né `Error` — il backend o un proxy chiudono la
   * connessione in modo pulito a metà. Prima la vista restava «in corso» per sempre, senza errore,
   * e nessun callback partiva: il pannello rifiutava ogni domanda successiva senza dire perché.
   *
   * La rete è a passi (`steppedFetch`): fra un evento e la chiusura l'hook rende, come nel browser.
   */
  describe('a stream that closes without Complete or Error (#4335)', () => {
    const INTERRUPTED =
      'La risposta si è interrotta prima della fine, quindi non è completa. Riprova a chiedere.';

    /** Consegna un passo (un evento, o la chiusura) e lascia all'hook il tempo di rendere. */
    async function deliver(next: () => void) {
      await act(async () => {
        next();
        await new Promise(resolve => setTimeout(resolve, 10));
      });
    }

    async function runToTheEnd(wire: ReturnType<typeof steppedFetch>) {
      for (let i = 0; i < wire.steps; i++) await deliver(wire.next);
    }

    it('ends the stream, says the answer was interrupted, and hands over what arrived', async () => {
      const wire = steppedFetch([
        sseEvent(EventType.Citations, {
          citations: [
            { text: 'Cinque tessere.', source: 'PDF:pdf-azul', page: 4, line: 0, score: 1 },
          ],
        }),
        sseEvent(EventType.Token, { token: 'Cinque ' }),
      ]);
      globalThis.fetch = wire.fetch;
      const onComplete = vi.fn();
      const onError = vi.fn();
      const { result } = renderHook(() => useAgentChatStream({ onComplete, onError }));

      act(() => result.current.sendMessage('azul', 'Quante tessere?'));
      await vi.waitFor(() => expect(wire.calls()).toBe(1));
      await runToTheEnd(wire);

      expect(result.current.state.isStreaming).toBe(false);
      expect(result.current.state.connectionStatus).toBe('error');
      expect(result.current.state.statusMessage).toBeNull();
      expect(result.current.state.error).toBe(INTERRUPTED);
      // La parte arrivata non si perde: resta nello stato e passa a chi mostra la risposta.
      expect(result.current.state.currentAnswer).toBe('Cinque ');
      expect(onError).toHaveBeenCalledTimes(1);
      expect(onError).toHaveBeenCalledWith(INTERRUPTED, {
        partialAnswer: 'Cinque ',
        citations: [
          { text: 'Cinque tessere.', source: 'PDF:pdf-azul', page: 4, line: 0, score: 1 },
        ],
      });
      // Non è una risposta completa.
      expect(onComplete).not.toHaveBeenCalled();
      // E non riparte da sola: la domanda la rifà l'utente, se vuole.
      expect(wire.calls()).toBe(1);
    });

    it('says the same when the stream closes before any word of the answer', async () => {
      const wire = steppedFetch([sseEvent(EventType.StateUpdate, { message: 'Cerco...' })]);
      globalThis.fetch = wire.fetch;
      const onError = vi.fn();
      const { result } = renderHook(() => useAgentChatStream({ onError }));

      act(() => result.current.sendMessage('azul', 'Quante tessere?'));
      await vi.waitFor(() => expect(wire.calls()).toBe(1));
      await runToTheEnd(wire);

      expect(result.current.state.isStreaming).toBe(false);
      expect(result.current.state.error).toBe(INTERRUPTED);
      expect(onError).toHaveBeenCalledWith(INTERRUPTED, { partialAnswer: '', citations: [] });
      expect(wire.calls()).toBe(1);
    });

    it('lets the next question through', async () => {
      const first = steppedFetch([sseEvent(EventType.Token, { token: 'Cinque ' })]);
      globalThis.fetch = first.fetch;
      const onComplete = vi.fn();
      const { result } = renderHook(() => useAgentChatStream({ onComplete }));

      act(() => result.current.sendMessage('azul', 'Quante tessere?'));
      await vi.waitFor(() => expect(first.calls()).toBe(1));
      await runToTheEnd(first);
      expect(result.current.state.error).toBe(INTERRUPTED);

      const second = steppedFetch([
        sseEvent(EventType.Token, { token: 'Cinque tessere.' }),
        sseEvent(EventType.Complete, { totalTokens: 2 }),
      ]);
      globalThis.fetch = second.fetch;
      act(() => result.current.sendMessage('azul', 'Quante tessere?'));
      await vi.waitFor(() => expect(second.calls()).toBe(1));
      // La domanda nuova riparte pulita: l'avviso era della risposta di prima.
      expect(result.current.state.error).toBeNull();
      expect(result.current.state.isStreaming).toBe(true);
      await runToTheEnd(second);

      expect(onComplete).toHaveBeenCalledTimes(1);
      expect(onComplete).toHaveBeenCalledWith('Cinque tessere.', expect.anything());
      expect(result.current.state.error).toBeNull();
    });

    /** Il controllo: la chiusura che segue `Complete` è la fine normale, non un'interruzione. */
    it('treats the close that follows Complete as the normal end', async () => {
      const wire = steppedFetch([
        sseEvent(EventType.Token, { token: 'Cinque tessere.' }),
        sseEvent(EventType.Complete, { totalTokens: 2 }),
      ]);
      globalThis.fetch = wire.fetch;
      const onComplete = vi.fn();
      const onError = vi.fn();
      const { result } = renderHook(() => useAgentChatStream({ onComplete, onError }));

      act(() => result.current.sendMessage('azul', 'Quante tessere?'));
      await vi.waitFor(() => expect(wire.calls()).toBe(1));
      await runToTheEnd(wire);

      expect(result.current.state.isStreaming).toBe(false);
      expect(result.current.state.connectionStatus).toBe('idle');
      expect(result.current.state.error).toBeNull();
      expect(onComplete).toHaveBeenCalledTimes(1);
      expect(onError).not.toHaveBeenCalled();
    });

    /** E quella che segue `Error`: l'errore è il suo, non l'interruzione. */
    it('keeps the message of an Error event, and hands over the words that came before it', async () => {
      const wire = steppedFetch([
        sseEvent(EventType.Token, { token: 'Cinque ' }),
        sseEvent(EventType.Error, {
          errorMessage: 'Agent not found',
          errorCode: 'AGENT_NOT_FOUND',
        }),
      ]);
      globalThis.fetch = wire.fetch;
      const onComplete = vi.fn();
      const onError = vi.fn();
      const { result } = renderHook(() => useAgentChatStream({ onComplete, onError }));

      act(() => result.current.sendMessage('azul', 'Quante tessere?'));
      await vi.waitFor(() => expect(wire.calls()).toBe(1));
      await runToTheEnd(wire);

      expect(result.current.state.isStreaming).toBe(false);
      expect(result.current.state.error).toBe('Agent not found');
      expect(onError).toHaveBeenCalledTimes(1);
      expect(onError).toHaveBeenCalledWith('Agent not found', {
        partialAnswer: 'Cinque ',
        citations: [],
      });
      expect(onComplete).not.toHaveBeenCalled();
    });

    /**
     * Review di #4509: dopo `Complete` il backend tiene aperta la connessione per generare le domande
     * successive (fino a 10 s). Se la rete cade lì, la risposta era già completa e consegnata:
     * trattarla da interrotta la consegnava una seconda volta, segnata «Risposta interrotta».
     */
    it('leaves a complete answer alone when the connection drops after Complete', async () => {
      const wire = steppedFetch([
        sseEvent(EventType.Token, { token: 'Cinque tessere.' }),
        sseEvent(EventType.Complete, { totalTokens: 2 }),
        new TypeError('network error'),
      ]);
      globalThis.fetch = wire.fetch;
      const onComplete = vi.fn();
      const onError = vi.fn();
      const { result } = renderHook(() => useAgentChatStream({ onComplete, onError }));

      act(() => result.current.sendMessage('azul', 'Quante tessere?'));
      await vi.waitFor(() => expect(wire.calls()).toBe(1));
      await runToTheEnd(wire);

      expect(onComplete).toHaveBeenCalledTimes(1);
      expect(onComplete).toHaveBeenCalledWith('Cinque tessere.', expect.anything());
      expect(onError).not.toHaveBeenCalled();
      expect(result.current.state.error).toBeNull();
      expect(result.current.state.isStreaming).toBe(false);
      expect(result.current.state.connectionStatus).toBe('idle');
      expect(wire.calls()).toBe(1);
    });

    /** Lo stesso per un `Error` che arriva dopo `Complete`: la risposta resta quella consegnata. */
    it('leaves a complete answer alone when an Error event follows Complete', async () => {
      const wire = steppedFetch([
        sseEvent(EventType.Token, { token: 'Cinque tessere.' }),
        sseEvent(EventType.Complete, { totalTokens: 2 }),
        sseEvent(EventType.Error, {
          errorMessage: 'Follow-up generation failed',
          errorCode: 'SOMETHING_ELSE',
        }),
      ]);
      globalThis.fetch = wire.fetch;
      const onComplete = vi.fn();
      const onError = vi.fn();
      const { result } = renderHook(() => useAgentChatStream({ onComplete, onError }));

      act(() => result.current.sendMessage('azul', 'Quante tessere?'));
      await vi.waitFor(() => expect(wire.calls()).toBe(1));
      await runToTheEnd(wire);

      expect(onComplete).toHaveBeenCalledTimes(1);
      expect(onError).not.toHaveBeenCalled();
      expect(result.current.state.error).toBeNull();
      expect(result.current.state.connectionStatus).toBe('idle');
    });
  });

  /**
   * Review di #4509: i codici che il backend manda in un evento `Error` e che la chat principale
   * traduce già (`qaStreamErrors`) arrivavano qui col messaggio grezzo del backend, in inglese.
   */
  it.each([
    [
      'AI_STREAM_INTERRUPTED',
      'La risposta si è interrotta prima della fine, quindi non è completa. Riprova a chiedere.',
    ],
    [
      'AI_PROVIDER_UNAVAILABLE',
      'Il servizio AI è temporaneamente non disponibile. Riprova tra qualche minuto.',
    ],
  ])('shows the Italian message for errorCode %s', async (errorCode, expected) => {
    const stream = createSSEStream([
      sseEvent(EventType.Error, { errorMessage: 'The LLM stream was interrupted.', errorCode }),
    ]);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });
    const onError = vi.fn();
    const { result } = renderHook(() => useAgentChatStream({ onError }));

    await act(async () => {
      result.current.sendMessage('azul', 'Quante tessere?');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    expect(result.current.state.error).toBe(expected);
    expect(onError).toHaveBeenCalledWith(expected, NOTHING_ARRIVED);
  });

  /**
   * #4335, lo stesso stato bloccato per un'altra strada: un errore LANCIATO che chiude lo stream
   * (la rete che cade a metà, o l'ultimo tentativo fallito) arrivava in `error` ma lasciava
   * `isStreaming` a `true`.
   */
  describe('a thrown error that ends the stream (#4335)', () => {
    // Un'asserzione rossa a metà non deve lasciare l'orologio finto ai test che seguono.
    afterEach(() => {
      vi.useRealTimers();
    });

    it('ends the stream after the last retry fails', async () => {
      vi.useFakeTimers();
      const failing = vi.fn(async () => {
        throw new TypeError('Failed to fetch');
      });
      vi.stubGlobal('fetch', failing);
      const onError = vi.fn();

      const { result } = renderHook(() => useAgentChatStream({ onError }));
      act(() => {
        result.current.sendMessage('azul', 'Quante tessere?');
      });
      await vi.runAllTimersAsync();

      expect(failing).toHaveBeenCalledTimes(3);
      expect(result.current.state.error).not.toBeNull();
      expect(result.current.state.isStreaming).toBe(false);
      expect(result.current.state.connectionStatus).toBe('error');
      expect(onError).toHaveBeenCalledTimes(1);
    });

    it('calls a connection lost after some words an interruption, and keeps the words', async () => {
      const encoder = new TextEncoder();
      let sent = false;
      const body = new ReadableStream<Uint8Array>({
        pull(controller) {
          if (!sent) {
            sent = true;
            controller.enqueue(encoder.encode(sseEvent(EventType.Token, { token: 'Cinque ' })));
            return;
          }
          controller.error(new TypeError('network error'));
        },
      });
      fetchMock.mockResolvedValueOnce({ ok: true, body });
      const onError = vi.fn();

      const { result } = renderHook(() => useAgentChatStream({ onError }));
      await act(async () => {
        result.current.sendMessage('azul', 'Quante tessere?');
        await new Promise(resolve => setTimeout(resolve, 50));
      });

      const interrupted =
        'La risposta si è interrotta prima della fine, quindi non è completa. Riprova a chiedere.';
      expect(result.current.state.isStreaming).toBe(false);
      expect(result.current.state.error).toBe(interrupted);
      expect(onError).toHaveBeenCalledWith(interrupted, {
        partialAnswer: 'Cinque ',
        citations: [],
      });
      // Con delle parole già mostrate non si ritenta: ripartire le duplicherebbe.
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });
  });
});
