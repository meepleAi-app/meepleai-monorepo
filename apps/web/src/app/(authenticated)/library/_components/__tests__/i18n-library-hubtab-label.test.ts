/**
 * i18n /library hub-tab label — gap A-01 (SP8 mobile audit, issue #3197)
 *
 * The in-page /library tab for the user's PERSONAL games
 * (`pages.library.hubTabs.games`, rendered by LibraryHub.tsx) collided with the
 * global sidebar/nav entry "Games" (the community catalog, id 'hub' →
 * href '/games', navigation.ts). In EN the clash was literal ("Games" vs "Games");
 * on mobile the sidebar collapses behind the hamburger, amplifying it.
 *
 * Fix: rename the TAB to the disambiguated "I miei giochi" / "My games",
 * leaving the nav catalog entry ("Games") untouched.
 *
 * These tests pin the disambiguated tab label in both catalogs and guard against
 * regressing to a value that collides with the nav "Games" entry.
 */

import itMessages from '@/locales/it.json';
import enMessages from '@/locales/en.json';
import { flattenMessages } from '@/locales';
import { UNIFIED_NAV_ITEMS } from '@/config/navigation';

const TAB_GAMES_KEY = 'pages.library.hubTabs.games';

describe('/library hub-tab "games" label disambiguation (A-01, #3197)', () => {
  const itFlat = flattenMessages(itMessages as Record<string, unknown>);
  const enFlat = flattenMessages(enMessages as Record<string, unknown>);
  const navCatalog = UNIFIED_NAV_ITEMS.find(item => item.id === 'hub');

  it('the nav catalog entry is still labelled "Games" (sanity — must not change)', () => {
    expect(navCatalog?.label).toBe('Games');
  });

  it('IT personal-games tab is the disambiguated "I miei giochi"', () => {
    expect(itFlat[TAB_GAMES_KEY]).toBe('I miei giochi');
  });

  it('EN personal-games tab is the disambiguated "My games"', () => {
    expect(enFlat[TAB_GAMES_KEY]).toBe('My games');
  });

  it('the tab label does not collide with the nav catalog "Games" entry (either locale)', () => {
    expect(itFlat[TAB_GAMES_KEY]).not.toBe(navCatalog?.label);
    expect(enFlat[TAB_GAMES_KEY]).not.toBe(navCatalog?.label);
  });
});
