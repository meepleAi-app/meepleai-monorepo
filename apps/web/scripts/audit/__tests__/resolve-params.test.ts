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

import { PARAM_QUERIES, resolveParams } from '../resolve-params';

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
