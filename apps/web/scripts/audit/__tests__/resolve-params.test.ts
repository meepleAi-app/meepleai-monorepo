/**
 * Unit test per il risolutore dei parametri dinamici.
 *
 * Le rotte [gameId], [threadId], [sessionId] non si navigano senza id reali:
 * senza questo passaggio il crawler produrrebbe 404 e chiameremmo "rotto" ciò
 * che è solo non indirizzato.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { describe, expect, it, vi } from 'vitest';

import { PARAM_QUERIES, STATIC_PARAMS, resolveParams, resolveRouteUrl } from '../resolve-params';

describe('resolveRouteUrl', () => {
  const params = { gameId: 'G1', userId: 'U1', sessionId: 'S1' };

  it('lascia intatte le rotte statiche', () => {
    expect(resolveRouteUrl('/library', params)).toBe('/library');
  });

  it('sostituisce un parametro con nome esplicito', () => {
    expect(resolveRouteUrl('/library/[gameId]/kb', params)).toBe('/library/G1/kb');
  });

  it('risolve [id] in base al prefisso della rotta, non a un valore unico', () => {
    // [id] compare in 40 rotte con significati diversi: un valore solo
    // produrrebbe 404 su tutte le rotte di tipo diverso.
    expect(resolveRouteUrl('/admin/users/[id]', params)).toBe('/admin/users/U1');
    expect(resolveRouteUrl('/games/[id]', params)).toBe('/games/G1');
  });

  it('restituisce null quando il parametro non è risolvibile', () => {
    expect(resolveRouteUrl('/chat/[threadId]', params)).toBeNull();
  });

  it('restituisce null per un [id] di cui non conosce il tipo', () => {
    expect(resolveRouteUrl('/qualcosa/[id]', params)).toBeNull();
  });

  it('risolve i catch-all come i parametri semplici', () => {
    expect(resolveRouteUrl('/games/[...id]', params)).toBe('/games/G1');
  });
});

/**
 * #4056. `[token]` e `[code]` sono generici esattamente come `[id]`: lo stesso nome di segmento
 * indica l'invito a una serata, la condivisione di un game-night, il join di una live session, la
 * condivisione di un play-record o quella di una libreria. Prima solo `[id]` aveva una
 * risoluzione per contesto, quindi queste rotte restavano non risolte e il crawler le saltava.
 */
describe('resolveRouteUrl — parametri generici oltre [id]', () => {
  const params = {
    gameNightShareToken: 'GNS',
    gameNightInviteToken: 'GNI',
    playRecordShareToken: 'PRS',
    libraryShareToken: 'LBS',
    liveSessionCode: 'CODE',
    providerName: 'openrouter',
  };

  it('risolve [token] in base al prefisso, non con un valore unico', () => {
    // `/invites/[token]` prende l'invito a una SERATA, non quello di `admin/invitations`: la
    // pagina interroga `/api/v1/game-nights/invitations/{token}`. Il crawl lo ha dimostrato
    // riportando un 404 su quell'endpoint quando le si passava un token di invito utente.
    expect(resolveRouteUrl('/invites/[token]', params)).toBe('/invites/GNI');
    expect(resolveRouteUrl('/game-nights/shared/[token]', params)).toBe('/game-nights/shared/GNS');
    expect(resolveRouteUrl('/play-records/shared/[token]', params)).toBe(
      '/play-records/shared/PRS'
    );
    expect(resolveRouteUrl('/library/shared/[token]', params)).toBe('/library/shared/LBS');
  });

  it('manda /join/[token] sul codice di sessione, che è ciò che quella rotta interroga', () => {
    // La pagina chiama `/api/v1/live-sessions/code/{…}`: il nome del segmento dice "token" ma il
    // valore atteso è un codice. Usare un token di invito qui darebbe 404.
    expect(resolveRouteUrl('/join/[token]', params)).toBe('/join/CODE');
  });

  it('due rotte /join annidate prendono sorgenti diverse', () => {
    // Non è il sort "prefisso più lungo vince" a decidere qui: ciascuna di queste rotte
    // combacia con un solo prefisso. Il sort resta una salvaguardia non esercitata da nessuna
    // rotta attuale — nessun prefisso in GENERIC_PARAM_SOURCES è prefisso di un altro per lo
    // stesso parametro — e serve il giorno che qualcuno aggiunga `/join/x/[token]` accanto a
    // `/join/[token]`.
    expect(resolveRouteUrl('/join/event/[code]', params)).toBe('/join/event/GNI');
    expect(resolveRouteUrl('/join/session/[code]', params)).toBe('/join/session/CODE');
  });

  it('risolve [name] sul nome di provider', () => {
    expect(resolveRouteUrl('/admin/providers/[name]', params)).toBe('/admin/providers/openrouter');
  });

  it('resta null per un [token] di cui non conosce il contesto', () => {
    // Il comportamento voluto: una rotta nuova con [token] e nessuna riga in
    // GENERIC_PARAM_SOURCES viene saltata, non visitata con il token sbagliato.
    expect(resolveRouteUrl('/qualcosa/[token]', params)).toBeNull();
  });

  it('un valore diretto con quel nome continua a vincere sulla risoluzione per prefisso', () => {
    expect(resolveRouteUrl('/invites/[token]', { ...params, token: 'DIRETTO' })).toBe(
      '/invites/DIRETTO'
    );
  });
});

describe('STATIC_PARAMS', () => {
  it('porta un nome di provider che la pagina accetta', () => {
    // La pagina `/admin/providers/[name]` risponde notFound() per tutto ciò che non è in
    // KNOWN_PROVIDERS: il valore si importa da lì, così un rinominio non fa ricomparire un salto.
    expect(STATIC_PARAMS.providerName).toBeTruthy();
  });
});

describe('resolveParams', () => {
  it('restituisce un valore per ogni parametro noto, più quelli statici', () => {
    const run = vi.fn().mockReturnValue('11111111-2222-3333-4444-555555555555\n');
    const params = resolveParams(run);

    expect(Object.keys(params).sort()).toEqual(
      [...Object.keys(PARAM_QUERIES), ...Object.keys(STATIC_PARAMS)].sort()
    );
    expect(params.gameId).toBe('11111111-2222-3333-4444-555555555555');
    expect(run).toHaveBeenCalledTimes(Object.keys(PARAM_QUERIES).length);
  });

  it('i parametri statici sopravvivono al fallimento di ogni query', () => {
    // Database spento: la mappa non è vuota, perché providerName non viene da lì.
    const params = resolveParams(() => {
      throw new Error('connection refused');
    });
    expect(params.providerName).toBe(STATIC_PARAMS.providerName);
  });

  it('omette il parametro quando la query non restituisce righe', () => {
    expect(resolveParams(() => '\n').gameId).toBeUndefined();
  });

  it('omette il parametro quando la query fallisce, senza interrompere gli altri', () => {
    const run = vi.fn((sql: string) => {
      if (sql.includes('games')) throw new Error('relation does not exist');
      return 'ok-value\n';
    });
    const params = resolveParams(run);

    expect(params.gameId).toBeUndefined();
    expect(Object.keys(params).length).toBeGreaterThan(0);
  });

  it('prende solo la prima riga quando la query ne restituisce più di una', () => {
    expect(resolveParams(() => 'primo\nsecondo\n').gameId).toBe('primo');
  });
});
