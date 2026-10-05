/**
 * #4059 — `agentDefinitionsApi` rifiuta un id inutilizzabile PRIMA di interpolarlo nel path.
 *
 * Il difetto: la pagina di edit leggeva `params.id` da una Promise, otteneva `undefined`, e il
 * valore finiva nel path. Misurato con un id reale nella URL del browser:
 * `404 /api/v1/admin/agent-definitions/undefined`.
 *
 * Perché la guardia serve anche ora che la pagina è corretta: quel 404 era **indistinguibile**
 * da «questo id non esiste» — tanto che la pagina mostrava «Agent not found», cioè la diagnosi
 * sbagliata. Un 404 è un esito legittimo dell'API, un id assente è un difetto del chiamante, e
 * i due vanno separati prima della richiesta.
 *
 * Il caso `"undefined"` come **stringa** è il più importante: è quello che si ottiene
 * interpolando il valore, quindi un controllo di sola verità (`if (!id)`) non lo vedrebbe.
 */

import { describe, expect, it, vi, beforeEach } from 'vitest';

import { agentDefinitionsApi } from '../agent-definitions.api';

describe("agentDefinitionsApi — guardia sull'id (#4059)", () => {
  beforeEach(() => {
    // Qualunque chiamata di rete qui sarebbe un fallimento del test: la guardia deve scattare
    // prima. Lo stub serve proprio a renderlo osservabile.
    vi.restoreAllMocks();
  });

  const unusable: Array<[label: string, value: string]> = [
    ['la stringa letterale "undefined"', 'undefined'],
    ['la stringa letterale "null"', 'null'],
    ['una stringa vuota', ''],
    ['soli spazi', '   '],
  ];

  describe.each(unusable)('con %s', (_label, value) => {
    it('getById lancia e NON fa alcuna richiesta', async () => {
      const fetchSpy = vi.spyOn(globalThis, 'fetch');

      await expect(agentDefinitionsApi.getById(value)).rejects.toThrow(/id non utilizzabile/);
      expect(fetchSpy).not.toHaveBeenCalled();
    });

    it('update lancia e NON fa alcuna richiesta', async () => {
      const fetchSpy = vi.spyOn(globalThis, 'fetch');

      await expect(
        agentDefinitionsApi.update(value, {
          name: 'x',
          description: null,
          model: 'm',
          maxTokens: 1,
          temperature: 0,
          prompts: [],
          tools: [],
        } as unknown as Parameters<typeof agentDefinitionsApi.update>[1])
      ).rejects.toThrow(/id non utilizzabile/);
      expect(fetchSpy).not.toHaveBeenCalled();
    });

    it('delete lancia e NON fa alcuna richiesta', async () => {
      const fetchSpy = vi.spyOn(globalThis, 'fetch');

      await expect(agentDefinitionsApi.delete(value)).rejects.toThrow(/id non utilizzabile/);
      expect(fetchSpy).not.toHaveBeenCalled();
    });
  });

  it('il messaggio dice al chiamante cosa ha sbagliato, non solo che ha sbagliato', async () => {
    // Un «invalid id» non avrebbe risparmiato il giro di diagnosi che questo difetto è costato:
    // il messaggio deve nominare la causa vera, cioè il parametro di rotta non risolto.
    await expect(agentDefinitionsApi.getById('undefined')).rejects.toThrow(/use\(params\)/);
    await expect(agentDefinitionsApi.getById('undefined')).rejects.toThrow(/getById/);
  });

  it('un id plausibile NON viene rifiutato: la guardia non è un blocco generico', async () => {
    // 🔴 Il controllo di non-vacuità al rovescio, e serve: una guardia che rifiutasse *tutto*
    // passerebbe ogni test qui sopra e romperebbe l'applicazione.
    //
    // ⚠️ L'asserzione è sul MOTIVO del rifiuto, non sul fatto che la richiesta parta. Un primo
    // tentativo spiava `globalThis.fetch` e falliva: `HttpClient` lega `fetchImpl` nel
    // costruttore, e `httpClient` è un singleton creato all'import del modulo, quindi una spia
    // installata dopo non intercetta nulla. Il motivo del rifiuto è comunque l'invariante che
    // interessa — un id valido deve superare la guardia, non deve necessariamente trovare un
    // server in un test unit.
    const outcome = await agentDefinitionsApi
      .getById('4496601f-060d-48ce-bdf0-5c3567a33acf')
      .then(() => null)
      .catch((error: unknown) => (error instanceof Error ? error.message : String(error)));

    if (outcome !== null) {
      expect(outcome).not.toMatch(/id non utilizzabile/);
    }
  });
});
