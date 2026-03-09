/**
 * OpenRouter Chat Proxy Route Tests
 * Issue #4779: SSE streaming proxy
 *
 * Tests:
 * - Input validation (missing fields, message length)
 * - SSE output format (Token, Complete, Error, Heartbeat)
 * - System prompt construction per typology
 * - Rate limiting
 * - Error handling (missing API key, OpenRouter errors)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { NextRequest } from 'next/server';
import { POST, _resetRateLimitForTesting } from '../route';

// StreamingEventType enum (matching backend)
const EventType = {
  StateUpdate: 0,
  Complete: 4,
  Error: 5,
  Heartbeat: 6,
  Token: 7,
} as const;

// ─── Helpers ─────────────────────────────────────────────

function createRequest(body: unknown, headers?: Record<string, string>): NextRequest {
  return new NextRequest('http://localhost:3000/api/chat-proxy', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...headers,
    },
    body: JSON.stringify(body),
  });
}

function createValidBody(
  overrides?: Partial<{
    message: string;
    agentId: string;
    threadId: string;
    gameContext: { gameName: string; agentTypology: string; ragContext?: string };
    modelId: string;
    maxTokens: number;
    temperature: number;
  }>
) {
  return {
    message: 'How do I play Catan?',
    agentId: 'agent-123',
    gameContext: {
      gameName: 'Catan',
      agentTypology: 'Tutor',
    },
    ...overrides,
  };
}

/** Read all SSE events from a streaming Response */
async function readSSEEvents(
  response: Response
): Promise<Array<{ type: number; data: unknown; timestamp: string }>> {
  const events: Array<{ type: number; data: unknown; timestamp: string }> = [];
  const reader = response.body?.getReader();
  if (!reader) return events;

  const decoder = new TextDecoder();
  let buffer = '';

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;

    buffer += decoder.decode(value, { stream: true });
    const parts = buffer.split('\n\n');
    buffer = parts.pop() || '';

    for (const part of parts) {
      const match = part.match(/data:\s*([\s\S]+)/);
      if (match) {
        try {
          events.push(JSON.parse(match[1]));
        } catch {
          // skip malformed
        }
      }
    }
  }

  return events;
}

/** Create a mock OpenRouter SSE stream */
function createOpenRouterStream(chunks: string[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder();
  let index = 0;
  return new ReadableStream({
    pull(controller) {
      if (index < chunks.length) {
        controller.enqueue(encoder.encode(chunks[index]));
        index++;
      } else {
        controller.close();
      }
    },
  });
}

function openRouterChunk(content: string): string {
  return `data: ${JSON.stringify({
    choices: [{ delta: { content } }],
  })}\n\n`;
}

function openRouterDone(): string {
  return 'data: [DONE]\n\n';
}

/** Setup fetch mock to return OpenRouter stream, consume response, return both events and fetch args */
async function callProxyAndConsume(
  fetchMock: ReturnType<typeof vi.fn>,
  body: ReturnType<typeof createValidBody>,
  openRouterChunks: string[] = [openRouterDone()],
  headers?: Record<string, string>
) {
  const stream = createOpenRouterStream(openRouterChunks);
  fetchMock.mockResolvedValueOnce({ ok: true, body: stream });

  const req = createRequest(body, headers);
  const res = await POST(req);
  // Consume the SSE stream to completion (this forces the internal fetch to OpenRouter to resolve)
  const events = await readSSEEvents(res);

  return { res, events, fetchCall: fetchMock.mock.calls[0] };
}

// ─── Tests ───────────────────────────────────────────────

describe('POST /api/chat-proxy', () => {
  let fetchMock: ReturnType<typeof vi.fn>;
  let originalEnv: NodeJS.ProcessEnv;

  beforeEach(() => {
    fetchMock = vi.fn();
    globalThis.fetch = fetchMock;
    originalEnv = { ...process.env };
    process.env.OPENROUTER_API_KEY = 'sk-or-test-key';
    process.env.OPENROUTER_DEFAULT_MODEL = 'test-model/free';
    _resetRateLimitForTesting();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    process.env = originalEnv;
  });

  describe('Input Validation', () => {
    it('returns 400 for missing message', async () => {
      const req = createRequest({
        agentId: 'a',
        gameContext: { gameName: 'X', agentTypology: 'Tutor' },
      });
      const res = await POST(req);

      expect(res.status).toBe(400);
      const json = await res.json();
      expect(json.error).toContain('message');
    });

    it('returns 400 for message exceeding 2000 chars', async () => {
      const req = createRequest(createValidBody({ message: 'x'.repeat(2001) }));
      const res = await POST(req);

      expect(res.status).toBe(400);
      const json = await res.json();
      expect(json.error).toContain('2000');
    });

    it('returns 400 for missing agentId', async () => {
      const req = createRequest({
        message: 'hi',
        gameContext: { gameName: 'X', agentTypology: 'Tutor' },
      });
      const res = await POST(req);

      expect(res.status).toBe(400);
      const json = await res.json();
      expect(json.error).toContain('agentId');
    });

    it('returns 400 for missing gameContext', async () => {
      const req = createRequest({ message: 'hi', agentId: 'a' });
      const res = await POST(req);

      expect(res.status).toBe(400);
      const json = await res.json();
      expect(json.error).toContain('gameContext');
    });

    it('returns 400 for invalid JSON body', async () => {
      const req = new NextRequest('http://localhost:3000/api/chat-proxy', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: 'not-json',
      });
      const res = await POST(req);

      expect(res.status).toBe(400);
      const json = await res.json();
      expect(json.error).toContain('Invalid JSON');
    });
  });

  describe('API Key', () => {
    it('returns 503 when OPENROUTER_API_KEY is not set', async () => {
      delete process.env.OPENROUTER_API_KEY;
      const req = createRequest(createValidBody());
      const res = await POST(req);

      expect(res.status).toBe(503);
      const json = await res.json();
      expect(json.error).toContain('API key');
    });
  });

  describe('SSE Streaming', () => {
    it('sends Token events for OpenRouter chunks', async () => {
      const chunks = [openRouterChunk('Hello'), openRouterChunk(' world'), openRouterDone()];

      const { res, events } = await callProxyAndConsume(fetchMock, createValidBody(), chunks);

      expect(res.status).toBe(200);
      expect(res.headers.get('content-type')).toBe('text/event-stream');

      const tokenEvents = events.filter(e => e.type === EventType.Token);
      expect(tokenEvents).toHaveLength(2);
      expect((tokenEvents[0].data as { token: string }).token).toBe('Hello');
      expect((tokenEvents[1].data as { token: string }).token).toBe(' world');

      const completeEvents = events.filter(e => e.type === EventType.Complete);
      expect(completeEvents).toHaveLength(1);
    });

    it('sends StateUpdate event at stream start', async () => {
      const { events } = await callProxyAndConsume(fetchMock, createValidBody());

      const stateEvents = events.filter(e => e.type === EventType.StateUpdate);
      expect(stateEvents).toHaveLength(1);
      expect((stateEvents[0].data as { message: string }).message).toContain('Connecting');
    });

    it('sends Complete event with token count', async () => {
      const chunks = [
        openRouterChunk('A'),
        openRouterChunk('B'),
        openRouterChunk('C'),
        openRouterDone(),
      ];

      const { events } = await callProxyAndConsume(fetchMock, createValidBody(), chunks);

      const complete = events.find(e => e.type === EventType.Complete);
      expect(complete).toBeDefined();
      expect((complete!.data as { totalTokens: number }).totalTokens).toBe(3);
    });

    it('includes threadId in Complete event when provided', async () => {
      const { events } = await callProxyAndConsume(
        fetchMock,
        createValidBody({ threadId: 'thread-456' })
      );

      const complete = events.find(e => e.type === EventType.Complete);
      expect((complete!.data as { chatThreadId: string }).chatThreadId).toBe('thread-456');
    });

    it('includes timestamps in all events', async () => {
      const chunks = [openRouterChunk('Hi'), openRouterDone()];
      const { events } = await callProxyAndConsume(fetchMock, createValidBody(), chunks);

      for (const event of events) {
        expect(event.timestamp).toBeDefined();
        expect(new Date(event.timestamp).getTime()).not.toBeNaN();
      }
    });
  });

  describe('OpenRouter Error Handling', () => {
    it('sends Error event when OpenRouter returns non-200', async () => {
      fetchMock.mockResolvedValueOnce({
        ok: false,
        status: 401,
        text: () => Promise.resolve('Invalid API key'),
      });

      const req = createRequest(createValidBody());
      const res = await POST(req);
      const events = await readSSEEvents(res);

      const errorEvents = events.filter(e => e.type === EventType.Error);
      expect(errorEvents).toHaveLength(1);
      expect((errorEvents[0].data as { errorMessage: string }).errorMessage).toContain('401');
      expect((errorEvents[0].data as { errorCode: string }).errorCode).toBe('MODEL_ERROR');
    });

    it('sends Error event when OpenRouter has no body', async () => {
      fetchMock.mockResolvedValueOnce({
        ok: true,
        body: null,
      });

      const req = createRequest(createValidBody());
      const res = await POST(req);
      const events = await readSSEEvents(res);

      const errorEvents = events.filter(e => e.type === EventType.Error);
      expect(errorEvents).toHaveLength(1);
      expect((errorEvents[0].data as { errorCode: string }).errorCode).toBe('NO_STREAM');
    });

    it('handles fetch failures gracefully', async () => {
      fetchMock.mockRejectedValueOnce(new Error('Network error'));

      const req = createRequest(createValidBody());
      const res = await POST(req);
      const events = await readSSEEvents(res);

      const errorEvents = events.filter(e => e.type === EventType.Error);
      expect(errorEvents).toHaveLength(1);
      expect((errorEvents[0].data as { errorMessage: string }).errorMessage).toContain(
        'Network error'
      );
    });

    it('skips malformed OpenRouter chunks', async () => {
      const chunks = ['data: {invalid json}\n\n', openRouterChunk('ok'), openRouterDone()];

      const { events } = await callProxyAndConsume(fetchMock, createValidBody(), chunks);

      const tokenEvents = events.filter(e => e.type === EventType.Token);
      expect(tokenEvents).toHaveLength(1);
      expect((tokenEvents[0].data as { token: string }).token).toBe('ok');
    });
  });

  describe('System Prompt', () => {
    it('sends correct system prompt for Tutor typology', async () => {
      const { fetchCall } = await callProxyAndConsume(
        fetchMock,
        createValidBody({
          gameContext: { gameName: 'Catan', agentTypology: 'Tutor' },
        })
      );

      const requestBody = JSON.parse(fetchCall[1].body);
      expect(requestBody.messages[0].role).toBe('system');
      expect(requestBody.messages[0].content).toContain('Tutor');
      expect(requestBody.messages[0].content).toContain('Catan');
      expect(requestBody.messages[0].content).toContain('italiano');
    });

    it('sends correct system prompt for Arbitro typology', async () => {
      const { fetchCall } = await callProxyAndConsume(
        fetchMock,
        createValidBody({
          gameContext: { gameName: 'Terraforming Mars', agentTypology: 'Arbitro' },
        })
      );

      const requestBody = JSON.parse(fetchCall[1].body);
      expect(requestBody.messages[0].content).toContain('Arbitro');
      expect(requestBody.messages[0].content).toContain('Terraforming Mars');
    });

    it('includes RAG context in system prompt when provided', async () => {
      const { fetchCall } = await callProxyAndConsume(
        fetchMock,
        createValidBody({
          gameContext: {
            gameName: 'Catan',
            agentTypology: 'Tutor',
            ragContext: 'Each player starts with 2 settlements and 2 roads.',
          },
        })
      );

      const requestBody = JSON.parse(fetchCall[1].body);
      expect(requestBody.messages[0].content).toContain('2 settlements and 2 roads');
    });

    it('uses default prompt for unknown typology', async () => {
      const { fetchCall } = await callProxyAndConsume(
        fetchMock,
        createValidBody({
          gameContext: { gameName: 'Chess', agentTypology: 'UnknownType' },
        })
      );

      const requestBody = JSON.parse(fetchCall[1].body);
      expect(requestBody.messages[0].content).toContain('assistente esperto');
      expect(requestBody.messages[0].content).toContain('Chess');
    });
  });

  describe('OpenRouter Request Format', () => {
    it('sends correct model and parameters to OpenRouter', async () => {
      const { fetchCall } = await callProxyAndConsume(
        fetchMock,
        createValidBody({
          modelId: 'anthropic/claude-3.5-sonnet',
        })
      );

      expect(fetchCall[0]).toBe('https://openrouter.ai/api/v1/chat/completions');
      const requestBody = JSON.parse(fetchCall[1].body);
      expect(requestBody.model).toBe('anthropic/claude-3.5-sonnet');
      expect(requestBody.stream).toBe(true);
      expect(requestBody.max_tokens).toBe(1024);
      expect(requestBody.temperature).toBe(0.5); // Tutor typology default
    });

    it('uses default model when modelId not provided', async () => {
      const { fetchCall } = await callProxyAndConsume(fetchMock, createValidBody());

      const requestBody = JSON.parse(fetchCall[1].body);
      // DEFAULT_MODEL is resolved at module load from env or fallback
      expect(requestBody.model).toMatch(/^[\w-]+\/[\w.:/-]+$/);
      expect(requestBody.model).toBeTruthy();
    });

    it('includes Authorization header with API key', async () => {
      const { fetchCall } = await callProxyAndConsume(fetchMock, createValidBody());

      expect(fetchCall[1].headers.Authorization).toBe('Bearer sk-or-test-key');
    });

    it('clamps max_tokens to 4096', async () => {
      const { fetchCall } = await callProxyAndConsume(fetchMock, {
        ...createValidBody(),
        maxTokens: 99999,
      });

      const requestBody = JSON.parse(fetchCall[1].body);
      expect(requestBody.max_tokens).toBe(4096);
    });

    it('clamps temperature between 0 and 2', async () => {
      const { fetchCall } = await callProxyAndConsume(fetchMock, {
        ...createValidBody(),
        temperature: 5,
      });

      const requestBody = JSON.parse(fetchCall[1].body);
      expect(requestBody.temperature).toBe(2);
    });

    it('includes user message in messages array', async () => {
      const { fetchCall } = await callProxyAndConsume(
        fetchMock,
        createValidBody({
          message: 'How to win at Catan?',
        })
      );

      const requestBody = JSON.parse(fetchCall[1].body);
      expect(requestBody.messages).toHaveLength(2);
      expect(requestBody.messages[1]).toEqual({ role: 'user', content: 'How to win at Catan?' });
    });
  });

  describe('Response Headers', () => {
    it('sets correct SSE headers', async () => {
      const { res } = await callProxyAndConsume(fetchMock, createValidBody());

      expect(res.headers.get('content-type')).toBe('text/event-stream');
      expect(res.headers.get('cache-control')).toBe('no-cache, no-transform');
      expect(res.headers.get('connection')).toBe('keep-alive');
    });
  });

  describe('Rate Limiting', () => {
    it('returns 429 after exceeding rate limit', async () => {
      // Make 10 requests (the limit)
      for (let i = 0; i < 10; i++) {
        const stream = createOpenRouterStream([openRouterDone()]);
        fetchMock.mockResolvedValueOnce({ ok: true, body: stream });
        const req = createRequest(createValidBody());
        const res = await POST(req);
        await readSSEEvents(res);
      }

      // 11th request should be rate limited
      const req = createRequest(createValidBody());
      const res = await POST(req);

      expect(res.status).toBe(429);
      const json = await res.json();
      expect(json.error).toContain('Rate limit');
    });
  });
});
