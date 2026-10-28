/**
 * Una mutazione gia' eseguita non si rispedisce perche' la lettura della sua risposta e' caduta.
 *
 * `isRetryableError` considera ritentabile ogni `TypeError` il cui messaggio contiene "fetch", e il
 * `catch` interno di ogni verbo lo converte in `NetworkError`. `post`/`put`/`patch` leggono il corpo
 * (`response.json()`) DENTRO la callback di `withRetry`: un corpo troncato che solleva
 * `TypeError: Failed to fetch` e' quindi letto come errore di rete e la richiesta parte di nuovo, anche
 * se il server l'ha gia' eseguita (un POST di creazione darebbe un duplicato o un conflitto).
 * `sendDelete` lo evita dal #4683; qui si misura il resto.
 *
 * Client REALE con `fetchImpl` iniettato, backoff accorciato a 1 ms (senza, ogni caso aspetta ~7 s).
 * Ogni test ha il suo path: il circuit breaker tiene lo stato per path a livello di modulo.
 */

import { describe, it, expect, vi } from 'vitest';

import { HttpClient } from '../httpClient';

/** Una risposta 200 il cui corpo cade a meta' lettura, come un trasferimento interrotto. */
function truncatedBodyResponse(): Response {
  return {
    ok: true,
    status: 200,
    json: () => Promise.reject(new TypeError('Failed to fetch')),
  } as unknown as Response;
}

const FAST_RETRY = { retry: { retryConfig: { baseDelay: 1, maxDelay: 2, jitter: 0 } } };

function fetchReturningTruncatedBody() {
  return vi
    .fn()
    .mockImplementation(async () => truncatedBodyResponse()) as unknown as typeof fetch &
    ReturnType<typeof vi.fn>;
}

function clientWith(fetchImpl: typeof fetch) {
  return new HttpClient({ baseUrl: 'http://test.local', fetchImpl });
}

describe('lettura del corpo e retry', () => {
  it.each([
    ['post', (c: HttpClient, p: string) => c.post(p, { name: 'x' }, undefined, FAST_RETRY)],
    ['put', (c: HttpClient, p: string) => c.put(p, { name: 'x' }, undefined, FAST_RETRY)],
    ['patch', (c: HttpClient, p: string) => c.patch(p, { name: 'x' }, undefined, FAST_RETRY)],
  ] as const)(
    '%s non rispedisce la richiesta se la lettura del corpo cade con un TypeError "fetch"',
    async (verb, call) => {
      const fetchImpl = fetchReturningTruncatedBody();
      const path = `/api/v1/probe/${verb}`;

      const error = await call(clientWith(fetchImpl), path).then(
        () => undefined,
        (e: unknown) => e
      );

      expect(fetchImpl).toHaveBeenCalledTimes(1);
      // L'errore dice quale richiesta e' stata eseguita ma non letta, e conserva la causa originale.
      expect(error).toBeInstanceOf(Error);
      expect((error as Error).message).toContain(`${verb.toUpperCase()} ${path}`);
      expect((error as Error).cause).toBeInstanceOf(TypeError);
    }
  );

  it.each([
    [
      "SyntaxError (corpo che non e' JSON)",
      () => new SyntaxError('Unexpected token < in JSON'),
      SyntaxError,
    ],
    [
      'AbortError (richiesta annullata)',
      () => new DOMException('aborted', 'AbortError'),
      DOMException,
    ],
  ] as const)(
    "un %s resta del suo tipo: solo il TypeError e' riscritto",
    async (_label, makeError, errorType) => {
      // Non erano ritentabili, quindi non serve mascherarli: chi riconosce un AbortError o un SyntaxError
      // dal tipo continua a poterlo fare.
      const fetchImpl = vi.fn().mockImplementation(async () => ({
        ok: true,
        status: 200,
        json: () => Promise.reject(makeError()),
      })) as unknown as typeof fetch & ReturnType<typeof vi.fn>;

      const error = await clientWith(fetchImpl)
        .post('/api/v1/probe/other-error', { name: 'x' }, undefined, FAST_RETRY)
        .then(
          () => undefined,
          (e: unknown) => e
        );

      expect(error).toBeInstanceOf(errorType);
      expect(fetchImpl).toHaveBeenCalledTimes(1);
    }
  );

  it("get invece continua a ritentare: e' idempotente, e un corpo troncato si recupera riprovando", async () => {
    // Controllo: prova che la sonda SA vedere un retry quando c'e'. Non e' un comportamento da cambiare.
    const fetchImpl = fetchReturningTruncatedBody();

    await expect(
      clientWith(fetchImpl).get('/api/v1/probe/get', undefined, FAST_RETRY)
    ).rejects.toThrow();

    expect(fetchImpl.mock.calls.length).toBeGreaterThan(1);
  });
});
