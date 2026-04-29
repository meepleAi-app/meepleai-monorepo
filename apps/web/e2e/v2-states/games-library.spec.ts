/**
 * V2 State Coverage — /games?tab=library route (Issue #633, Wave B.1).
 *
 * Captures 4 surface states del 5-state FSM:
 *   `default | loading | empty | filtered-empty`
 *
 * Il quinto state `error` è escluso dalla coverage visual perché surface
 * dipendente da `libraryQuery.isError` (TanStack Query), che richiede
 * un mock di network failure non riproducibile in modo deterministico
 * via URL override. Coperto invece dal unit test orchestratore
 * (`__tests__/GamesLibraryView.test.tsx`).
 *
 * Uses test-only `?state=...` query param, gated da
 * `NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD` in
 * `_components/GamesLibraryView.tsx`.
 *
 * Test ID strategy:
 *   - In CI bootstrap workflow `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` è
 *     settato pre-build, attivando il fixture deterministico
 *     (`tryLoadVisualTestFixture()` da `@/lib/games/visual-test-fixture`).
 *   - `?state=empty` instructa il fixture a tornare `[]` invece di
 *     5 entries; gli altri override sovrascrivono solo `effectiveKind`,
 *     mantenendo il fixture default.
 *
 * Snapshots written to `apps/web/e2e/v2-states/games-library.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run 266963272 --ref <branch> -f mode=bootstrap -f project_filter=both`
 */
import { test, expect, type Page } from '@playwright/test';

import { seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

async function waitForViewReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="games-library-view"]', { timeout: 30_000 });

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

test.describe('Games library — state coverage', () => {
  test.describe.configure({ retries: 0 });

  test('default state', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await page.goto('/games?tab=library', { waitUntil: 'networkidle' });
    await waitForViewReady(page);
    // Default state expects results grid populato (5 entries dal fixture).
    await expect(page.locator('[data-slot="games-results-grid"]')).toBeVisible();
    await expect(page).toHaveScreenshot('games-library-default.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('loading state', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await page.goto('/games?tab=library&state=loading', {
      waitUntil: 'networkidle',
    });
    await waitForViewReady(page);
    // Loading kind: GamesEmptyState rendere skeleton con `data-kind="loading"`.
    await expect(
      page.locator('[data-slot="games-empty-state"][data-kind="loading"]')
    ).toBeVisible();
    await expect(page).toHaveScreenshot('games-library-loading.png', {
      fullPage: true,
      animations: 'disabled',
      // Skeleton pulse animations — mask to avoid flake (mirror Wave A.4 pattern).
      mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
    });
  });

  test('empty state (no library entries)', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await page.goto('/games?tab=library&state=empty', {
      waitUntil: 'networkidle',
    });
    await waitForViewReady(page);
    // `?state=empty` triggera `tryLoadVisualTestFixture('empty')` → 0 entries.
    // GamesEmptyState rendere il library empty kind (con CTA "Aggiungi gioco").
    await expect(page.locator('[data-slot="games-empty-state"][data-kind="empty"]')).toBeVisible();
    await expect(page).toHaveScreenshot('games-library-empty.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('filtered-empty state (filters yield no results)', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await page.goto('/games?tab=library&state=filtered-empty', {
      waitUntil: 'networkidle',
    });
    await waitForViewReady(page);
    // 5 entries underlying ma effectiveKind override → filtered-empty surface
    // con CTA clearFilters (reset query+status).
    await expect(
      page.locator('[data-slot="games-empty-state"][data-kind="filtered-empty"]')
    ).toBeVisible();
    await expect(page).toHaveScreenshot('games-library-filtered-empty.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
