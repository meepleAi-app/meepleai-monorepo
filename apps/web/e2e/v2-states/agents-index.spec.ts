/**
 * V2 State Coverage — /agents route (Issue #634, Wave B.2).
 *
 * Captures 4 surface states del 5-state FSM:
 *   `default | loading | empty | filtered-empty`
 *
 * Il quinto state `error` è escluso dalla coverage visual perché surface
 * dipendente da `agentsQuery.isError` (TanStack Query), che richiede un mock
 * di network failure non riproducibile in modo deterministico via URL
 * override. Coperto invece dal unit test orchestratore
 * (`_components/__tests__/AgentsLibraryView.test.tsx`).
 *
 * Uses test-only `?state=...` query param, gated da
 * `NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD` in
 * `_components/AgentsLibraryView.tsx`.
 *
 * Test ID strategy:
 *   - In CI bootstrap workflow `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` è
 *     settato pre-build, attivando il fixture deterministico
 *     (`tryLoadVisualTestFixture()` da `@/lib/agents/visual-test-fixture`).
 *   - `?state=empty` instructa il fixture a tornare `[]` invece di 6 entries;
 *     gli altri override sovrascrivono solo `effectiveKind`, mantenendo il
 *     fixture default.
 *
 * Snapshots written to `apps/web/e2e/v2-states/agents-index.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run visual-regression-migrated.yml --ref <branch> \
 *     -f mode=bootstrap -f project_filter=both`
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

async function waitForViewReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="agents-library-view"]', { timeout: 30_000 });

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

test.describe('Agents library — state coverage', () => {
  test.describe.configure({ retries: 0 });

  test('default state', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/agents', { waitUntil: 'domcontentloaded' });
    await waitForViewReady(page);
    // Default state expects results grid populato (6 entries dal fixture).
    await expect(page.locator('[data-slot="agents-results-grid"]')).toBeVisible();
    await expect(page).toHaveScreenshot('agents-index-default.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('loading state', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/agents?state=loading', {
      waitUntil: 'domcontentloaded',
    });
    await waitForViewReady(page);
    // Loading kind: EmptyAgents rendere skeleton con `data-kind="loading"`.
    await expect(
      page.locator('[data-slot="agents-empty-state"][data-kind="loading"]')
    ).toBeVisible();
    await expect(page).toHaveScreenshot('agents-index-loading.png', {
      fullPage: true,
      animations: 'disabled',
      // Skeleton pulse animations — mask to avoid flake (mirror Wave A.4 / B.1 pattern).
      mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
    });
  });

  test('empty state (no agents)', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/agents?state=empty', {
      waitUntil: 'domcontentloaded',
    });
    await waitForViewReady(page);
    // `?state=empty` triggera `tryLoadVisualTestFixture('empty')` → 0 entries.
    // EmptyAgents rendere il default empty kind (con CTA "Crea il tuo primo agente").
    await expect(page.locator('[data-slot="agents-empty-state"][data-kind="empty"]')).toBeVisible();
    await expect(page).toHaveScreenshot('agents-index-empty.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('filtered-empty state (filters yield no results)', async ({ page }) => {
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/agents?state=filtered-empty', {
      waitUntil: 'domcontentloaded',
    });
    await waitForViewReady(page);
    // 6 entries underlying ma effectiveKind override → filtered-empty surface
    // con CTA clearFilters (reset query+status, sort preserved).
    await expect(
      page.locator('[data-slot="agents-empty-state"][data-kind="filtered-empty"]')
    ).toBeVisible();
    await expect(page).toHaveScreenshot('agents-index-filtered-empty.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
