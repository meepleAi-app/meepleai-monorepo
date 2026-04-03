/**
 * Library Navigation Config Tests
 * Verifica la logica di attivazione tab per i nuovi ID (games/wishlist/catalogo).
 */

import { describe, it, expect } from 'vitest';

import { LIBRARY_TABS, getActiveLibraryTab } from '@/config/library-navigation';

describe('getActiveLibraryTab', () => {
  it('ritorna "games" per /library senza tab param (default)', () => {
    expect(getActiveLibraryTab('/library')).toBe('games');
  });

  it('ritorna "games" per /library?tab=games', () => {
    expect(getActiveLibraryTab('/library', '?tab=games')).toBe('games');
  });

  it('ritorna "wishlist" per ?tab=wishlist', () => {
    expect(getActiveLibraryTab('/library', '?tab=wishlist')).toBe('wishlist');
  });

  it('ritorna "catalogo" per ?tab=catalogo', () => {
    expect(getActiveLibraryTab('/library', '?tab=catalogo')).toBe('catalogo');
  });

  it('ritorna "games" per tab sconosciuto (fallback)', () => {
    expect(getActiveLibraryTab('/library', '?tab=unknown')).toBe('games');
  });

  it('ritorna "games" per stringa search vuota', () => {
    expect(getActiveLibraryTab('/library', '')).toBe('games');
  });
});

describe('LIBRARY_TABS struttura', () => {
  it('ha tra 2 e 3 tab (2 in alpha, 3 in non-alpha)', () => {
    expect(LIBRARY_TABS.length).toBeGreaterThanOrEqual(2);
    expect(LIBRARY_TABS.length).toBeLessThanOrEqual(3);
  });

  it('contiene tab con id "games"', () => {
    expect(LIBRARY_TABS.some(t => t.id === 'games')).toBe(true);
  });

  it('contiene tab con id "wishlist"', () => {
    expect(LIBRARY_TABS.some(t => t.id === 'wishlist')).toBe(true);
  });

  it('label del tab "games" è "I Miei Giochi"', () => {
    const tab = LIBRARY_TABS.find(t => t.id === 'games');
    expect(tab?.label).toBe('I Miei Giochi');
  });

  it('label del tab "wishlist" è "Wishlist"', () => {
    const tab = LIBRARY_TABS.find(t => t.id === 'wishlist');
    expect(tab?.label).toBe('Wishlist');
  });

  it('href del tab "games" è "/library" (default, nessun param)', () => {
    const tab = LIBRARY_TABS.find(t => t.id === 'games');
    expect(tab?.href).toBe('/library');
  });

  it('href del tab "wishlist" è "/library?tab=wishlist"', () => {
    const tab = LIBRARY_TABS.find(t => t.id === 'wishlist');
    expect(tab?.href).toBe('/library?tab=wishlist');
  });

  it('non contiene più il tab "proposals"', () => {
    expect(LIBRARY_TABS.some(t => t.id === 'proposals')).toBe(false);
  });

  it('non contiene più il tab "collection" (rinominato in "catalogo")', () => {
    expect(LIBRARY_TABS.some(t => t.id === 'collection')).toBe(false);
  });

  it('non contiene più il tab "private" (rinominato in "games")', () => {
    expect(LIBRARY_TABS.some(t => t.id === 'private')).toBe(false);
  });
});
