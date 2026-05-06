/**
 * V2 State Coverage — /sessions/[id]/live (Issue #750, Wave D.2 Interactions sub-PR).
 *
 * Extends Foundation sub-PR (#746) with Interactions dialog states:
 *   `default | loading | not-found` (Foundation, 6 PNG)
 *   + role variants: spectator | host | paused (Foundation, 3 PNG)
 *   + dialog variants: pause-dialog | endgame-dialog (Interactions, 2 PNG — desktop only)
 *
 * Total: 6 + 3 + 2 = 11 PNG (Task 5 bootstraps baselines).
 *
 * Dialog states — URL mechanism:
 *   `?dialog=pause`   → PauseOverlay lazy-mounted via React.Suspense
 *   `?dialog=endgame` → EndgameDialog lazy-mounted via React.Suspense
 *   Both derived by `deriveSessionLiveDialogState()` from URL searchParams (URL SSOT).
 *   Require `?fixture=host` to render default shell first (activeSession != null).
 *
 * Connection-lost banner — SCOPE REDUCED (documented):
 *   `showConnectionBanner` is gated by `!IS_VISUAL_TEST_BUILD` in the orchestrator.
 *   In CI builds (`NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1`), IS_VISUAL_TEST_BUILD=true
 *   → banner is always hidden in fixture mode. No `?state=connection-lost` URL override
 *   exists in the orchestrator (`parseStateOverride` handles only `loading|not-found`).
 *   Connection-lost states are tested in `e2e/a11y/session-live.spec.ts` via
 *   `ConnectionLostBanner` component unit tests and a11y axe scan in IS_VISUAL_TEST_BUILD=false
 *   context only. Visual coverage excluded — mirrors Wave B.2 'error' state exclusion pattern.
 *
 * Fixture UUID: `00000000-0000-4000-8000-000000000d20` (Wave D.2 sentinel).
 * Theme: dark-only (orchestrator hardcodes `data-theme="dark"` on root container).
 * Auth bypass: Triple helper (seedAuthSession + seedCookieConsent + mockAuthEndpoints).
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

        // ── Dialog states — Interactions sub-PR (Wave D.2, Issue #750) ─────────
        // Dialog states use ?dialog= URL param (SSOT via deriveSessionLiveDialogState).
        // Requires ?fixture=host to ensure activeSession != null (guard in orchestrator).
        // Dialogs are React.lazy → Suspense — wait for dialog mount before screenshot.

        test('pause dialog open — PauseOverlay mounted via Suspense (Host role)', async ({
          page,
        }) => {
          await page.setViewportSize({ width: viewport.width, height: viewport.height });
          await seedAuth(page);
          // ?fixture=host: Host fixture ensures activeSession != null (dialog branch reached).
          // ?dialog=pause: deriveSessionLiveDialogState returns 'pause' → PauseOverlay lazy mount.
          await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?fixture=host&dialog=pause`, {
            waitUntil: 'domcontentloaded',
          });
          // Wait for default shell (activeSession != null guard passes)
          await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
            timeout: 30_000,
          });
          // Wait for Suspense lazy-load to resolve and PauseOverlay to mount
          await page.waitForSelector('[data-slot="pause-overlay"]', { timeout: 15_000 });
          await waitForFontsAndRaf(page);
          await expect(page).toHaveScreenshot('session-live-desktop-pause-dialog.png', {
            fullPage: true,
            animations: 'disabled',
            mask: [page.locator('[data-dynamic]')],
          });
        });

        test('endgame dialog open — EndgameDialog mounted via Suspense', async ({ page }) => {
          await page.setViewportSize({ width: viewport.width, height: viewport.height });
          await seedAuth(page);
          // ?dialog=endgame: deriveSessionLiveDialogState returns 'endgame' → EndgameDialog lazy mount.
          // Default fixture (Player role) is sufficient — endgame dialog has no role gating.
          await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?dialog=endgame`, {
            waitUntil: 'domcontentloaded',
          });
          await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
            timeout: 30_000,
          });
          // Wait for Suspense lazy-load to resolve and EndgameDialog to mount
          await page.waitForSelector('[data-slot="endgame-dialog"]', { timeout: 15_000 });
          await waitForFontsAndRaf(page);
          await expect(page).toHaveScreenshot('session-live-desktop-endgame-dialog.png', {
            fullPage: true,
            animations: 'disabled',
            mask: [page.locator('[data-dynamic]')],
          });
        });
      }
    });
  }
});

// ── Connection-lost banner — SCOPE REDUCED ──────────────────────────────────────
//
// `showConnectionBanner` in the orchestrator is gated by `!IS_VISUAL_TEST_BUILD`:
//   const showConnectionBanner = !IS_VISUAL_TEST_BUILD && (liveStream.connectionState === ...);
//
// In CI builds (`NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1`), IS_VISUAL_TEST_BUILD=true.
// This means the banner is always hidden in the visual-test fixture mode.
//
// There is NO `?state=connection-lost` URL override in `parseStateOverride`:
//   parseStateOverride handles only 'loading' | 'not-found', not connection states.
//
// ConnectionLostBanner visual states cannot be reproduced deterministically via URL
// in the IS_VISUAL_TEST_BUILD=true CI build context.
//
// Resolution: ConnectionLostBanner is covered by:
//   1. Unit tests in `components/v2/session-live/__tests__/ConnectionLostBanner.test.tsx`
//      (all 3 kinds: reconnecting, degraded-polling, failed)
//   2. a11y axe scan in `e2e/a11y/session-live.spec.ts` ConnectionLostBanner section
//      (DOM structure + ARIA role assertions — not visual regression)
//
// This scope reduction mirrors the Wave B.2 'error' state visual exclusion pattern.
// To add visual coverage: add `?state=connection-lost-{kind}` override + IS_VISUAL_TEST_BUILD
// bypass in the orchestrator in a future PR (out of Wave D.2 Interactions scope).
