/**
 * Visual contract — /players/[id] route migrata vs mockup baseline.
 *
 * Issue #683 (Wave 3 /players/[id] migration)
 * V2 Migration Phase 1 (Wave 3 Tier M).
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente, pre-bootstrap): la baseline PNG non esiste ancora.
 *   - **Green**: route v2 attiva (`(authenticated)/players/[id]/_components/
 *     PlayerDetailView.tsx`) con hero + stats grid + leaderboard + favorite agent
 *     + achievement grid.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   `gh workflow run visual-regression-migrated.yml --ref feature/issue-683-wave-3-player-detail-fe-v2 \
 *     -f mode=bootstrap -f project_filter=both`
 *   Il workflow setta `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` prima del
 *   build, così il fixture deterministico (Sara Rossi profile, Wingspan-shaped)
 *   viene short-circuitato dal `PlayerDetailView` orchestrator senza richiedere
 *   il backend API.
 *
 * Strategia ID (visual-test fixture, NOT a backend fetch):
 *   - Il `PlayerDetailView` orchestrator legge `IS_VISUAL_TEST_BUILD` da
 *     `@/lib/player-detail/player-detail-visual-test-fixture` e sostituisce
 *     `statsQuery.data` con il profilo deterministico (Sara Rossi).
 *   - In production deploy il fixture è dead code (constant-fold) — NON
 *     espone alcun shape pubblico.
 *
 * Hybrid masking:
 *   Le zone marcate `data-dynamic` sono mascherate per evitare flake.
 *
 * Auth bypass:
 *   La route `/players/[id]` è sotto `(authenticated)/` ma il layout non
 *   gate-keepa server-side; `PLAYWRIGHT_AUTH_BYPASS=true` settato in
 *   `playwright.config.ts:434` (webServer env) garantisce navigazione senza
 *   session cookie. `seedAuthSession` + `seedCookieConsent` + `mockAuthEndpoints`
 *   soddisfano il `proxy.ts` middleware gate + i flussi React-side `AuthProvider`
 *   / `useSessionCheck` (Wave B.1/B.2/B.3 lesson learned, triple-helper pattern).
 *
 * Cherry-picked patterns from Wave 4 D1 (players-index) + Wave C.1 (game-detail).
 * networkidle anti-pattern replaced with domcontentloaded + explicit waitForSelector.
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const SLUG = 'sp4-player-detail';
/** Matches FIXTURE_DEFAULT.playerId from player-detail-visual-test-fixture.ts */
const FIXTURE_PLAYER_ID = 'sara-rossi';

async function waitForPlayerDetailReady(page: Page): Promise<void> {
  // Default state: wait for root shell + hero (both rendered by fixture short-circuit)
  await page.waitForSelector('[data-slot="player-detail-view"]', { timeout: 30_000 });
  await page.waitForSelector('[data-slot="player-detail-hero"]', { timeout: 10_000 });
  await page.waitForSelector('[data-slot="player-detail-stats-grid"]', { timeout: 10_000 });

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

test.describe('V2 Visual Migrated — /players/[id] matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('Player detail desktop 1280x720 — default state matches sp4-player-detail mockup', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto(`/players/${FIXTURE_PLAYER_ID}`, { waitUntil: 'domcontentloaded' });
    await waitForPlayerDetailReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}-desktop.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('Player detail mobile 375x812 — default state matches sp4-player-detail mockup', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto(`/players/${FIXTURE_PLAYER_ID}`, { waitUntil: 'domcontentloaded' });
    await waitForPlayerDetailReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}-mobile.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
