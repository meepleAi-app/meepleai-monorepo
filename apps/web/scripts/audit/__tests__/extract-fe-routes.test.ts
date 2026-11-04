/**
 * Unit test per l'estrattore di rotte frontend — ondata 0 dell'audit esaustivo.
 *
 * Verifica che i path di `src/app` diventino URL corrette:
 *   - i route group `(...)` scompaiono dalla URL ma restano come etichetta di gruppo
 *   - i segmenti dinamici `[param]` e catch-all `[...slug]` sono riconosciuti
 *   - i file che non sono `page.tsx` vengono ignorati
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { describe, expect, it } from 'vitest';

import { toRoute } from '../extract-fe-routes';

describe('toRoute', () => {
  it('elimina i route group dalla URL ma li conserva come gruppo', () => {
    expect(toRoute('(authenticated)/library/page.tsx')).toEqual({
      route: '/library',
      group: '(authenticated)',
      dynamicSegments: [],
      file: '(authenticated)/library/page.tsx',
    });
  });

  it('riconosce i segmenti dinamici', () => {
    expect(toRoute('(authenticated)/library/[gameId]/kb/page.tsx')).toEqual({
      route: '/library/[gameId]/kb',
      group: '(authenticated)',
      dynamicSegments: ['gameId'],
      file: '(authenticated)/library/[gameId]/kb/page.tsx',
    });
  });

  it('riconosce i catch-all', () => {
    const entry = toRoute('admin/docs/[...slug]/page.tsx');
    expect(entry?.route).toBe('/admin/docs/[...slug]');
    expect(entry?.dynamicSegments).toEqual(['...slug']);
  });

  it('mappa la root su /', () => {
    expect(toRoute('page.tsx')?.route).toBe('/');
  });

  it('usa il primo segmento come gruppo quando non ci sono parentesi', () => {
    expect(toRoute('admin/users/page.tsx')?.group).toBe('admin');
  });

  it('preferisce il primo segmento a un route group annidato', () => {
    // 94 pagine admin vivono sotto admin/(dashboard)/…: il gruppo utile è 'admin',
    // non '(dashboard)', altrimenti l'inventario perde di vista chi sono.
    const entry = toRoute('admin/(dashboard)/users/page.tsx');
    expect(entry?.group).toBe('admin');
    expect(entry?.route).toBe('/admin/users');
  });

  it('ignora i file che non sono page.tsx', () => {
    expect(toRoute('(authenticated)/library/layout.tsx')).toBeNull();
  });
});
