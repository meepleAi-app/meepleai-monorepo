/**
 * V2 State Coverage — /players/[id] route (Issue #683, Wave 3 Tier M).
 *
 * Captures 3 surface states del 4-state FSM:
 *   `default | loading | not-found`
 *
 * Il quarto state `error` è escluso dalla coverage visual perché surface
 * dipendente da `statsQuery.isError` (TanStack Query), che richiede un mock
 * di network failure non riproducibile in modo deterministico via URL
 * override. Coperto invece dal unit test orchestratore
 * (`_components/__tests__/PlayerDetailView.test.tsx`).
 *
 * Uses test-only `?state=...` query param, gated da
 * `NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD` in
 * `_components/PlayerDetailView.tsx`.
 *
 * VALID_OVERRIDES dal orchestrator: `loading | error | not-found`.
 *
 * Test ID strategy:
 *   - In CI bootstrap workflow `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` è
 *     settato pre-build, attivando il fixture deterministico
 *     (`tryLoadVisualTestFixture()` da
 *     `@/lib/player-detail/player-detail-visual-test-fixture`).
 *   - `?state=not-found` + fixture disabled → not-found shell renders.
 *   - `?state=loading` → loading skeleton renders (fixture does NOT intercept).
 *   - Default state: fixture active → Sara Rossi profile renders.
 *
 * data-slot selectors (actual shells from committed orchestrator Task 3):
 *   - default:   [data-slot="player-detail-view"]
 *   - loading:   [data-slot="player-detail-loading"]
 *   - not-found: [data-slot="player-detail-not-found"]
 *
 * Snapshots written to `apps/web/e2e/v2-states/player-detail.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run visual-regression-migrated.yml \
 *     --ref feature/issue-683-wave-3-player-detail-fe-v2 \
 *     -f mode=bootstrap -f project_filter=both`
 *
 * Cherry-picked patterns from Wave C.1 (game-detail) + Wave 4 D1 (players-index).
 * networkidle anti-pattern replaced with domcontentloaded + explicit waitForSelector.
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

/** Matches FIXTURE_DEFAULT.playerId from player-detail-visual-test-fixture.ts */
const FIXTURE_PLAYER_ID = 'sara-rossi';

const VIEWPORTS = [
  { name: 'desktop', width: 1280, height: 720 },
  { name: 'mobile', width: 375, height: 812 },
] as const;

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function waitForFontsAndRaf(page: Page): Promise<void> {
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

test.describe('Player detail — state coverage', () => {
  test.describe.configure({ retries: 0 });

  for (const viewport of VIEWPORTS) {
    test.describe(`${viewport.name} ${viewport.width}x${viewport.height}`, () => {
      test('default state', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        await page.goto(`/players/${FIXTURE_PLAYER_ID}`, { waitUntil: 'domcontentloaded' });
        // Default state: fixture active → root shell + hero + stats grid rendered
        await page.waitForSelector('[data-slot="player-detail-view"]', { timeout: 30_000 });
        await page.waitForSelector('[data-slot="player-detail-hero"]', { timeout: 10_000 });
        await waitForFontsAndRaf(page);

        await expect(page).toHaveScreenshot(`player-detail-${viewport.name}-default.png`, {
          fullPage: true,
          animations: 'disabled',
          mask: [page.locator('[data-dynamic]')],
        });
      });

      test('loading state', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        await page.goto(`/players/${FIXTURE_PLAYER_ID}?state=loading`, {
          waitUntil: 'domcontentloaded',
        });
        // Loading state: separate inline skeleton shell with animate-pulse elements.
        // NOT EmptyPlayerDetail — mirror Wave B.1/C.1 pattern.
        await page.waitForSelector('[data-slot="player-detail-loading"]', { timeout: 30_000 });
        await waitForFontsAndRaf(page);

        await expect(page).toHaveScreenshot(`player-detail-${viewport.name}-loading.png`, {
          fullPage: true,
          animations: 'disabled',
          // Skeleton pulse animations — mask to avoid flake (Wave B.1/B.2/C.1 pattern).
          mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
        });
      });

      test('not-found state', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        await page.goto(`/players/${FIXTURE_PLAYER_ID}?state=not-found`, {
          waitUntil: 'domcontentloaded',
        });
        // Not-found shell: descriptive message + CTA back to /players
        await page.waitForSelector('[data-slot="player-detail-not-found"]', { timeout: 30_000 });
        await waitForFontsAndRaf(page);

        await expect(page).toHaveScreenshot(`player-detail-${viewport.name}-not-found.png`, {
          fullPage: true,
          animations: 'disabled',
          mask: [page.locator('[data-dynamic]')],
        });
      });
    });
  }

  // Note: error state is excluded from visual coverage because the error surface
  // depends on `statsQuery.isError` (TanStack Query network failure) which cannot
  // be reproduced deterministically via URL override. Error state is covered by
  // unit tests in `_components/__tests__/PlayerDetailView.test.tsx`.
});
