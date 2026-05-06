/**
 * V2 State Coverage — /gamebook/upload Tier L wizard (Issue #789, SP6 Phase
 * C.1.D Foundation sub-PR).
 *
 * Captures fixture-driven visual + state variants of the 14-cell FSM. Per
 * Phase 0.5 contract §14, this spec contributes the fixture-cell desktop
 * baselines beyond the mockup-parity defaults in
 * `e2e/visual-migrated/sp6-gamebook-upload.spec.ts`:
 *
 *   - `step1-no-results` (desktop) — 3 ActionCards (create / search BGG /
 *     add private)
 *   - `step1-bgg-loading` (desktop) — spinner + skeleton
 *   - `step2-low-light` (desktop) — placeholder; real low-light pulse
 *     deferred to Interactions
 *   - `step2-failed` (desktop) — placeholder; real detection-failed retake
 *     deferred to Interactions
 *   - `step2-denied` (desktop) — permission-denied placeholder (no real
 *     camera in Foundation, per contract §14)
 *   - `step3-partial` (desktop) — placeholder; real 3 retake CTAs deferred
 *     to Interactions
 *   - `step3-offline` (desktop) — placeholder; real banner + retry
 *     countdown deferred to Interactions
 *   - `step3-cancel-modal` (desktop) — placeholder modal overlay; modal
 *     focus trap deferred to a11y spec; visually excluded per contract §14
 *     ("modal overlay covered via component unit test + a11y E2E focus
 *     trap"). This spec asserts the FSM cell renders w/o screenshot.
 *
 * Mobile variants for steps 2/3 are unit-tested single-tree responsive
 * (Wave B.3 pattern).
 *
 * URL override hatch (`?fixture=…`):
 *   Gated by `STATE_OVERRIDE_ENABLED` (`IS_VISUAL_TEST_BUILD ||
 *   NODE_ENV !== 'production'`). In production builds the bundler dead-code-
 *   eliminates the lookup so this hatch is a build-time concern only.
 *
 * Auth bypass: triple helper (seedAuthSession + seedCookieConsent +
 * mockAuthEndpoints) — Wave B.1 pattern (Issue #633) for `(authenticated)`
 * routes. No `networkidle` (Wave B.1 lesson) — `domcontentloaded` +
 * explicit `waitForSelector`.
 *
 * Snapshots written to
 * `apps/web/e2e/v2-states/gamebook-upload.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run visual-regression-migrated.yml --ref <branch> \
 *     -f mode=bootstrap -f project_filter=both`
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function gotoUpload(page: Page, fixture: string): Promise<void> {
  await seedAuth(page);
  await page.goto(`/gamebook/upload?fixture=${fixture}`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('[data-slot="gamebook-upload-view"]', { timeout: 30_000 });
}

test.describe('Gamebook upload — state coverage', () => {
  test.describe.configure({ retries: 0 });

  // ── Step 1 variants ────────────────────────────────────────────────────
  test('desktop step1-no-results (3 ActionCards)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await gotoUpload(page, 'step1-no-results');
    await expect(
      page.locator('[data-slot="gamebook-upload-view"][data-ui-state="step1-no-results"]')
    ).toBeVisible();
    // NoResultsPanel hosts the 3 ActionCards; assert it is rendered.
    await expect(page.locator('[data-slot="step1-results"]')).toBeVisible();

    await expect(page).toHaveScreenshot('gamebook-upload-step1-no-results-desktop.png', {
      fullPage: true,
      animations: 'disabled',
    });
  });

  test('desktop step1-bgg-loading (spinner + skeleton)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await gotoUpload(page, 'step1-bgg-loading');
    await expect(
      page.locator('[data-slot="gamebook-upload-view"][data-ui-state="step1-bgg-loading"]')
    ).toBeVisible();
    await expect(page.locator('[data-slot="bgg-loading-spinner"]')).toBeVisible();

    await expect(page).toHaveScreenshot('gamebook-upload-step1-bgg-loading-desktop.png', {
      fullPage: true,
      animations: 'disabled',
    });
  });

  // ── Step 2 variants (all rendered as Step2Placeholder in Foundation) ───
  test('desktop step2-low-light (placeholder)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await gotoUpload(page, 'step2-low-light');
    await expect(
      page.locator('[data-slot="gamebook-upload-view"][data-ui-state="step2-low-light"]')
    ).toBeVisible();
    await expect(
      page.locator('[data-slot="step2-placeholder"][data-cell="step2-low-light"]')
    ).toBeVisible();

    await expect(page).toHaveScreenshot('gamebook-upload-step2-low-light-desktop.png', {
      fullPage: true,
      animations: 'disabled',
    });
  });

  test('desktop step2-failed (placeholder)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await gotoUpload(page, 'step2-failed');
    await expect(
      page.locator('[data-slot="gamebook-upload-view"][data-ui-state="step2-failed"]')
    ).toBeVisible();
    await expect(
      page.locator('[data-slot="step2-placeholder"][data-cell="step2-failed"]')
    ).toBeVisible();

    await expect(page).toHaveScreenshot('gamebook-upload-step2-failed-desktop.png', {
      fullPage: true,
      animations: 'disabled',
    });
  });

  test('desktop step2-denied (permission denied placeholder)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await gotoUpload(page, 'step2-denied');
    await expect(
      page.locator('[data-slot="gamebook-upload-view"][data-ui-state="step2-denied"]')
    ).toBeVisible();
    await expect(
      page.locator('[data-slot="step2-placeholder"][data-cell="step2-denied"]')
    ).toBeVisible();

    await expect(page).toHaveScreenshot('gamebook-upload-step2-denied-desktop.png', {
      fullPage: true,
      animations: 'disabled',
    });
  });

  // ── Step 3 variants (all rendered as Step3Placeholder in Foundation) ───
  test('desktop step3-partial (placeholder)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await gotoUpload(page, 'step3-partial');
    await expect(
      page.locator('[data-slot="gamebook-upload-view"][data-ui-state="step3-partial"]')
    ).toBeVisible();
    await expect(
      page.locator('[data-slot="step3-placeholder"][data-cell="step3-partial"]')
    ).toBeVisible();

    await expect(page).toHaveScreenshot('gamebook-upload-step3-partial-desktop.png', {
      fullPage: true,
      animations: 'disabled',
    });
  });

  test('desktop step3-offline (placeholder)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await gotoUpload(page, 'step3-offline');
    await expect(
      page.locator('[data-slot="gamebook-upload-view"][data-ui-state="step3-offline"]')
    ).toBeVisible();
    await expect(
      page.locator('[data-slot="step3-placeholder"][data-cell="step3-offline"]')
    ).toBeVisible();

    await expect(page).toHaveScreenshot('gamebook-upload-step3-offline-desktop.png', {
      fullPage: true,
      animations: 'disabled',
    });
  });

  // ── Step 3 cancel modal (state assertion only — visual excluded per
  //    contract §14: "modal overlay covered via component unit test +
  //    a11y E2E focus trap"). This test guards against FSM regressions; the
  //    modal's accessible-name + role contract lives in the a11y spec.
  test('desktop step3-cancel-modal (state assertion, visual excluded)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await gotoUpload(page, 'step3-cancel-modal');
    await expect(
      page.locator('[data-slot="gamebook-upload-view"][data-ui-state="step3-cancel-modal"]')
    ).toBeVisible();
    await expect(page.locator('[data-slot="cancel-modal-placeholder"]')).toBeVisible();
    await expect(page.locator('[data-slot="cancel-modal-placeholder"]')).toHaveAttribute(
      'role',
      'dialog'
    );
    await expect(page.locator('[data-slot="cancel-modal-placeholder"]')).toHaveAttribute(
      'aria-modal',
      'true'
    );
  });
});
