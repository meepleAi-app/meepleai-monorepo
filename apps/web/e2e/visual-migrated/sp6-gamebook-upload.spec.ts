/**
 * Visual contract — /gamebook/upload Tier L wizard vs sp6 mockup baselines.
 *
 * Issue #789 (SP6 Phase C.1.D Foundation sub-PR — Tier L 3-step wizard).
 *
 * Coverage in this spec (Foundation scope):
 *   - Step 1 default fixture × { desktop 1280×800, mobile 375×667 } = 2 PNG
 *   - Step 2 ready (camera placeholder) × desktop only = 1 PNG
 *   - Step 3 progress (indexing placeholder) × desktop only = 1 PNG
 *
 * Step 2 / Step 3 visuals here render the **placeholder** orchestrator output
 * (Step2Placeholder / Step3Placeholder). Real CameraViewfinder + PageThumb
 * baselines are deferred to Interactions sub-PR (per contract §14 Foundation
 * vs Interactions split). Mobile variants for steps 2/3 are unit-tested
 * single-tree responsive (Wave B.3 pattern); FSM-cell variants live in
 * `e2e/v2-states/gamebook-upload.spec.ts`.
 *
 * IS_VISUAL_TEST_BUILD=true baked into CI bootstrap build short-circuits any
 * stub hooks; orchestrator only resolves the FSM via `?fixture=` URL hatch
 * (Pattern P14 from Wave D.3 lesson — never falls back automatically).
 *
 * Bootstrap baseline (one-time, post-Task E):
 *   `gh workflow run visual-regression-migrated.yml \
 *     --ref feature/issue-789-upload-foundation \
 *     -f mode=bootstrap -f project_filter=both`
 *
 * Auth bypass:
 *   `/gamebook/upload` is under `(authenticated)/`. `PLAYWRIGHT_AUTH_BYPASS=true`
 *   is set in `playwright.config.ts:434` (webServer env). Triple auth helper
 *   (`seedAuthSession + seedCookieConsent + mockAuthEndpoints`) satisfies the
 *   `proxy.ts` middleware gate + the client-side AuthProvider/useSessionCheck
 *   gates without requiring a real DB session.
 *
 * No `networkidle` — always `domcontentloaded` + explicit `waitForSelector`
 * (Wave B.1 lesson).
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const SLUG = 'sp6-gamebook-upload';

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function waitForReady(page: Page, expectedCell: string): Promise<void> {
  // Wait for orchestrator to mount in the expected FSM cell. `?fixture=` URL
  // hatch deterministically resolves the cell, short-circuiting stub hooks.
  await page.waitForSelector(
    `[data-slot="gamebook-upload-view"][data-ui-state="${expectedCell}"]`,
    { timeout: 30_000 }
  );

  // Confirm the wizard step indicator is mounted before screenshot.
  await page.waitForSelector('[data-slot="wizard-step-indicator"]', { timeout: 10_000 });

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

test.describe('V2 Visual Migrated — /gamebook/upload matches sp6 mockup', () => {
  test.describe.configure({ retries: 0 });

  // ── Step 1 default × desktop + mobile ──────────────────────────────────
  for (const viewport of [
    { name: 'desktop', width: 1280, height: 800 },
    { name: 'mobile', width: 375, height: 667 },
  ] as const) {
    test(`Step 1 ${viewport.name} ${viewport.width}x${viewport.height} — default state matches sp6-gamebook-upload mockup`, async ({
      page,
    }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await seedAuth(page);
      // EXPLICIT ?fixture=step1-default — orchestrator only short-circuits
      // with the explicit query string (Pattern P14 from Wave D.3 lesson).
      await page.goto('/gamebook/upload?fixture=step1-default', {
        waitUntil: 'domcontentloaded',
      });
      await waitForReady(page, 'step1-default');

      await expect(page).toHaveScreenshot(`${SLUG}-step1-${viewport.name}.png`, {
        fullPage: true,
        animations: 'disabled',
      });
    });
  }

  // ── Step 2 ready × desktop (placeholder camera UI for Foundation) ──────
  test('Step 2 desktop 1280x800 — ready state (camera placeholder)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await seedAuth(page);
    await page.goto('/gamebook/upload?fixture=step2-ready', { waitUntil: 'domcontentloaded' });
    await waitForReady(page, 'step2-ready');

    await expect(page).toHaveScreenshot(`${SLUG}-step2-desktop.png`, {
      fullPage: true,
      animations: 'disabled',
    });
  });

  // ── Step 3 progress × desktop (placeholder indexing UI for Foundation) ─
  test('Step 3 desktop 1280x800 — progress state (indexing placeholder)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await seedAuth(page);
    await page.goto('/gamebook/upload?fixture=step3-progress', { waitUntil: 'domcontentloaded' });
    await waitForReady(page, 'step3-progress');

    await expect(page).toHaveScreenshot(`${SLUG}-step3-desktop.png`, {
      fullPage: true,
      animations: 'disabled',
    });
  });
});
