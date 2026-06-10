/**
 * E2E BGG ToS Compliance Tests — Issue #2123
 *
 * Asserts that user-facing routes do NOT issue any browser request to BGG
 * hosts. The custom Next.js image loader is the runtime guard; this suite
 * is the end-to-end proof the guard is actually wired across the public
 * surface (Cover wrapper, MeepleCard, GameDetail, etc.).
 *
 * For each target route the test:
 *   1. Installs a network listener on the page that captures any request
 *      whose URL matches `geekdo|boardgamegeek` (any host variant).
 *   2. Navigates and waits for network idle.
 *   3. Asserts the captured list is empty.
 *
 * Refs:
 *   Issue : https://github.com/meepleAi-app/meepleai-monorepo/issues/2123
 *   Spec  : docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md (AC-13)
 *   ADR   : docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md §5
 */

import { test, expect, type Page } from '@playwright/test';

const BGG_PATTERN =
  /(cf\.geekdo-images\.com|geekdo-images\.com|images\.geekdo\.com|boardgamegeek\.com)/i;

/**
 * Public routes that must NEVER issue a BGG-host request. Auth-required routes
 * are excluded from this skeleton; the same assertion should be added to those
 * routes once the auth fixture is wired in (Phase E follow-up).
 *
 * `/shared-games` and `/discover` are unauthenticated SSR pages — the most
 * critical surfaces for ToS exposure because they're crawlable by search
 * engines and reachable without any login.
 */
const PUBLIC_ROUTES = [
  { path: '/shared-games', name: 'public shared-games catalog' },
  { path: '/discover', name: 'public discover hub' },
];

async function collectBggRequests(page: Page, action: () => Promise<void>): Promise<string[]> {
  const captured: string[] = [];
  const listener = (req: { url: () => string }) => {
    const url = req.url();
    if (BGG_PATTERN.test(url)) captured.push(url);
  };
  page.on('request', listener);
  try {
    await action();
  } finally {
    page.off('request', listener);
  }
  return captured;
}

for (const route of PUBLIC_ROUTES) {
  test(`#2123 — no BGG network requests on ${route.path} (${route.name})`, async ({ page }) => {
    const offending = await collectBggRequests(page, async () => {
      await page.goto(route.path, { waitUntil: 'domcontentloaded' });
      // Wait for any lazy-loaded covers / images to either resolve or fall back.
      await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});
    });

    // Soft assertion: include the offending URLs in the error so a regression
    // points at the specific BGG asset that slipped through. Hard assertion:
    // any single captured URL fails the test.
    expect(
      offending,
      `Expected zero BGG-host requests on ${route.path}; got ${offending.length}:\n  - ` +
        offending.slice(0, 10).join('\n  - ') +
        (offending.length > 10 ? `\n  - … and ${offending.length - 10} more` : '')
    ).toEqual([]);
  });
}

/**
 * Cover renderer contract: when a SharedGame has no cover R2 key, the FE
 * MUST render a deterministic placeholder (no <img>/<Image> with a BGG URL,
 * no network request to BGG). This test pin-points the visual outcome rather
 * than relying solely on the network sniffer.
 *
 * Implementation note: the deterministic placeholder rendered by
 * `lib/games/cover-utils.ts` has a stable text label = extracted initials.
 * The test only asserts the absence of BGG src attributes; the visual
 * regression of the placeholder itself is covered by the unit-test snapshot
 * for `<Cover>`.
 */
test('#2123 — no <img>/<Image> on /shared-games has a BGG src attribute', async ({ page }) => {
  await page.goto('/shared-games', { waitUntil: 'domcontentloaded' });
  await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});

  const bggSrcs = await page
    .locator('img, source')
    .evaluateAll(nodes =>
      nodes
        .map(
          node =>
            (node as HTMLImageElement | HTMLSourceElement).src ||
            (node as HTMLSourceElement).srcset ||
            ''
        )
        .filter(src =>
          /(cf\.geekdo-images\.com|geekdo-images\.com|images\.geekdo\.com|boardgamegeek\.com)/i.test(
            src
          )
        )
    );

  expect(
    bggSrcs,
    `Expected zero <img>/<source> with a BGG src on /shared-games; got ${bggSrcs.length}:\n  - ` +
      bggSrcs.join('\n  - ')
  ).toEqual([]);
});
