/**
 * Contract test: every `?tab=` that `next.config.js` can produce for the game
 * detail route must be an id the page actually accepts.
 *
 * Why this exists — `/library/[gameId]/page.tsx` resolves the deep-link with
 *
 *   const initialTab: GameTabId = isGameTabId(tabParam) ? tabParam : 'info';
 *
 * so an unknown id does not error: it silently opens Info. Until 2026-10-07
 * seven legacy sub-route redirects (`?tab=agent`, `?tab=toolkit`,
 * `?tab=sessions`, `?tab=faq`, `?tab=reviews`, `?tab=rules`,
 * `?tab=strategies`) plus `/games/:id/knowledge-base` and `/games/:id/agents`
 * all mapped to ids `isGameTabId` rejects, so every one of those deep-links
 * opened the wrong tab without a trace. That is precisely the silent fallback
 * #1004 was filed about, surviving inside the redirects whose comment claimed
 * they "map specific tab paths".
 *
 * The binding below is the point of the test: it imports the REAL
 * `isGameTabId`, so renaming a tab id without updating `next.config.js` turns
 * this red instead of degrading deep-links in silence.
 */
import { describe, expect, it } from 'vitest';

import { GAME_TABS, isGameTabId } from '@/components/game-detail/tabs/types';

import nextConfig from '../../next.config.js';

interface RedirectEntry {
  source: string;
  destination: string;
  permanent?: boolean;
}

type ConfigWithRedirects = {
  redirects?: () => Promise<RedirectEntry[]>;
};

/** Destinations landing on the game detail route, i.e. governed by GAME_TABS. */
const GAME_DETAIL_DESTINATION = /^\/library\/:id(\?|$)/;

async function loadRedirects(): Promise<RedirectEntry[]> {
  const redirects = (nextConfig as ConfigWithRedirects).redirects;
  expect(typeof redirects).toBe('function');
  return (await redirects!()) ?? [];
}

function tabParamOf(destination: string): string | null {
  const match = /[?&]tab=([^&]*)/.exec(destination);
  return match ? decodeURIComponent(match[1]) : null;
}

describe('next.config.js — game detail tab redirects', () => {
  it('produces only tab ids that the game detail page accepts', async () => {
    const redirects = await loadRedirects();
    const gameDetail = redirects.filter(r => GAME_DETAIL_DESTINATION.test(r.destination));

    // Guard against the filter silently matching nothing: a passing assertion
    // over an empty set would make this whole test vacuous.
    expect(gameDetail.length).toBeGreaterThan(0);

    const invalid = gameDetail
      .map(r => ({ source: r.source, destination: r.destination, tab: tabParamOf(r.destination) }))
      // A destination with no `?tab=` opens the default tab by design.
      .filter(entry => entry.tab !== null && !isGameTabId(entry.tab));

    expect(
      invalid,
      `these redirects point at tab ids the page rejects, so they silently open Info. ` +
        `Valid ids: ${GAME_TABS.map(t => t.id).join(', ')}`
    ).toEqual([]);
  });

  it('maps the legacy sub-routes to their intended tabs', async () => {
    const redirects = await loadRedirects();
    const destinationOf = (source: string) => redirects.find(r => r.source === source)?.destination;

    // Regression anchors: these three carried a stale id until 2026-10-07.
    expect(destinationOf('/library/games/:id/agent')).toBe('/library/:id?tab=aiChat');
    expect(destinationOf('/library/games/:id/toolkit')).toBe('/library/:id?tab=toolbox');
    expect(destinationOf('/library/games/:id/sessions')).toBe('/library/:id?tab=partite');

    // The agent surface is also where the KB lives, so both legacy
    // `/games/:id/*` entry points land on the agent tab.
    expect(destinationOf('/games/:id/knowledge-base')).toBe('/library/:id?tab=aiChat');
    expect(destinationOf('/games/:id/agents')).toBe('/library/:id?tab=aiChat');
  });

  it('sends sub-routes with no surviving tab to the default tab explicitly', async () => {
    const redirects = await loadRedirects();
    const destinationOf = (source: string) => redirects.find(r => r.source === source)?.destination;

    // `faq`, `reviews`, `rules` and `strategies` have no counterpart in the
    // 5-tab surface. They used to point at non-existent ids, which looked
    // deliberate and behaved like a bug; they now declare the fallback.
    for (const source of [
      '/library/games/:id/faqs',
      '/library/games/:id/reviews',
      '/library/games/:id/rules',
      '/library/games/:id/strategies',
    ]) {
      expect(destinationOf(source), source).toBe('/library/:id');
    }
  });

  it('still has no catch-all for the bare `/library/games/:id` form (#1004, #4105)', async () => {
    const redirects = await loadRedirects();
    expect(redirects.map(r => r.source)).not.toContain('/library/games/:id');
  });
});
