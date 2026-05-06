/**
 * V2 State Coverage — /sessions/[id]/live (Issue #746, Wave D.2 Foundation sub-PR).
 *
 * Captures 3 deterministic FSM states across 2 viewports:
 *   `default | loading | not-found`
 *
 * The fourth state `error` is excluded from visual coverage because the error
 * surface depends on `sessionQuery.isError` (TanStack Query network failure),
 * which cannot be reproduced deterministically via URL override.
 * `parseStateOverride` explicitly excludes 'error': fixture.ts:248 comment.
 * Error state is covered by unit tests in
 * `_components/__tests__/SessionLiveView.test.tsx`.
 *
 * Role variants (desktop-only):
 *   - `?fixture=spectator` — Spectator role (read-only controls)
 *   - `?fixture=host`      — Host role (pause/endgame controls)
 *   - `?fixture=paused`    — Paused status (Player role, resume control)
 *
 * Total: 3 states × 2 viewports = 6 PNG + 3 role variants × 1 (desktop) = 3 PNG = 9 PNG.
 *
 * URL override mechanism:
 *   `?state=loading|not-found` — gated by `STATE_OVERRIDE_ENABLED`
 *   (`NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD`).
 *   CI build sets `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` → STATE_OVERRIDE_ENABLED=true.
 *
 * Fixture UUID: `00000000-0000-4000-8000-000000000d20` (Wave D.2 sentinel).
 *
 * Theme: dark-only (orchestrator hardcodes `data-theme="dark"` on root container —
 * no `?theme=light` URL override in Foundation sub-PR scope).
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

async function waitForFontsAndRaf(page: Page): Promise<void> {
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

test.describe('Session live — state coverage', () => {
  test.describe.configure({ retries: 0 });

  for (const viewport of VIEWPORTS) {
    test.describe(`${viewport.name} ${viewport.width}x${viewport.height}`, () => {
      // ── Default state ───────────────────────────────────────────────────────

      test('default state — full layout with Player role fixture', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        // No ?fixture or ?state param — uses VISUAL_TEST_FIXTURE_SESSION (Player, InProgress)
        await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live`, {
          waitUntil: 'domcontentloaded',
        });
        await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
          timeout: 30_000,
        });
        await page.waitForSelector('[data-slot="session-live-top-bar"]', { timeout: 10_000 });
        await waitForFontsAndRaf(page);
        await expect(page).toHaveScreenshot(`session-live-${viewport.name}-default.png`, {
          fullPage: true,
          animations: 'disabled',
          mask: [page.locator('[data-dynamic]')],
        });
      });

      // ── Loading state ───────────────────────────────────────────────────────

      test('loading state — skeleton shell', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?state=loading`, {
          waitUntil: 'domcontentloaded',
        });
        // LoadingShell has data-slot="session-live-loading" inside
        // data-slot="session-live-view"[data-ui-state="loading"]
        await page.waitForSelector('[data-slot="session-live-loading"]', { timeout: 30_000 });
        await waitForFontsAndRaf(page);
        await expect(page).toHaveScreenshot(`session-live-${viewport.name}-loading.png`, {
          fullPage: true,
          animations: 'disabled',
          // Mask skeleton pulse animations to avoid frame-timing flake.
          mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
        });
      });

      // ── Not-found state ─────────────────────────────────────────────────────

      test('not-found state — back CTA', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?state=not-found`, {
          waitUntil: 'domcontentloaded',
        });
        // NotFoundShell has data-slot="session-live-not-found" inside
        // data-slot="session-live-view"[data-ui-state="not-found"]
        await page.waitForSelector('[data-slot="session-live-not-found"]', { timeout: 30_000 });
        await waitForFontsAndRaf(page);
        await expect(page).toHaveScreenshot(`session-live-${viewport.name}-not-found.png`, {
          fullPage: true,
          animations: 'disabled',
          mask: [page.locator('[data-dynamic]')],
        });
      });

      // NOTE: 'error' state excluded from visual coverage (Wave D.2 lesson, mirrors Wave B.1/B.2/D.1):
      // `parseStateOverride` intentionally excludes 'error' — `sessionQuery.isError` (TanStack Query
      // network failure) cannot be reproduced deterministically via URL override. Covered by unit tests
      // in `_components/__tests__/SessionLiveView.test.tsx`.

      // ── Role variants — desktop only ────────────────────────────────────────

      if (viewport.name === 'desktop') {
        test('spectator role variant — read-only controls', async ({ page }) => {
          await page.setViewportSize({ width: viewport.width, height: viewport.height });
          await seedAuth(page);
          await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?fixture=spectator`, {
            waitUntil: 'domcontentloaded',
          });
          await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
            timeout: 30_000,
          });
          await page.waitForSelector('[data-slot="session-live-top-bar"]', { timeout: 10_000 });
          await waitForFontsAndRaf(page);
          await expect(page).toHaveScreenshot('session-live-desktop-spectator.png', {
            fullPage: true,
            animations: 'disabled',
            mask: [page.locator('[data-dynamic]')],
          });
        });

        test('host role variant — pause + endgame controls', async ({ page }) => {
          await page.setViewportSize({ width: viewport.width, height: viewport.height });
          await seedAuth(page);
          await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?fixture=host`, {
            waitUntil: 'domcontentloaded',
          });
          await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
            timeout: 30_000,
          });
          await page.waitForSelector('[data-slot="session-live-top-bar"]', { timeout: 10_000 });
          await waitForFontsAndRaf(page);
          await expect(page).toHaveScreenshot('session-live-desktop-host.png', {
            fullPage: true,
            animations: 'disabled',
            mask: [page.locator('[data-dynamic]')],
          });
        });

        test('paused status variant — resume control (Player role)', async ({ page }) => {
          await page.setViewportSize({ width: viewport.width, height: viewport.height });
          await seedAuth(page);
          await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?fixture=paused`, {
            waitUntil: 'domcontentloaded',
          });
          await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
            timeout: 30_000,
          });
          await page.waitForSelector('[data-slot="session-live-top-bar"]', { timeout: 10_000 });
          await waitForFontsAndRaf(page);
          await expect(page).toHaveScreenshot('session-live-desktop-paused.png', {
            fullPage: true,
            animations: 'disabled',
            mask: [page.locator('[data-dynamic]')],
          });
        });
      }
    });
  }
});
