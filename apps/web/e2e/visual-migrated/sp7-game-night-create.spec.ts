/**
 * Visual contract — /game-nights/new wizard vs mockup baseline.
 *
 * Issue #950 (W4 — SP7 game-night-create) · spec §13 (test strategy).
 *
 * Strategy mirrors Wave C.2 sp4-agent-detail.spec.ts:
 *   - The orchestrator at /game-nights/new reads `?fixture=<state-id>` and,
 *     when `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` is baked into the
 *     build, substitutes the reducer initial state with a deterministic
 *     `WizardState` from `lib/game-nights/wizard-fixture.ts`.
 *   - In production deploys the fixture is dead code (constant-fold).
 *
 * 10 FSM states (spec §4 inventory):
 *   step1-date, step1-warning, step2-location, step3-empty, step3-typing,
 *   step3-filled, step4-games, step4-decide-group, mobile-step-flow,
 *   desktop-split
 *
 * Bootstrap baseline (one-time, post-route-swap):
 *   gh workflow run visual-regression-migrated.yml \
 *     --ref feature/issue-950-week4-e2e \
 *     -f mode=bootstrap -f project_filter=both
 */

import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const SLUG = 'sp7-game-night-create';

/**
 * Fixture identifiers from `lib/game-nights/wizard-fixture.ts` —
 * `WizardFixtureState`. Order mirrors spec §4 FSM table.
 *
 * Subset captured here: only the desktop-suitable states ship as
 * desktop snapshots; mobile snapshots use the mobile-step-flow + a
 * subset of stable single-step shots.
 */
const FIXTURE_STATES_DESKTOP = [
  'step1-date',
  'step1-warning',
  'step2-location',
  'step3-empty',
  'step3-filled',
  'step4-games',
  'step4-decide-group',
  'desktop-split',
] as const;

const FIXTURE_STATES_MOBILE = [
  'step1-date',
  'step1-warning',
  'step3-filled',
  'step4-decide-group',
  'mobile-step-flow',
] as const;

async function waitForWizardReady(page: Page): Promise<void> {
  // Wait for the wizard root + stepper. The orchestrator hydrates the
  // reducer with the fixture before the first paint when ?fixture= is set,
  // so we don't need additional readiness signals beyond the DOM mount.
  await page.waitForSelector('[data-slot="game-night-create-wizard"]', { timeout: 30_000 });
  await page.waitForSelector('[data-slot="game-night-create-stepper"]', { timeout: 10_000 });

  // Fonts + double RAF for stable screenshots.
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

test.describe('V2 Visual Migrated — /game-nights/new wizard matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  for (const state of FIXTURE_STATES_DESKTOP) {
    test(`Wizard desktop 1280x720 — state=${state}`, async ({ page }) => {
      await page.setViewportSize({ width: 1280, height: 720 });
      await seedAuthSession(page);
      await seedCookieConsent(page);
      await mockAuthEndpoints(page);
      await page.goto(`/game-nights/new?fixture=${state}`, { waitUntil: 'domcontentloaded' });
      await waitForWizardReady(page);

      await expect(page).toHaveScreenshot(`${SLUG}-${state}-desktop.png`, {
        fullPage: true,
        animations: 'disabled',
        mask: [page.locator('[data-dynamic]')],
      });
    });
  }

  for (const state of FIXTURE_STATES_MOBILE) {
    test(`Wizard mobile 375x812 — state=${state}`, async ({ page }) => {
      await page.setViewportSize({ width: 375, height: 812 });
      await seedAuthSession(page);
      await seedCookieConsent(page);
      await mockAuthEndpoints(page);
      await page.goto(`/game-nights/new?fixture=${state}`, { waitUntil: 'domcontentloaded' });
      await waitForWizardReady(page);

      await expect(page).toHaveScreenshot(`${SLUG}-${state}-mobile.png`, {
        fullPage: true,
        animations: 'disabled',
        mask: [page.locator('[data-dynamic]')],
      });
    });
  }
});
