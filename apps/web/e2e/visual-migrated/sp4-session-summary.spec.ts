/**
 * Visual contract — /sessions/[id] summary route vs mockup baseline.
 *
 * Issue #756 (Wave D.3 brownfield migration)
 * V2 Migration Phase 1 (Wave D.3).
 *
 * Coverage in this spec: 2 viewports × default fixture = 2 PNG.
 * The remaining 4 fixture-driven baselines (`tied`, `abandoned`, `empty-photos`,
 * dark ShareCard preview) live in `e2e/v2-states/session-summary.spec.ts`.
 *
 * Fixture sentinel: SESSION_SUMMARY_VISUAL_TEST_SENTINEL uuid
 * `00000000-0000-4000-8000-000000000756` — encodes Wave D.3 issue id in the
 * trailing nibble for human debuggability (mirrors Wave D.1 `735` and Wave D.2
 * `d20`).
 *
 * IS_VISUAL_TEST_BUILD=true baked into CI bootstrap build short-circuits the 4
 * orchestrator hooks (`useSessionDetail`, `useSessionDiaryQuery`,
 * `useSessionVisionSnapshots`, `useSessionAchievements`) so no backend API at
 * `:8080` is required.
 *
 * Bootstrap baseline (one-time, post-Task 5):
 *   `gh workflow run visual-regression-migrated.yml \
 *     --ref feature/issue-756-d3-summary-impl \
 *     -f mode=bootstrap -f project_filter=both`
 *
 * Auth bypass:
 *   `/sessions/[id]` is under `(authenticated)/`. `PLAYWRIGHT_AUTH_BYPASS=true`
 *   is set in `playwright.config.ts:434` (webServer env). Triple auth helper
 *   (`seedAuthSession + seedCookieConsent + mockAuthEndpoints`) satisfies the
 *   `proxy.ts` middleware gate + the client-side AuthProvider/useSessionCheck
 *   gates (Wave B.1 lesson learned, Issue #633).
 *
 * Confetti masking (Crispin Phase 0.5 contract §14):
 *   The hero confetti pieces are non-deterministic CSS animations. Masked via
 *   `[data-slot="confetti"]` to avoid flake. The static medal fallback rendered
 *   under `prefers-reduced-motion` is NOT masked (covered by a11y spec).
 *
 * No `networkidle` — always `domcontentloaded` + explicit `waitForSelector`
 * (Wave B.1 lesson).
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const SLUG = 'sp4-session-summary';
const FIXTURE_SESSION_ID = '00000000-0000-4000-8000-000000000756';

const VIEWPORTS = [
  { name: 'desktop', width: 1280, height: 720 },
  { name: 'mobile', width: 375, height: 812 },
] as const;

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function waitForSummaryReady(page: Page): Promise<void> {
  // Wait for the orchestrator container — full default render emits
  // `data-ui-state="default"` only after sessionQuery + diaryQuery + snapshotsQuery
  // have all settled (or fixture short-circuit applies in CI builds).
  await page.waitForSelector('[data-slot="session-summary-view"][data-ui-state="default"]', {
    timeout: 30_000,
  });
  // Confirm the hero is mounted before screenshot.
  await page.waitForSelector('[data-slot="session-summary-hero"]', { timeout: 10_000 });

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

test.describe('V2 Visual Migrated — /sessions/[id] summary matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  for (const viewport of VIEWPORTS) {
    test(`Session summary ${viewport.name} ${viewport.width}x${viewport.height} — default state matches sp4-session-summary mockup`, async ({
      page,
    }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await seedAuth(page);
      // Use explicit `?fixture=default` to short-circuit hooks and render
      // deterministic baseline data (orchestrator gates on STATE_OVERRIDE_ENABLED).
      await page.goto(`/sessions/${FIXTURE_SESSION_ID}?fixture=default`, {
        waitUntil: 'domcontentloaded',
      });
      await waitForSummaryReady(page);

      await expect(page).toHaveScreenshot(`${SLUG}-${viewport.name}.png`, {
        fullPage: true,
        animations: 'disabled',
        // Mask confetti CSS animation (non-deterministic frame timing) +
        // any `[data-dynamic]` regions emitted by future contributors.
        mask: [page.locator('[data-slot="confetti"]'), page.locator('[data-dynamic]')],
      });
    });
  }
});
