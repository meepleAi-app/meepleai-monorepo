/**
 * kbAskClient — POST /api/v1/knowledge-base/ask/global (SSE).
 *
 * Returns an AsyncIterable<KbAskEvent>. Caller MUST pass an `AbortSignal`
 * and call `controller.abort()` to stop the LLM stream (BE EC-3 — handler
 * stops cleanly on CancellationToken).
 *
 * Wire format: `data: {json}\n\n` (SseJsonOptions.Default, numeric enum).
 *
 * @see apps/web/src/lib/api/schemas/kb-ask.schemas.ts (KbAskEventSchema)
 */

import { KbAskEventSchema, type KbAskEvent, type KbAskRequest } from '../schemas/kb-ask.schemas';

const ENDPOINT = '/api/v1/knowledge-base/ask/global';

async function* parseSseStream(
  response: Response,
  signal: AbortSignal
): AsyncGenerator<KbAskEvent> {
  if (!response.body) throw new Error('SSE response has no body');
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  try {
    while (!signal.aborted) {
      const { value, done } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      // Split on `\n\n` (SSE event boundary). Keep the trailing partial in buffer.
      let boundary: number;
      while ((boundary = buffer.indexOf('\n\n')) !== -1) {
        const rawEvent = buffer.slice(0, boundary);
        buffer = buffer.slice(boundary + 2);
        // Each event is a sequence of `data: ...` lines (BE emits one line).
        const dataLine = rawEvent.split('\n').find(l => l.startsWith('data: '));
        if (!dataLine) continue;
        const json = dataLine.slice('data: '.length);
        try {
          const parsed = KbAskEventSchema.parse(JSON.parse(json));
          yield parsed;
        } catch {
          // Malformed event — skip, continue stream (resilience).
        }
      }
    }
  } finally {
    // Always release the reader lock, even if consumer breaks early.
    try {
      reader.releaseLock();
    } catch {
      /* lock may already be released on abort */
    }
  }
}

export const kbAskClient = {
  /**
   * Stream a cross-game KB question.
   * @param body request body (query + optional language + topK)
   * @param signal MUST be from an AbortController owned by the caller
   * @returns AsyncIterable of parsed KbAskEvent (idle event types are filtered upstream by hook)
   */
  async *askGlobal(body: KbAskRequest, signal: AbortSignal): AsyncGenerator<KbAskEvent> {
    const response = await fetch(ENDPOINT, {
      method: 'POST',
      credentials: 'include',
      headers: { 'content-type': 'application/json', accept: 'text/event-stream' },
      body: JSON.stringify(body),
      signal,
    });
    if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);
    yield* parseSseStream(response, signal);
  },
} as const;
