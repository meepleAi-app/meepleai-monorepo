/**
 * Breadcrumb utility tests
 *
 * Validates the shared breadcrumb helpers extracted from DesktopBreadcrumb.
 */

import { describe, it, expect } from 'vitest';

import {
  SEGMENT_LABELS,
  isLikelyId,
  formatFallbackLabel,
  buildBreadcrumbs,
} from '../breadcrumb-utils';

// ─── SEGMENT_LABELS ──────────────────────────────────────────────────────────

describe('SEGMENT_LABELS', () => {
  it('contains expected Italian labels for core sections', () => {
    expect(SEGMENT_LABELS['library']).toBe('Libreria');
    expect(SEGMENT_LABELS['games']).toBe('Catalogo');
    expect(SEGMENT_LABELS['dashboard']).toBe('Dashboard');
    expect(SEGMENT_LABELS['favorites']).toBe('Preferiti');
    expect(SEGMENT_LABELS['settings']).toBe('Impostazioni');
  });

  it('contains new game-nights and toolkit labels', () => {
    expect(SEGMENT_LABELS['game-nights']).toBe('Serate di Gioco');
    expect(SEGMENT_LABELS['new']).toBe('Nuovo');
    expect(SEGMENT_LABELS['edit']).toBe('Modifica');
    expect(SEGMENT_LABELS['toolkit']).toBe('Toolkit');
    expect(SEGMENT_LABELS['badges']).toBe('Badge');
  });
});

// ─── isLikelyId ──────────────────────────────────────────────────────────────

describe('isLikelyId', () => {
  it('detects standard UUID format', () => {
    expect(isLikelyId('a1b2c3d4-e5f6-7890-abcd-ef1234567890')).toBe(true);
    expect(isLikelyId('00000000-0000-0000-0000-000000000000')).toBe(true);
  });

  it('detects uppercase UUIDs', () => {
    expect(isLikelyId('A1B2C3D4-E5F6-7890-ABCD-EF1234567890')).toBe(true);
  });

  it('detects numeric IDs', () => {
    expect(isLikelyId('123')).toBe(true);
    expect(isLikelyId('0')).toBe(true);
    expect(isLikelyId('99999')).toBe(true);
  });

  it('rejects named segments', () => {
    expect(isLikelyId('dashboard')).toBe(false);
    expect(isLikelyId('library')).toBe(false);
    expect(isLikelyId('games')).toBe(false);
    expect(isLikelyId('game-nights')).toBe(false);
    expect(isLikelyId('play-records')).toBe(false);
  });

  it('rejects partial UUIDs and mixed strings', () => {
    expect(isLikelyId('a1b2c3d4')).toBe(false);
    expect(isLikelyId('abc-123')).toBe(false);
    expect(isLikelyId('12abc')).toBe(false);
  });
});

// ─── formatFallbackLabel ─────────────────────────────────────────────────────

describe('formatFallbackLabel', () => {
  it('capitalises single word', () => {
    expect(formatFallbackLabel('reports')).toBe('Reports');
  });

  it('splits hyphenated segments and capitalises each word', () => {
    expect(formatFallbackLabel('my-custom-page')).toBe('My Custom Page');
  });
});

// ─── buildBreadcrumbs ────────────────────────────────────────────────────────

describe('buildBreadcrumbs', () => {
  it('returns Dashboard only for /dashboard', () => {
    const crumbs = buildBreadcrumbs('/dashboard');
    expect(crumbs).toHaveLength(1);
    expect(crumbs[0]).toEqual({
      label: 'Dashboard',
      href: '/dashboard',
      isCurrent: true,
    });
  });

  it('returns Dashboard + Libreria for /library', () => {
    const crumbs = buildBreadcrumbs('/library');
    expect(crumbs).toHaveLength(2);
    expect(crumbs[0]).toEqual({
      label: 'Dashboard',
      href: '/dashboard',
      isCurrent: false,
    });
    expect(crumbs[1]).toEqual({
      label: 'Libreria',
      href: null,
      isCurrent: true,
    });
  });

  it('renders UUID segments as "Dettaglio"', () => {
    const crumbs = buildBreadcrumbs('/library/a1b2c3d4-e5f6-7890-abcd-ef1234567890');
    expect(crumbs).toHaveLength(3);
    expect(crumbs[2]).toEqual({
      label: 'Dettaglio',
      href: null,
      isCurrent: true,
    });
  });

  it('builds 3 segments for /library/favorites', () => {
    const crumbs = buildBreadcrumbs('/library/favorites');
    expect(crumbs).toHaveLength(3);
    expect(crumbs[0].label).toBe('Dashboard');
    expect(crumbs[1].label).toBe('Libreria');
    expect(crumbs[1].href).toBe('/library');
    expect(crumbs[1].isCurrent).toBe(false);
    expect(crumbs[2].label).toBe('Preferiti');
    expect(crumbs[2].href).toBeNull();
    expect(crumbs[2].isCurrent).toBe(true);
  });

  it('uses fallback label for unknown segments', () => {
    const crumbs = buildBreadcrumbs('/some-unknown-page');
    expect(crumbs).toHaveLength(2);
    expect(crumbs[1].label).toBe('Some Unknown Page');
  });

  it('handles intermediate ID segments with href', () => {
    const crumbs = buildBreadcrumbs('/games/a1b2c3d4-e5f6-7890-abcd-ef1234567890/faqs');
    expect(crumbs).toHaveLength(4);
    // Intermediate ID keeps href for navigation
    expect(crumbs[2].label).toBe('Dettaglio');
    expect(crumbs[2].href).toBe('/games/a1b2c3d4-e5f6-7890-abcd-ef1234567890');
    expect(crumbs[2].isCurrent).toBe(false);
    // Last segment is current
    expect(crumbs[3].label).toBe('FAQ');
    expect(crumbs[3].isCurrent).toBe(true);
  });
});
