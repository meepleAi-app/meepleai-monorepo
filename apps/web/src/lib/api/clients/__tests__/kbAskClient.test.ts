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

  /**
   * #4168: il filo vero di /ask/global, nella forma dei record C# serializzati dal backend
   * (Snippet: text/source/page/line/score/chunkId/chunkPosition; StreamingError:
   * errorMessage/errorCode). Lo schema chiedeva docId/snippet e message/code, quindi questi due
   * eventi fallivano la validazione e il client li scartava in silenzio: la pagina della KB
   * globale non mostrava mai né le citazioni né il motivo di un errore.
   */
  it('accepts the citations and the error exactly as the backend serializes them', async () => {
    const encoder = new TextEncoder();
    const wire = [
      {
        type: 1,
        data: {
          citations: [
            {
              text: 'The Scout class begins with three signature abilities.',
              source: '550e8400-e29b-41d4-a716-446655440000',
              page: 14,
              line: 0,
              score: 0.42,
              chunkId: '550e8400-e29b-41d4-a716-446655440000_3',
              chunkPosition: 3,
            },
          ],
        },
        timestamp: '2026-10-12T10:00:00Z',
      },
      {
        type: 5,
        data: {
          errorMessage: 'Il servizio AI è in pausa.',
          errorCode: 'AI_BUDGET_DAILY_EXHAUSTED',
        },
        timestamp: '2026-10-12T10:00:01Z',
      },
    ];
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(
        new ReadableStream<Uint8Array>({
          start(controller) {
            for (const evt of wire)
              controller.enqueue(
                encoder.encode(`data: ${JSON.stringify(evt)}

`)
              );
            controller.close();
          },
        }),
        { status: 200, headers: { 'content-type': 'text/event-stream' } }
      )
    );

    const received: KbAskEvent[] = [];
    for await (const evt of kbAskClient.askGlobal({ query: 'q' }, new AbortController().signal)) {
      received.push(evt);
    }

    expect(received.map(e => e.type)).toEqual([1, 5]);
    expect(received[0]).toMatchObject({
      type: 1,
      data: {
        citations: [
          {
            docId: '550e8400-e29b-41d4-a716-446655440000',
            page: 14,
            snippet: 'The Scout class begins with three signature abilities.',
            score: 0.42,
            chunkId: '550e8400-e29b-41d4-a716-446655440000_3',
            chunkPosition: 3,
          },
        ],
      },
    });
    expect(received[1]).toMatchObject({
      type: 5,
      data: { message: 'Il servizio AI è in pausa.', code: 'AI_BUDGET_DAILY_EXHAUSTED' },
    });
  });
});
