/**
 * V2 State Coverage — /games/[id] desktop route (Issue #581, Wave C.1).
 *
 * Captures 3 surface states del 5-state FSM:
 *   `default | loading | not-found`
 *
 * `error` è escluso dalla coverage visual perché surface dipendente da
 * `detailQuery.isError` (TanStack Query), che richiede un mock di network
 * failure non riproducibile in modo deterministico via URL override. Coperto
 * invece dal unit test orchestratore
 * (`_components/__tests__/GameDetailViewV2.test.tsx`) che esercita l'override
 * `?state=error` direttamente.
 *
 * `empty` è alias di `not-found` per contract sez. 3 — non ha un visual
 * baseline distinto (same shell renders for both).
 *
 * Uses test-only `?state=...` query param, gated da
 * `NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD` in
 * `_components/GameDetailViewV2.tsx`.
 *
 * data-slot selectors (actual shells from committed orchestrator Task 3):
 *   - default:   [data-slot="game-detail-view"]
 *   - loading:   [data-slot="game-detail-loading"]
 *   - error:     [data-slot="game-detail-error"]
 *   - not-found: [data-slot="game-detail-not-found"]
 *
 * Snapshots written to `apps/web/e2e/v2-states/game-detail.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run visual-regression-migrated.yml \
 *     --ref feature/issue-581-wave-c1-game-detail-fe-v2-retry \
 *     -f mode=bootstrap -f project_filter=both`
 *
 * Cherry-picked from PR #697 commit 30d48a26e — data-slot selectors updated to
 * match Task 2/3 committed components (separate shells per FSM state, NOT a
 * single data-state wrapper). networkidle anti-pattern replaced with
 * domcontentloaded + explicit waitForSelector (Wave B.1 lesson).
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const GAME_ID = '00000000-0000-4000-8000-000000000581';

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

test.describe('Game detail — state coverage', () => {
  test.describe.configure({ retries: 0 });

  for (const viewport of VIEWPORTS) {
    test.describe(`${viewport.name} ${viewport.width}x${viewport.height}`, () => {
      test('default state', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        await page.goto(`/games/${GAME_ID}`, { waitUntil: 'domcontentloaded' });
        // Default state: root shell + hero + tablist all rendered
        await page.waitForSelector('[data-slot="game-detail-view"]', { timeout: 30_000 });
        await page.waitForSelector('[data-slot="game-detail-hero"]', { timeout: 10_000 });
        await waitForFontsAndRaf(page);

        await expect(page).toHaveScreenshot(`game-detail-${viewport.name}-default.png`, {
          fullPage: true,
          animations: 'disabled',
          mask: [page.locator('[data-dynamic]')],
        });
      });

      test('loading state', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        await page.goto(`/games/${GAME_ID}?state=loading`, { waitUntil: 'domcontentloaded' });
        // Loading state: separate shell with animate-pulse skeletons
        await page.waitForSelector('[data-slot="game-detail-loading"]', { timeout: 30_000 });
        await waitForFontsAndRaf(page);

        await expect(page).toHaveScreenshot(`game-detail-${viewport.name}-loading.png`, {
          fullPage: true,
          animations: 'disabled',
          // Skeleton pulse animations — mask to avoid flake (Wave B.1/B.2 pattern).
          mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
        });
      });

      test('not-found state', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        await page.goto(`/games/${GAME_ID}?state=not-found`, { waitUntil: 'domcontentloaded' });
        // Not-found state: CTA shell with back-to-games link
        await page.waitForSelector('[data-slot="game-detail-not-found"]', { timeout: 30_000 });
        await waitForFontsAndRaf(page);

        await expect(page).toHaveScreenshot(`game-detail-${viewport.name}-not-found.png`, {
          fullPage: true,
          animations: 'disabled',
          mask: [page.locator('[data-dynamic]')],
        });
      });

      test('empty state (alias → not-found shell)', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        await page.goto(`/games/${GAME_ID}?state=empty`, { waitUntil: 'domcontentloaded' });
        // `empty` maps to `not-found` per contract sez. 3 — same shell renders
        await page.waitForSelector('[data-slot="game-detail-not-found"]', { timeout: 30_000 });
        await waitForFontsAndRaf(page);

        await expect(page).toHaveScreenshot(`game-detail-${viewport.name}-empty.png`, {
          fullPage: true,
          animations: 'disabled',
          mask: [page.locator('[data-dynamic]')],
        });
      });
    });
  }
});
