/**
 * askSessionAssistant — one turn of the in-game assistant (#4238 A4, spec 2026-10-11 §6.2).
 *
 * `POST /api/v1/live-sessions/{id}/assistant` answers with SSE, but `EventSource` cannot send a
 * body, so the response stream is read here, as `qaStream` does. Each `data:` line is one
 * `AssistantTurnEvent`; a line the schema rejects is skipped, not fatal.
 *
 * The checks of the turn run before the first event. When they fail the response is JSON, not a
 * stream, and this throws the `ApiError` with the server's code (`assistant_disabled`,
 * `session_state_invalid`, …) in `code`.
 */

import { createApiError } from '@/lib/api/core/errors';
import { getApiBase } from '@/lib/api/core/httpClient';
import {
  AssistantTurnEventSchema,
  type AssistantTurnEvent,
} from '@/lib/api/schemas/live-sessions.schemas';

export async function* askSessionAssistant(
  sessionId: string,
  question: string,
  language: string | undefined,
  signal?: AbortSignal
): AsyncGenerator<AssistantTurnEvent> {
  const endpoint = `/api/v1/live-sessions/${encodeURIComponent(sessionId)}/assistant`;
  const response = await fetch(`${getApiBase()}${endpoint}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ question, language }),
    credentials: 'include',
    signal,
  });

  if (!response.ok) {
    throw await createApiError(endpoint, response);
  }

  const reader = response.body?.getReader();
  if (!reader) throw new Error('The assistant answered without a body');

  const decoder = new TextDecoder();
  let buffer = '';
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      const frames = buffer.split('\n\n');
      buffer = frames.pop() ?? '';
      for (const frame of frames) {
        const event = parseFrame(frame);
        if (event) yield event;
      }
    }

    const last = parseFrame(buffer);
    if (last) yield last;
  } finally {
    reader.releaseLock();
  }
}

function parseFrame(frame: string): AssistantTurnEvent | null {
  const data = frame
    .split('\n')
    .filter(line => line.startsWith('data:'))
    .map(line => line.slice(5).trim())
    .join('\n');
  if (!data) return null;

  try {
    const parsed = AssistantTurnEventSchema.safeParse(JSON.parse(data));
    return parsed.success ? parsed.data : null;
  } catch {
    return null;
  }
}
