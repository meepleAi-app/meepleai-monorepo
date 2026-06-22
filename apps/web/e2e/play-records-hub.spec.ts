/**
 * E2E — Play Records hub URL filter persistence (#2347).
 *
 * Covers the 4 scenarios locked in spec
 * `docs/superpowers/specs/2026-06-17-issue-2347-play-records-url-filter-persistence.md`
 * Acceptance §Testing.
 *
 * Also runs axe AA smoke on default + filter-empty states (DEC-6).
 *
 * Auth pattern: seedCookieConsent + seedAuthSession + mockAuthEndpoints
 * (matches asse-b-drawer-stack-flow.spec.ts, asse-d-p1-polymorphic-scoring.spec.ts).
 *
 * Selector contracts (verified against RecordFilters.tsx:85, PlayHistory.tsx:116-199):
 *   - `data-testid="filter-status-all"` → "Tutte" chip (STATUS_OPTIONS value:'all')
 *   - `data-testid="filter-status-{value}"` → per-status chip (Completed, InProgress, Planned…)
 *   - `data-testid="play-history"` → list wrapper
 *   - `data-testid="play-history-empty-filter"` → empty state when filter yields 0 results
 */

import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

const HUB = '/play-records';

test.describe('Play Records hub — URL filter persistence', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  test.beforeEach(async ({ page }) => {
    await seedCookieConsent(page);
    await seedAuthSession(page);
    await mockAuthEndpoints(page);
    await page.goto(HUB);
    await page.waitForLoadState('networkidle');
  });

  test('Scenario 1 — Default mount: "Tutte" chip pressed, URL clean', async ({ page }) => {
    // No ?status= param on default mount
    await expect(page).toHaveURL(/\/play-records(?:\?tab=\w+)?$/);
    const allChip = page.getByTestId('filter-status-all');
    await expect(allChip).toHaveAttribute('aria-pressed', 'true');
  });

  test('Scenario 2 — Filter chip click → URL updates', async ({ page }) => {
    await page.getByTestId('filter-status-Completed').click();
    await expect(page).toHaveURL(/\?status=Completed/);
    await expect(page.getByTestId('filter-status-Completed')).toHaveAttribute(
      'aria-pressed',
      'true'
    );
  });

  test('Scenario 3 — Deep-link ?status=InProgress: chip pre-pressed', async ({ page }) => {
    await page.goto(`${HUB}?status=InProgress`);
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('filter-status-InProgress')).toHaveAttribute(
      'aria-pressed',
      'true'
    );
  });

  test('Scenario 4 — Empty filter state retry: reset chip restores URL', async ({ page }) => {
    await page.goto(`${HUB}?status=Planned`);
    await page.waitForLoadState('networkidle');

    // Wait for either the empty state OR records list — whichever the fixture provides.
    const emptyState = page.getByTestId('play-history-empty-filter');
    const recordsList = page.getByTestId('play-history').locator('[data-testid^="play-record-"]');
    await Promise.race([
      emptyState.waitFor({ state: 'visible', timeout: 5_000 }).catch(() => null),
      recordsList
        .first()
        .waitFor({ state: 'visible', timeout: 5_000 })
        .catch(() => null),
    ]);

    if (await emptyState.isVisible()) {
      // Click the reset CTA inside the empty state
      const resetCta = emptyState.getByRole('button');
      await resetCta.click();
      await expect(page).toHaveURL(/\/play-records$/);
      await expect(page.getByTestId('filter-status-all')).toHaveAttribute('aria-pressed', 'true');
    } else {
      // Fixture happened to have Planned records — verify default chip-click reset still works
      await page.getByTestId('filter-status-all').click();
      await expect(page).toHaveURL(/\/play-records$/);
    }
  });
});

test.describe('Play Records hub — axe AA smoke', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  test.beforeEach(async ({ page }) => {
    await seedCookieConsent(page);
    await seedAuthSession(page);
    await mockAuthEndpoints(page);
  });

  test('axe AA: default state has 0 violations', async ({ page }) => {
    await page.goto(HUB);
    await page.waitForLoadState('networkidle');
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations).toEqual([]);
  });

  test('axe AA: filter-empty state has 0 violations', async ({ page }) => {
    await page.goto(`${HUB}?status=Planned`);
    await page.waitForLoadState('networkidle');
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations).toEqual([]);
  });
});
