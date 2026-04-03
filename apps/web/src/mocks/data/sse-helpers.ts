/**
 * SSE event builder helpers — browser-safe subset of sse-test-helpers
 *
 * Used by chat.handlers.ts to build streaming mock responses without
 * importing from the test-only @/__tests__/ path.
 */

export interface SSEOptions {
  eventDelay?: number;
  signal?: AbortSignal;
}

export function createSSEResponse(events: string[], options: SSEOptions = {}): Response {
  const { eventDelay = 10, signal } = options;

  async function* generateEvents() {
    const encoder = new TextEncoder();
    for (let i = 0; i < events.length; i++) {
      if (signal?.aborted) break;
      if (i > 0 && eventDelay > 0) {
        await new Promise(resolve => setTimeout(resolve, eventDelay));
      }
      yield encoder.encode(`data: ${events[i]}\n\n`);
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const stream = (ReadableStream as any).from(generateEvents());
  return new Response(stream, {
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' },
  });
}

export function createTokenEvent(token: string, timestamp?: string): string {
  return JSON.stringify({
    type: 'token',
    data: { token },
    timestamp: timestamp ?? new Date().toISOString(),
  });
}

export function createStateUpdateEvent(state: string, timestamp?: string): string {
  return JSON.stringify({
    type: 'stateUpdate',
    data: { state },
    timestamp: timestamp ?? new Date().toISOString(),
  });
}

export function createCompleteEvent(
  totalTokens: number,
  confidence: number | null,
  snippets?: Array<{ source: string; page: number; text: string; score: number }>,
  timestamp?: string
): string {
  return JSON.stringify({
    type: 'complete',
    data: {
      totalTokens,
      confidence,
      ...(snippets ? { snippets } : {}),
    },
    timestamp: timestamp ?? new Date().toISOString(),
  });
}
