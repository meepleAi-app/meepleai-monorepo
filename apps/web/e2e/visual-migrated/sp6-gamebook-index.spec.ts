/**
 * Visual contract — /gamebook index route vs sp6 mockup baseline.
 *
 * Issue #788 (SP6 Phase B brownfield migration — Tier M)
 *
 * Coverage in this spec: 2 viewports × default fixture = 2 PNG.
 * The remaining 5 fixture-driven baselines (`empty`, `quota-soft`, `quota-hard`,
 * `loading`, `error`) live in `e2e/v2-states/gamebook-index.spec.ts`.
 *
 * IS_VISUAL_TEST_BUILD=true baked into CI bootstrap build short-circuits the
 * 2 stub orchestrator hooks (`useGamebooks`, `useQuotaInfo`) so no backend API
 * at `:8080` is required. Default state is non-deterministic without an explicit
 * `?fixture=default` URL hatch (Pattern P14 from Wave D.3 lesson — orchestrator
 * only gates on the explicit query string, never falls back automatically).
 *
 * Bootstrap baseline (one-time, post-Task 5):
 *   `gh workflow run visual-regression-migrated.yml \
 *     --ref feature/issue-788-gamebook-index \
 *     -f mode=bootstrap -f project_filter=both`
 *
 * Auth bypass:
 *   `/gamebook` is under `(authenticated)/`. `PLAYWRIGHT_AUTH_BYPASS=true`
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

const SLUG = 'sp6-gamebook-index';

const VIEWPORTS = [
  { name: 'desktop', width: 1280, height: 800 },
  { name: 'mobile', width: 375, height: 667 },
] as const;

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function waitForGamebookReady(page: Page): Promise<void> {
  // Wait for the orchestrator to mount in the `default` cell — the
  // ?fixture=default URL hatch deterministically resolves the FSM to this
  // state, short-circuiting both stub hooks (`useGamebooks` + `useQuotaInfo`).
  await page.waitForSelector('[data-slot="gamebook-index-view"][data-ui-state="default"]', {
    timeout: 30_000,
  });
  // Confirm the hero is mounted before screenshot (default cell renders hero
  // KPIs derived from fixture data).
  await page.waitForSelector('[data-slot="gamebook-hero"]', { timeout: 10_000 });

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

test.describe('V2 Visual Migrated — /gamebook index matches sp6 mockup', () => {
  test.describe.configure({ retries: 0 });

  for (const viewport of VIEWPORTS) {
    test(`Gamebook index ${viewport.name} ${viewport.width}x${viewport.height} — default state matches sp6-gamebook-index mockup`, async ({
      page,
    }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await seedAuth(page);
      // EXPLICIT ?fixture=default — orchestrator only short-circuits with the
      // explicit query string (Pattern P14 from Wave D.3 lesson). Without it
      // the stub hooks return live (empty) data and the FSM resolves to a
      // different cell.
      await page.goto('/gamebook?fixture=default', {
        waitUntil: 'domcontentloaded',
      });
      await waitForGamebookReady(page);

      await expect(page).toHaveScreenshot(`${SLUG}-${viewport.name}.png`, {
        fullPage: true,
        animations: 'disabled',
      });
    });
  }
});
