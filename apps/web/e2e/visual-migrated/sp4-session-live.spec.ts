/**
 * Visual contract — /sessions/[id]/live route vs mockup baseline.
 *
 * Issue #746 (Wave D.2 Foundation sub-PR)
 * V2 Migration Phase 1 (Wave D.2).
 *
 * Coverage: 2 viewports × 1 theme = 4 PNG (dark-only, see note below).
 *
 * NOTE — Theme scope: dark-only.
 *   The orchestrator (SessionLiveView) hardcodes `data-theme="dark"` on the root
 *   container for every FSM branch. No `?theme=light` URL override mechanism
 *   exists in the Foundation sub-PR. Light-theme coverage is intentionally
 *   deferred to the Interactions sub-PR when/if a theme toggle is wired.
 *   See: SessionLiveView.tsx lines 476, 487, 507, 527, 592 — all carry
 *   `data-theme="dark"`.
 *
 * Fixture sentinel: VISUAL_TEST_FIXTURE_SESSION uuid `00000000-0000-4000-8000-000000000d20`
 *   IS_VISUAL_TEST_BUILD=true baked into CI bootstrap build replaces the
 *   real `useSession` hook with deterministic data (5 players, 12-turn action
 *   log) so no backend API at `:8080` is required.
 *
 * Bootstrap baseline (one-time, post-Foundation):
 *   `gh workflow run visual-regression-migrated.yml \
 *     --ref feature/issue-746-d2-foundation \
 *     -f mode=bootstrap -f project_filter=both`
 *
 * Auth bypass:
 *   `/sessions/[id]/live` is under `(authenticated)/`. `PLAYWRIGHT_AUTH_BYPASS=true`
 *   is set in `playwright.config.ts:434` (webServer env). Triple auth helper
 *   (`seedAuthSession + seedCookieConsent + mockAuthEndpoints`) satisfies proxy.ts
 *   middleware gate + client-side AuthProvider/useSessionCheck gates.
 *
 * No `networkidle` — always `domcontentloaded` + explicit `waitForSelector` (Wave B.1 lesson).
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const FIXTURE_SESSION_ID = '00000000-0000-4000-8000-000000000d20';

const VIEWPORTS = [
  { name: 'desktop', width: 1280, height: 720 },
  { name: 'mobile', width: 375, height: 812 },
] as const;

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function waitForSessionLiveReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="session-live-view"]', { timeout: 30_000 });
  // Confirm the top bar is fully mounted (default state gate).
  await page.waitForSelector('[data-slot="session-live-top-bar"]', { timeout: 10_000 });

  // Wait for fonts to avoid flaky text rendering differences across runs.
  await page.evaluate(async () => {
    if (typeof document !== 'undefined' && 'fonts' in document) {
      await (document as Document & { fonts: { ready: Promise<void> } }).fonts.ready;
    }
  });

  // Double rAF: settle any layout-triggered repaint before screenshot.
  await page.evaluate(
    () =>
      new Promise<void>(resolve => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      })
  );
}

test.describe('V2 Visual Migrated — /sessions/[id]/live matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  for (const viewport of VIEWPORTS) {
    test(`Session live ${viewport.name} ${viewport.width}x${viewport.height} — dark default state matches sp4-session-live mockup`, async ({
      page,
    }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await seedAuth(page);
      await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live`, {
        waitUntil: 'domcontentloaded',
      });
      await waitForSessionLiveReady(page);

      await expect(page).toHaveScreenshot(`sp4-session-live-${viewport.name}-dark.png`, {
        fullPage: true,
        animations: 'disabled',
        // Mask timestamp strings and any other dynamic content to prevent flake.
        mask: [page.locator('[data-dynamic]')],
      });
    });
  }
});
