/**
 * V2 State Coverage — /library desktop route (Issue #574, Wave B.3).
 *
 * Captures 4 surface states del 5-state FSM:
 *   `default | loading | empty | filtered-empty`
 *
 * Il quinto state `error` è escluso dalla coverage visual perché surface
 * dipendente da `libraryQuery.isError` (TanStack Query), che richiede un mock
 * di network failure non riproducibile in modo deterministico via URL
 * override. Coperto invece dal unit test orchestratore
 * (`_components/__tests__/LibraryHubV2.test.tsx`) che esercita l'override
 * `?state=error` direttamente.
 *
 * Uses test-only `?state=...` query param, gated da
 * `NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD` in
 * `_components/LibraryHubV2.tsx`.
 *
 * Test ID strategy:
 *   - In CI bootstrap workflow `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` è
 *     settato pre-build, attivando il fixture deterministico
 *     (`tryLoadVisualTestFixture()` da `@/lib/library/visual-test-fixture`).
 *   - `?state=empty` instructa il fixture a tornare `[]` invece di 12 entries;
 *     gli altri override (`loading`, `filtered-empty`) sovrascrivono solo
 *     `effectiveKind` mantenendo il fixture default per coerenza con i contatori
 *     dei tab.
 *
 * Snapshots written to `apps/web/e2e/v2-states/library.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run visual-regression-migrated.yml --ref <branch> \
 *     -f mode=bootstrap -f project_filter=both`
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

async function waitForViewReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="library-hub-v2"]', { timeout: 30_000 });

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

test.describe('Library desktop — state coverage', () => {
  test.describe.configure({ retries: 0 });

  test('default state', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/library', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    // Default state expects grid populato (12 entries dal fixture).
    await expect(page.locator('[data-slot="library-hybrid-grid"]')).toBeVisible();
    await expect(page).toHaveScreenshot('library-default.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('loading state', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/library?state=loading', {
      waitUntil: 'domcontentloaded',
    });
    await waitForViewReady(page);
    // Loading kind: EmptyLibrary rendere skeleton con `data-kind="loading"`.
    await expect(
      page.locator('[data-slot="library-empty-state"][data-kind="loading"]')
    ).toBeVisible();
    await expect(page).toHaveScreenshot('library-loading.png', {
      fullPage: true,
      animations: 'disabled',
      // Skeleton pulse animations — mask to avoid flake (mirror Wave A.4 / B.1 / B.2 pattern).
      mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
    });
  });

  test('empty state (no library entries)', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/library?state=empty', {
      waitUntil: 'domcontentloaded',
    });
    await waitForViewReady(page);
    // `?state=empty` triggera `tryLoadVisualTestFixture('empty')` → 0 entries.
    // EmptyLibrary rendere il default empty kind (con CTA "Aggiungi gioco").
    await expect(
      page.locator('[data-slot="library-empty-state"][data-kind="empty"]')
    ).toBeVisible();
    await expect(page).toHaveScreenshot('library-empty.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('filtered-empty state (filters yield no results)', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/library?state=filtered-empty', {
      waitUntil: 'domcontentloaded',
    });
    await waitForViewReady(page);
    // 12 entries underlying ma effectiveKind override → filtered-empty surface
    // con CTA clearFilters (reset query+tab, navigates away from override URL).
    await expect(
      page.locator('[data-slot="library-empty-state"][data-kind="filtered-empty"]')
    ).toBeVisible();
    await expect(page).toHaveScreenshot('library-filtered-empty.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
