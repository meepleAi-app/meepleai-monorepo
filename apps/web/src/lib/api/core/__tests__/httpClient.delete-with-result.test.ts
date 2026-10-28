/**
 * #4683 — un DELETE puo' rispondere 200 con un corpo che dice cosa e' successo.
 *
 * `DELETE /api/v1/pdf/{id}` risponde 204 solo se il PDF e' stato cancellato; se lo usano altri giochi
 * risponde 200 `{ outcome: 'unlinked' | 'kept', ... }`. `HttpClient.delete` scarta il corpo di ogni
 * 2xx, quindi la UI non poteva sapere che il file era rimasto. `deleteWithResult` e' la variante che
 * lo legge; `delete` resta com'era.
 *
 * Come in `httpClient.no-content.test.ts`: client REALE con `fetchImpl` iniettato. Ogni test usa un
 * path suo, perche' il circuit breaker tiene lo stato per path a livello di modulo.
 */

import { describe, it, expect, vi } from 'vitest';
import { z } from 'zod';

import { SchemaValidationError } from '../errors';
import { HttpClient } from '../httpClient';

const OutcomeSchema = z.object({
  outcome: z.enum(['unlinked', 'kept']),
  message: z.string(),
});

function noContentResponse(): Response {
  return new Response(null, { status: 204, statusText: 'No Content' });
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function clientWith(fetchImpl: typeof fetch) {
  return new HttpClient({ baseUrl: 'http://test.local', fetchImpl });
}

describe('HttpClient.deleteWithResult', () => {
  it('restituisce il corpo di un 200 e manda davvero un DELETE', async () => {
    const fetchImpl = vi
      .fn()
      .mockResolvedValue(
        jsonResponse({ outcome: 'kept', message: 'in uso' })
      ) as unknown as typeof fetch;

    const result = await clientWith(fetchImpl).deleteWithResult(
      '/api/v1/pdf/with-body',
      OutcomeSchema
    );

    expect(result).toEqual({ outcome: 'kept', message: 'in uso' });
    expect(fetchImpl).toHaveBeenCalledTimes(1);
    const [url, init] = (fetchImpl as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(url).toBe('http://test.local/api/v1/pdf/with-body');
    expect(init.method).toBe('DELETE');
  });

  it("restituisce null su 204, senza provare a leggere un corpo che non c'e'", async () => {
    const fetchImpl = vi.fn().mockResolvedValue(noContentResponse()) as unknown as typeof fetch;

    await expect(
      clientWith(fetchImpl).deleteWithResult('/api/v1/pdf/no-content', OutcomeSchema)
    ).resolves.toBeNull();
  });

  it("senza schema restituisce il corpo cosi' com'e'", async () => {
    const fetchImpl = vi
      .fn()
      .mockResolvedValue(jsonResponse({ qualunque: 'cosa' })) as unknown as typeof fetch;

    await expect(
      clientWith(fetchImpl).deleteWithResult<{ qualunque: string }>('/api/v1/pdf/no-schema')
    ).resolves.toEqual({ qualunque: 'cosa' });
  });

  it('rifiuta un corpo che non rispetta lo schema, e non ripete il DELETE', async () => {
    // Il DELETE e' gia' andato a buon fine sul server: un secondo invio non deve partire solo perche'
    // la risposta e' malformata (un 404 al secondo giro farebbe sembrare fallita un'operazione riuscita).
    const fetchImpl = vi
      .fn()
      .mockResolvedValue(jsonResponse({ outcome: 'altro' })) as unknown as typeof fetch;

    // Resta un errore di validazione (un ApiError): non viene mascherato da un Error generico.
    await expect(
      clientWith(fetchImpl).deleteWithResult('/api/v1/pdf/bad-body', OutcomeSchema)
    ).rejects.toBeInstanceOf(SchemaValidationError);
    expect(fetchImpl).toHaveBeenCalledTimes(1);
  });

  it('non ripete il DELETE nemmeno se la lettura del corpo cade con un TypeError "fetch"', async () => {
    // Il caso che la regola "errori non ritentabili" non copre da sola: un `TypeError` il cui messaggio
    // contiene "fetch" (es. «Failed to fetch» su un corpo troncato) per il retry e' un errore di rete.
    // Qui la risposta e' gia' arrivata e il DELETE e' gia' stato eseguito: ripeterlo darebbe `kept` o 404.
    const truncated = {
      ok: true,
      status: 200,
      json: () => Promise.reject(new TypeError('Failed to fetch')),
    } as unknown as Response;
    const fetchImpl = vi.fn().mockResolvedValue(truncated) as unknown as typeof fetch;

    await expect(
      clientWith(fetchImpl).deleteWithResult('/api/v1/pdf/truncated-body', OutcomeSchema)
    ).rejects.toThrow(/truncated-body/);
    expect(fetchImpl).toHaveBeenCalledTimes(1);
  });

  it('propaga un errore HTTP come fa delete', async () => {
    const fetchImpl = vi
      .fn()
      .mockResolvedValue(jsonResponse({ error: 'not found' }, 404)) as unknown as typeof fetch;

    await expect(
      clientWith(fetchImpl).deleteWithResult('/api/v1/pdf/missing', OutcomeSchema)
    ).rejects.toMatchObject({ statusCode: 404 });
    expect(fetchImpl).toHaveBeenCalledTimes(1);
  });

  it('propaga un 401 come fa delete', async () => {
    const fetchImpl = vi
      .fn()
      .mockResolvedValue(jsonResponse({ error: 'unauthorized' }, 401)) as unknown as typeof fetch;

    await expect(
      clientWith(fetchImpl).deleteWithResult('/api/v1/pdf/unauthorized', OutcomeSchema, {
        skipErrorLogging: true,
      })
    ).rejects.toMatchObject({ statusCode: 401 });
  });

  it('non condivide la richiesta in volo con delete sullo stesso path', async () => {
    // Con la deduplicazione attiva due chiamate con la stessa chiave riusano la stessa promise: se
    // `delete` e `deleteWithResult` avessero la stessa chiave, una delle due riceverebbe il risultato
    // dell'altra (undefined al posto del corpo, o il corpo dove ci si aspetta undefined).
    const fetchImpl = vi
      .fn()
      .mockImplementation(async () =>
        jsonResponse({ outcome: 'unlinked', message: 'scollegato' })
      ) as unknown as typeof fetch;
    const client = clientWith(fetchImpl);
    const path = '/api/v1/pdf/shared-key';

    const [plain, withResult] = await Promise.all([
      client.delete(path, { skipDedup: false }),
      client.deleteWithResult(path, OutcomeSchema, { skipDedup: false }),
    ]);

    expect(plain).toBeUndefined();
    expect(withResult).toEqual({ outcome: 'unlinked', message: 'scollegato' });
  });
});

describe('HttpClient.delete (invariato)', () => {
  it('scarta il corpo di un 200 e risolve a undefined', async () => {
    const fetchImpl = vi
      .fn()
      .mockResolvedValue(
        jsonResponse({ outcome: 'kept', message: 'in uso' })
      ) as unknown as typeof fetch;

    await expect(clientWith(fetchImpl).delete('/api/v1/pdf/plain-200')).resolves.toBeUndefined();
  });

  it('risolve a undefined su 204', async () => {
    const fetchImpl = vi.fn().mockResolvedValue(noContentResponse()) as unknown as typeof fetch;

    await expect(clientWith(fetchImpl).delete('/api/v1/pdf/plain-204')).resolves.toBeUndefined();
  });

  it('propaga un errore HTTP', async () => {
    const fetchImpl = vi
      .fn()
      .mockResolvedValue(jsonResponse({ error: 'not found' }, 404)) as unknown as typeof fetch;

    await expect(clientWith(fetchImpl).delete('/api/v1/pdf/plain-404')).rejects.toMatchObject({
      statusCode: 404,
    });
  });
});
