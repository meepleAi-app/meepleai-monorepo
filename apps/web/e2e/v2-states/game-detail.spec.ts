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
 * `?state=error` direttamente. `empty` è alias di `not-found` per cui non
 * abbiamo un visual baseline distinto.
 *
 * Uses test-only `?state=...` query param, gated da
 * `NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD` in
 * `_components/GameDetailViewV2.tsx`.
 *
 * Snapshots written to `apps/web/e2e/v2-states/game-detail.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run visual-regression-migrated.yml --ref <branch> \
 *     -f mode=bootstrap -f project_filter=both`
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const GAME_ID = '00000000-0000-4000-8000-000000000581';

async function waitForViewReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="game-detail-view"]', { timeout: 30_000 });

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

test.describe('Game detail desktop — state coverage', () => {
  test.describe.configure({ retries: 0 });

  test('default state', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto(`/games/${GAME_ID}`, { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    await expect(
      page.locator('[data-slot="game-detail-view"][data-state="default"]')
    ).toBeVisible();
    await expect(page.locator('[data-slot="game-detail-hero"]')).toBeVisible();
    await expect(page).toHaveScreenshot('game-detail-default.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('loading state', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto(`/games/${GAME_ID}?state=loading`, {
      waitUntil: 'domcontentloaded',
    });
    await waitForViewReady(page);
    await expect(
      page.locator('[data-slot="game-detail-view"][data-state="loading"]')
    ).toBeVisible();
    await expect(page).toHaveScreenshot('game-detail-loading.png', {
      fullPage: true,
      animations: 'disabled',
      // Skeleton pulse animations — mask to avoid flake.
      mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
    });
  });

  test('not-found state', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto(`/games/${GAME_ID}?state=not-found`, {
      waitUntil: 'domcontentloaded',
    });
    await waitForViewReady(page);
    await expect(
      page.locator('[data-slot="game-detail-view"][data-state="not-found"]')
    ).toBeVisible();
    await expect(page).toHaveScreenshot('game-detail-not-found.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
