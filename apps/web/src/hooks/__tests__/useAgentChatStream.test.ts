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
import { useAgentChatStream } from '../useAgentChatStream';

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
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('initializes with default state', () => {
    const { result } = renderHook(() => useAgentChatStream());

    expect(result.current.state).toEqual({
      statusMessage: null,
      currentAnswer: '',
      followUpQuestions: [],
      isStreaming: false,
      error: null,
      chatThreadId: null,
      totalTokens: 0,
      debugSteps: [],
      modelDowngrade: null,
      strategyTier: null,
      executionId: null,
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

    expect(result.current.state.error).toBe('HTTP 500');
    expect(result.current.state.isStreaming).toBe(false);
    expect(onError).toHaveBeenCalledWith('HTTP 500');
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
    expect(onError).toHaveBeenCalledWith('Agent not found');
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

  it('includes chatThreadId in request body when provided', async () => {
    const stream = createSSEStream([sseEvent(EventType.Complete, { totalTokens: 0 })]);
    fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

    const { result } = renderHook(() => useAgentChatStream());

    await act(async () => {
      result.current.sendMessage('agent-1', 'Hello', 'existing-thread');
      await new Promise(resolve => setTimeout(resolve, 50));
    });

    const fetchCall = fetchMock.mock.calls[0];
    const requestBody = JSON.parse(fetchCall[1].body);
    expect(requestBody).toEqual({ message: 'Hello', chatThreadId: 'existing-thread' });
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
});
