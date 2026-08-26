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

import { PARAM_QUERIES, resolveParams, resolveRouteUrl } from '../resolve-params';

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

describe('resolveParams', () => {
  it('restituisce un valore per ogni parametro noto', () => {
    const run = vi.fn().mockReturnValue('11111111-2222-3333-4444-555555555555\n');
    const params = resolveParams(run);

    expect(Object.keys(params).sort()).toEqual(Object.keys(PARAM_QUERIES).sort());
    expect(params.gameId).toBe('11111111-2222-3333-4444-555555555555');
    expect(run).toHaveBeenCalledTimes(Object.keys(PARAM_QUERIES).length);
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
