/**
 * Visual contract — /games/[id] desktop route migrata vs mockup baseline.
 *
 * Issue #581 (Wave C.1 game detail desktop migration) · umbrella #578
 * V2 Migration Phase 1 (Wave C.1 brownfield big-bang).
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente, pre-bootstrap): la baseline PNG non esiste ancora.
 *   - **Green**: route v2 attiva (`(authenticated)/games/[id]/_components/
 *     GameDetailViewV2.tsx`) con hero + GameTabs (6 tab) + tabpanel.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   `gh workflow run visual-regression-migrated.yml \
 *     --ref feature/issue-581-wave-c1-game-detail-fe-v2-retry \
 *     -f mode=bootstrap -f project_filter=both`
 *   Il workflow setta `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` prima del
 *   build, così il fixture deterministico (Wingspan-shaped LibraryGameDetail)
 *   viene short-circuitato dal `GameDetailViewV2` orchestrator senza
 *   richiedere il backend API.
 *
 * Strategia ID (visual-test fixture, NOT a backend fetch):
 *   - `GameDetailViewV2` legge `IS_VISUAL_TEST_BUILD` da
 *     `@/lib/games/game-detail-visual-test-fixture` e sostituisce
 *     `detailQuery.data` con entries deterministiche (Wingspan-shaped).
 *   - In production deploy il fixture è dead code (constant-fold) — NON
 *     espone alcun shape pubblico.
 *
 * Auth bypass:
 *   La route `/games/[id]` è sotto `(authenticated)/` ma il layout non
 *   gate-keepa server-side; `PLAYWRIGHT_AUTH_BYPASS=true` settato in
 *   `playwright.config.ts:434` (webServer env) garantisce navigazione senza
 *   session cookie. `seedAuthSession` + `seedCookieConsent` + `mockAuthEndpoints`
 *   soddisfano il `proxy.ts` middleware gate + i flussi React-side `AuthProvider`
 *   / `useSessionCheck` (Wave B.1/B.2/B.3 lesson learned, triple-helper pattern).
 *
 * Cherry-picked from PR #697 commit 30d48a26e — updated data-slot selectors to
 * match Task 2 components (game-detail-view, game-detail-hero, game-detail-tabs).
 * networkidle anti-pattern replaced with domcontentloaded + explicit waitForSelector
 * (Wave B.1 lesson).
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const SLUG = 'sp4-game-detail';
const GAME_ID = '00000000-0000-4000-8000-000000000581';

async function waitForGameDetailReady(page: Page): Promise<void> {
  // Default state: wait for game-detail-view root + hero + tablist
  await page.waitForSelector('[data-slot="game-detail-view"]', { timeout: 30_000 });
  await page.waitForSelector('[data-slot="game-detail-hero"]', { timeout: 10_000 });
  await page.waitForSelector('[data-slot="game-detail-tabs"]', { timeout: 10_000 });

  // Fonts + RAF double-flush for stable screenshot
  await page.evaluate(async () => {
    if (typeof document !== 'undefined' && 'fonts' in document) {
      await (document as Document & { fonts: { ready: Promise<void> } }).fonts.ready;
    }
  });

  await page.evaluate(
    () =>
      new Promise<void>(resolve => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      })
  );
}

test.describe('V2 Visual Migrated — /games/[id] matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('Game detail desktop 1280x720 — default state matches sp4-game-detail mockup', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto(`/games/${GAME_ID}`, { waitUntil: 'domcontentloaded' });
    await waitForGameDetailReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}-desktop.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('Game detail mobile 375x812 — default state matches sp4-game-detail mockup', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto(`/games/${GAME_ID}`, { waitUntil: 'domcontentloaded' });
    await waitForGameDetailReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}-mobile.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
