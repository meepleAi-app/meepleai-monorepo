/**
 * Unit test per il riporto delle verifiche manuali nel tracker.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { describe, expect, it } from 'vitest';

import { applyManualChecks, type ManualCheck } from '../mark-verified';

const CSV =
  'id,tipo,path,metodo,contesto,ruolo,livello,stato,evidenza,note\n' +
  'aaa11111,endpoint,/api/v1/auth/login,POST,Authentication,user,L1,⬜ non coperto,,\n' +
  'bbb22222,endpoint,/api/v1/auth/logout,POST,Authentication,user,L1,⬜ non coperto,,\n';

const check = (over: Partial<ManualCheck> = {}): ManualCheck => ({
  metodo: 'POST',
  path: '/api/v1/auth/login',
  livello: 'L3',
  esito: 'atteso',
  evidenza: 'cookie emesso, riga in user_sessions',
  ...over,
});

describe('applyManualChecks', () => {
  it('promuove la riga a verificato e ne alza il livello', () => {
    const { csv } = applyManualChecks(CSV, [check()]);
    const riga = csv.split('\n')[1].split(',');
    expect(riga[6]).toBe('L3');
    expect(riga[7]).toBe('✅ verificato');
    expect(riga[8]).toContain('user_sessions');
  });

  it('marca come finding le verifiche difformi', () => {
    const { csv } = applyManualChecks(CSV, [check({ esito: 'difforme' })]);
    expect(csv.split('\n')[1]).toContain('⚠️ finding da aprire');
  });

  it('non tocca le righe senza verifica', () => {
    const { csv } = applyManualChecks(CSV, [check()]);
    expect(csv.split('\n')[2]).toContain('⬜ non coperto');
  });

  it('segnala le verifiche senza riscontro invece di ignorarle', () => {
    // Un path scritto a mano che non combacia con l'inventario è un errore di
    // trascrizione: se passasse in silenzio, la verifica risulterebbe fatta
    // mentre nel tracker la riga resta scoperta.
    const { orfane } = applyManualChecks(CSV, [check({ path: '/api/v1/auth/inesistente' })]);
    expect(orfane).toHaveLength(1);
    expect(orfane[0].path).toBe('/api/v1/auth/inesistente');
  });

  it('neutralizza le virgole nell evidenza per non sfasare le colonne', () => {
    const { csv } = applyManualChecks(CSV, [check({ evidenza: 'a, b, c' })]);
    expect(csv.split('\n')[1].split(',')).toHaveLength(10);
  });
});
