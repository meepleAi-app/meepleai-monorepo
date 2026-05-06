/**
 * V2 State Coverage — /players route (Issue #682, Wave 4 D1).
 *
 * Captures 4 surface states del 5-state FSM:
 *   `default | loading | empty | filtered-empty`
 *
 * Il quinto state `error` è escluso dalla coverage visual perché surface
 * dipendente da `statsQuery.isError` (TanStack Query), che richiede un mock
 * di network failure non riproducibile in modo deterministico via URL
 * override. Coperto invece dal unit test orchestratore
 * (`_components/__tests__/PlayersLibraryView.test.tsx`).
 *
 * Uses test-only `?state=...` query param, gated da
 * `NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD` in
 * `_components/PlayersLibraryView.tsx`.
 *
 * Test ID strategy:
 *   - In CI bootstrap workflow `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` è
 *     settato pre-build, attivando il fixture deterministico
 *     (`tryLoadVisualTestFixture()` da `@/lib/players/players-visual-test-fixture`).
 *   - `?state=empty` instructa il fixture a tornare `[]` invece di 5 entries;
 *     gli altri override sovrascrivono solo `effectiveKind`, mantenendo il
 *     fixture default.
 *   - Loading state usa il `data-slot="players-loading"` skeleton inline
 *     nell'orchestrator (NON EmptyPlayers — mirror Wave B.1 pattern). Gli
 *     altri stati terminali (empty/filtered-empty/error) usano EmptyPlayers
 *     con `data-slot="players-empty"` + `data-kind="..."`.
 *
 * Snapshots written to `apps/web/e2e/v2-states/players-index.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run visual-regression-migrated.yml --ref <branch> \
 *     -f mode=bootstrap -f project_filter=both`
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

async function waitForViewReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="players-library-view"]', { timeout: 30_000 });

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

test.describe('Players index — state coverage', () => {
  test.describe.configure({ retries: 0 });

  // ── Desktop ────────────────────────────────────────────────────────────────

  test('desktop default state', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/players', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    // Default state expects results grid populato (5 entries dal fixture).
    await expect(page.locator('[data-slot="players-results-grid"]')).toBeVisible();
    await expect(page).toHaveScreenshot('players-index-desktop-default.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('desktop loading state', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/players?state=loading', { waitUntil: 'domcontentloaded' });
    // Loading uses a distinct skeleton shell (data-slot="players-loading"),
    // not EmptyPlayers. Wait directly for the skeleton wrapper.
    await page.waitForSelector('[data-slot="players-loading"]', { timeout: 30_000 });
    await expect(page.locator('[data-slot="players-loading"]')).toBeVisible();
    await expect(page).toHaveScreenshot('players-index-desktop-loading.png', {
      fullPage: true,
      animations: 'disabled',
      // Skeleton pulse animations — mask to avoid flake (mirror Wave A.4/B.1/B.2 pattern).
      mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
    });
  });

  test('desktop empty state (no players)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/players?state=empty', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    // `?state=empty` triggera `tryLoadVisualTestFixture('empty')` → 0 entries.
    // EmptyPlayers rendere il default empty kind.
    await expect(page.locator('[data-slot="players-empty"][data-kind="empty"]')).toBeVisible();
    await expect(page).toHaveScreenshot('players-index-desktop-empty.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('desktop filtered-empty state (filters yield no results)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/players?state=filtered-empty', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    // 5 entries underlying ma effectiveKind override → filtered-empty surface
    // con CTA clearFilters (reset search).
    await expect(
      page.locator('[data-slot="players-empty"][data-kind="filtered-empty"]')
    ).toBeVisible();
    await expect(page).toHaveScreenshot('players-index-desktop-filtered-empty.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  // ── Mobile ─────────────────────────────────────────────────────────────────

  test('mobile default state', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/players', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    await expect(page.locator('[data-slot="players-results-grid"]')).toBeVisible();
    await expect(page).toHaveScreenshot('players-index-mobile-default.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('mobile loading state', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/players?state=loading', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="players-loading"]', { timeout: 30_000 });
    await expect(page.locator('[data-slot="players-loading"]')).toBeVisible();
    await expect(page).toHaveScreenshot('players-index-mobile-loading.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
    });
  });

  test('mobile empty state (no players)', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/players?state=empty', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    await expect(page.locator('[data-slot="players-empty"][data-kind="empty"]')).toBeVisible();
    await expect(page).toHaveScreenshot('players-index-mobile-empty.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('mobile filtered-empty state (filters yield no results)', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/players?state=filtered-empty', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    await expect(
      page.locator('[data-slot="players-empty"][data-kind="filtered-empty"]')
    ).toBeVisible();
    await expect(page).toHaveScreenshot('players-index-mobile-filtered-empty.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  // Note: desktop-error and mobile-error states are excluded from visual
  // coverage because the error surface depends on `statsQuery.isError`
  // (TanStack Query network failure) which cannot be reproduced deterministically
  // via URL override. Error state is covered by unit tests in
  // `_components/__tests__/PlayersLibraryView.test.tsx`.
});
