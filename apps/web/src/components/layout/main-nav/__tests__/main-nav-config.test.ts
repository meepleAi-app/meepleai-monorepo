import { describe, it, expect } from 'vitest';

import { MAIN_NAV_ITEMS } from '../main-nav-config';

/**
 * CRIT-8 fix tests + invariante #20 of the GameNight/Session domain model
 * spec (docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md).
 *
 * Invariante #20: sidebar has EXACTLY two game-related voices (Library +
 * Games); Discover is a TAB of /games, not a standalone sidebar item.
 */
describe('MAIN_NAV_ITEMS', () => {
  it('contains exactly 8 voci in the expected order', () => {
    expect(MAIN_NAV_ITEMS.map(i => i.id)).toEqual([
      'dashboard',
      'library',
      'games',
      'gamenights',
      'sessions',
      'agents',
      'notifications',
      'profile',
    ]);
  });

  it('marks exactly two voices as game-related (invariante #20)', () => {
    const gameRelated = MAIN_NAV_ITEMS.filter(i => i.gameRelated);
    expect(gameRelated.map(i => i.id)).toEqual(['library', 'games']);
  });

  it('Games entry defaults to ?tab=discover as landing tab (invariante #20)', () => {
    const games = MAIN_NAV_ITEMS.find(i => i.id === 'games');
    expect(games?.defaultHref).toBe('/games?tab=discover');
  });

  it('does NOT expose a standalone Discover item (collapsed into /games tab)', () => {
    const discover = MAIN_NAV_ITEMS.find(i => i.id === 'discover');
    expect(discover).toBeUndefined();
  });

  it('Notifications voce uses Bell icon and supports the counter badge', () => {
    const notif = MAIN_NAV_ITEMS.find(i => i.id === 'notifications');
    expect(notif).toBeDefined();
    // Lucide icons are components — name lookup via displayName/render is
    // brittle; the canonical test is that the icon is the imported Bell
    // component. We assert the slot via render in MainNavList.test.tsx.
    expect(notif?.showCounter).toBe(true);
    expect(notif?.defaultHref).toBe('/notifications');
  });

  it('every item has non-empty id, label and defaultHref', () => {
    for (const item of MAIN_NAV_ITEMS) {
      expect(item.id.length).toBeGreaterThan(0);
      expect(item.label.length).toBeGreaterThan(0);
      expect(item.defaultHref.length).toBeGreaterThan(0);
      expect(item.defaultHref.startsWith('/')).toBe(true);
    }
  });

  it('has no duplicate ids', () => {
    const ids = MAIN_NAV_ITEMS.map(i => i.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('has no duplicate defaultHref values', () => {
    const hrefs = MAIN_NAV_ITEMS.map(i => i.defaultHref);
    expect(new Set(hrefs).size).toBe(hrefs.length);
  });

  it('only the notifications item declares showCounter (MVP)', () => {
    const counters = MAIN_NAV_ITEMS.filter(i => i.showCounter === true);
    expect(counters.map(i => i.id)).toEqual(['notifications']);
  });
});
