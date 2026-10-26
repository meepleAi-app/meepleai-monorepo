/**
 * #4388 — `qaStream` davanti al rifiuto 429 delle rotte di risposta.
 *
 * Il backend tiene aperte al massimo due risposte per utente. Le viste che usano `qaStream`
 * (la chat desktop e quella mobile) hanno bisogno di riconoscere quel rifiuto per dirlo in italiano:
 * con `QA stream failed: HTTP 429` potevano solo mostrare il guasto generico.
 */

import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  QA_TOO_MANY_REQUESTS_MESSAGE,
  QaTooManyRequestsError,
} from '@/components/chat-unified/qaStreamErrors';

import { qaStream } from '../chatClient';

async function drain(): Promise<void> {
  for await (const _event of qaStream({ gameId: 'game-1', query: 'Quante carte?' })) {
    /* drain */
  }
}

describe('qaStream — HTTP refusals', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('throws the too-many-requests error for a 429, with the message the views show', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ error: 'Too Many Requests' }), { status: 429 })
    );

    const failure = await drain().then(
      () => null,
      (error: unknown) => error
    );

    expect(failure).toBeInstanceOf(QaTooManyRequestsError);
    expect((failure as Error).message).toBe(QA_TOO_MANY_REQUESTS_MESSAGE);
  });

  it('keeps the generic error, with the status, for any other refusal', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(new Response('boom', { status: 500 }));

    await expect(drain()).rejects.toThrow('QA stream failed: HTTP 500');
  });
});
