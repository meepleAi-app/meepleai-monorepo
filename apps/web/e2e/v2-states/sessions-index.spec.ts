/**
 * V2 State Coverage — /sessions route (Issue #735, Wave D.1).
 *
 * Captures 4 surface states del 5-state FSM:
 *   `default | loading | empty | filtered-empty`
 *
 * Il quinto state `error` è escluso dalla coverage visual perché surface
 * dipendente da `sessionsQuery.isError` (TanStack Query), che richiede un mock
 * di network failure non riproducibile in modo deterministico via URL
 * override. Coperto invece dal unit test orchestratore
 * (`_components/__tests__/SessionsLibraryView.test.tsx`).
 *
 * Uses test-only `?state=...` query param, gated da
 * `NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD` in
 * `lib/sessions/sessions-visual-test-fixture.ts`.
 *
 * Test ID strategy:
 *   - In CI bootstrap workflow `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` è
 *     settato pre-build, attivando il fixture deterministico
 *     (`VISUAL_TEST_FIXTURE_SESSIONS` da `@/lib/sessions/sessions-visual-test-fixture`).
 *   - `?state=empty` instructa il fixture a tornare `[]` invece di 6 entries
 *     E imposta effectiveKind='empty' → EmptySessions con kind='empty';
 *     gli altri override sovrascrivono solo `effectiveKind`, mantenendo il
 *     fixture default.
 *   - Loading state usa il `data-slot="sessions-empty-loading"` skeleton inline
 *     nell'EmptySessions component (kind='loading'). Gli altri stati terminali
 *     (empty/filtered-empty/error) usano EmptySessions con
 *     `data-slot="sessions-empty-{kind}"`.
 *
 * Snapshots written to `apps/web/e2e/v2-states/sessions-index.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run visual-regression-migrated.yml --ref <branch> \
 *     -f mode=bootstrap -f project_filter=both`
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

async function waitForViewReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="sessions-library-view"]', { timeout: 30_000 });

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

test.describe('Sessions index — state coverage', () => {
  test.describe.configure({ retries: 0 });

  // ── Desktop ────────────────────────────────────────────────────────────────

  test('desktop default state', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/sessions', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    // Default state expects hero + filters visible (list view populated with fixture entries).
    await expect(page.locator('[data-slot="sessions-hero"]')).toBeVisible();
    await expect(page.locator('[data-slot="sessions-filters"]')).toBeVisible();
    await expect(page).toHaveScreenshot('sessions-index-desktop-default.png', {
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
    await page.goto('/sessions?state=loading', { waitUntil: 'domcontentloaded' });
    // Loading uses EmptySessions with kind='loading' (data-slot="sessions-empty-loading").
    await page.waitForSelector('[data-slot="sessions-empty-loading"]', { timeout: 30_000 });
    await expect(page.locator('[data-slot="sessions-empty-loading"]')).toBeVisible();
    await expect(page).toHaveScreenshot('sessions-index-desktop-loading.png', {
      fullPage: true,
      animations: 'disabled',
      // Skeleton pulse animations — mask to avoid flake (mirror Wave A.4/B.1/B.2 pattern).
      mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
    });
  });

  test('desktop empty state (no sessions)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    // ?state=empty: stateOverride='empty' → fixture=EMPTY_ARRAY + effectiveKind='empty'
    // → EmptySessions renders with kind='empty' (data-slot="sessions-empty-empty").
    await page.goto('/sessions?state=empty', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    await expect(page.locator('[data-slot="sessions-empty-empty"]')).toBeVisible();
    await expect(page).toHaveScreenshot('sessions-index-desktop-empty.png', {
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
    await page.goto('/sessions?state=filtered-empty', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    // 6 entries underlying but effectiveKind override → filtered-empty surface
    // con CTA clearFilters (reset search/status).
    await expect(page.locator('[data-slot="sessions-empty-filtered-empty"]')).toBeVisible();
    await expect(page).toHaveScreenshot('sessions-index-desktop-filtered-empty.png', {
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
    await page.goto('/sessions', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    await expect(page.locator('[data-slot="sessions-hero"]')).toBeVisible();
    await expect(page.locator('[data-slot="sessions-filters"]')).toBeVisible();
    await expect(page).toHaveScreenshot('sessions-index-mobile-default.png', {
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
    await page.goto('/sessions?state=loading', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="sessions-empty-loading"]', { timeout: 30_000 });
    await expect(page.locator('[data-slot="sessions-empty-loading"]')).toBeVisible();
    await expect(page).toHaveScreenshot('sessions-index-mobile-loading.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
    });
  });

  test('mobile empty state (no sessions)', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/sessions?state=empty', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    await expect(page.locator('[data-slot="sessions-empty-empty"]')).toBeVisible();
    await expect(page).toHaveScreenshot('sessions-index-mobile-empty.png', {
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
    await page.goto('/sessions?state=filtered-empty', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    await expect(page.locator('[data-slot="sessions-empty-filtered-empty"]')).toBeVisible();
    await expect(page).toHaveScreenshot('sessions-index-mobile-filtered-empty.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  // Note: desktop-error and mobile-error states are excluded from visual
  // coverage because the error surface depends on `sessionsQuery.isError`
  // (TanStack Query network failure) which cannot be reproduced deterministically
  // via URL override. Error state is covered by unit tests in
  // `_components/__tests__/SessionsLibraryView.test.tsx`.
});
