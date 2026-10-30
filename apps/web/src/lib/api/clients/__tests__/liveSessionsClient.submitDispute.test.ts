/**
 * liveSessionsClient.submitDispute — #4751.
 *
 * Every attempt is a paid model call. The server answers 503 when the arbitro gave no verdict (or the
 * AI gateway refused the call), and the HTTP client retries 502/503 by default: one click would cost
 * several model calls and a minute of spinner before the message arrives.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { HttpClient } from '../../core/httpClient';
import { createLiveSessionsClient } from '../liveSessionsClient';

const SESSION_ID = '11111111-1111-4111-8111-111111111111';
const NO_VERDICT = "L'Arbitro non è riuscito a emettere un verdetto. Riprova tra poco.";

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

describe('liveSessionsClient.submitDispute — #4751', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('asks the model once when the server answers 503, and shows the server message', async () => {
    const fetchImpl = vi.fn(async () =>
      json(503, { error: 'arbitro_nessun_verdetto', message: NO_VERDICT })
    );
    const client = createLiveSessionsClient({
      httpClient: new HttpClient({ baseUrl: 'http://localhost', fetchImpl: fetchImpl as never }),
    });

    const outcome = client.submitDispute(SESSION_ID, 'Chi ha ragione?', 'Marco').then(
      () => null,
      (e: unknown) => e as Error
    );
    // Long enough for every backoff of the default policy: if a retry were scheduled, it would run here.
    await vi.advanceTimersByTimeAsync(60_000);
    const error = await outcome;

    expect(fetchImpl).toHaveBeenCalledTimes(1);
    expect(error?.message).toBe(NO_VERDICT);
  });

  it('still returns the verdict when the server gives one', async () => {
    const fetchImpl = vi.fn(async () =>
      json(200, {
        id: '22222222-2222-4222-8222-222222222222',
        verdict: 'Ha ragione Sara.',
        ruleReferences: ['Manuale pag. 12'],
        note: null,
      })
    );
    const client = createLiveSessionsClient({
      httpClient: new HttpClient({ baseUrl: 'http://localhost', fetchImpl: fetchImpl as never }),
    });

    const result = await client.submitDispute(SESSION_ID, 'Chi ha ragione?', 'Marco');

    expect(fetchImpl).toHaveBeenCalledTimes(1);
    expect(result.verdict).toBe('Ha ragione Sara.');
  });
});
