import { describe, it, expect, vi, beforeEach } from 'vitest';
import { kbAskClient } from '../kbAskClient';
import type { KbAskEvent } from '../../schemas/kb-ask.schemas';

function buildSseStream(events: KbAskEvent[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder();
  return new ReadableStream({
    start(controller) {
      for (const evt of events) {
        controller.enqueue(encoder.encode(`data: ${JSON.stringify(evt)}\n\n`));
      }
      controller.close();
    },
  });
}

describe('kbAskClient.askGlobal', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('yields parsed events from SSE stream in order', async () => {
    const events: KbAskEvent[] = [
      { type: 0, data: { message: 'Ricerca…' } },
      {
        type: 1,
        data: { citations: [{ docId: 'd', source: 'd', page: 14, snippet: 't', score: 0.9 }] },
      },
      { type: 7, data: { token: 'La ' } },
      { type: 7, data: { token: 'classe.' } },
      {
        type: 4,
        data: {
          totalTokens: 2,
          promptTokens: 10,
          completionTokens: 2,
          estimatedReadingTimeMinutes: 0,
          confidence: null,
        },
      },
    ];
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(buildSseStream(events), {
        status: 200,
        headers: { 'content-type': 'text/event-stream' },
      })
    );

    const received: KbAskEvent[] = [];
    for await (const evt of kbAskClient.askGlobal(
      { query: 'scout abilities' },
      new AbortController().signal
    )) {
      received.push(evt);
    }

    expect(received).toEqual(events);
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/knowledge-base/ask/global'),
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('throws on non-200 HTTP status', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(new Response('Unauthorized', { status: 401 }));

    await expect(async () => {
      for await (const _ of kbAskClient.askGlobal({ query: 'q' }, new AbortController().signal)) {
        /* drain */
      }
    }).rejects.toThrow(/HTTP 401/);
  });

  it('handles split events across chunk boundaries (partial line buffering)', async () => {
    const encoder = new TextEncoder();
    const stream = new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(encoder.encode('data: {"type":0,'));
        controller.enqueue(encoder.encode('"data":{"message":"ok"}}\n\n'));
        controller.close();
      },
    });
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(stream, { status: 200, headers: { 'content-type': 'text/event-stream' } })
    );

    const received: KbAskEvent[] = [];
    for await (const evt of kbAskClient.askGlobal({ query: 'q' }, new AbortController().signal)) {
      received.push(evt);
    }
    expect(received).toEqual([{ type: 0, data: { message: 'ok' } }]);
  });

  it('aborts cleanly when signal triggers', async () => {
    const controller = new AbortController();
    const encoder = new TextEncoder();
    const stream = new ReadableStream<Uint8Array>({
      start(c) {
        c.enqueue(encoder.encode(`data: ${JSON.stringify({ type: 7, data: { token: 'a' } })}\n\n`));
        // Never closes — would hang without abort
      },
    });
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(stream, { status: 200, headers: { 'content-type': 'text/event-stream' } })
    );

    const received: KbAskEvent[] = [];
    const consume = (async () => {
      for await (const evt of kbAskClient.askGlobal({ query: 'q' }, controller.signal)) {
        received.push(evt);
        controller.abort();
      }
    })();
    await consume;
    expect(received).toHaveLength(1);
  });
});
