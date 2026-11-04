/**
 * #4099 — una risposta 204 non deve essere interpretata come JSON.
 *
 * Il difetto: `put` era l'unico verbo senza la guardia sul 204 (POST, PATCH e DELETE la hanno), e
 * quindici endpoint `MapPut` del backend rispondono `204 No Content`. Il `response.json()` su corpo
 * vuoto lanciava «Unexpected end of JSON input», quindi un salvataggio RIUSCITO arrivava alla UI
 * come un errore: l'edit admin del catalogo mostrava l'alert e non chiudeva il drawer mentre il
 * dato era già persistito (verificato nel database).
 *
 * Questi test usano il client REALE con `fetchImpl` iniettato. È la differenza che conta: gli altri
 * test dell'area mockano `HttpClient` per intero, quindi non potevano vedere un difetto che vive
 * nel contratto fra il client e il 204.
 */

import { describe, it, expect, vi } from 'vitest';

import { HttpClient } from '../httpClient';

/** Una `Response` 204 come la manda il backend: nessun corpo, nessun content-type. */
function noContentResponse(): Response {
  return new Response(null, { status: 204, statusText: 'No Content' });
}

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'content-type': 'application/json' },
  });
}

function clientWith(fetchImpl: typeof fetch) {
  return new HttpClient({ baseUrl: 'http://test.local', fetchImpl });
}

describe('HttpClient e le risposte 204', () => {
  it('put non lancia e restituisce undefined su 204', async () => {
    const fetchImpl = vi.fn().mockResolvedValue(noContentResponse()) as unknown as typeof fetch;
    const client = clientWith(fetchImpl);

    // Senza la guardia questo rigetta con «Unexpected end of JSON input».
    await expect(
      client.put('/api/v1/admin/shared-games/abc', { title: 'x' })
    ).resolves.toBeUndefined();
  });

  it.each([
    ['post', (c: HttpClient) => c.post('/p', {})],
    ['patch', (c: HttpClient) => c.patch('/p', {})],
  ] as const)('%s gestisce il 204 allo stesso modo', async (_verb, call) => {
    // Il controllo che impedisce al difetto di tornare su un altro verbo: la guardia deve esserci
    // su tutti, non solo su quello che qualcuno ha corretto per ultimo.
    const fetchImpl = vi.fn().mockResolvedValue(noContentResponse()) as unknown as typeof fetch;

    await expect(call(clientWith(fetchImpl))).resolves.toBeUndefined();
  });

  it('put interpreta ancora il corpo quando c è (200 con JSON)', async () => {
    // Il controllo al rovescio: la guardia non deve far perdere le risposte che HANNO un corpo.
    const fetchImpl = vi
      .fn()
      .mockResolvedValue(jsonResponse({ id: 'abc', title: 'x' })) as unknown as typeof fetch;

    await expect(clientWith(fetchImpl).put('/p', { title: 'x' })).resolves.toEqual({
      id: 'abc',
      title: 'x',
    });
  });
});
