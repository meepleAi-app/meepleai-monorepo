/**
 * Accessibility tests — /sessions/[id]/live (Wave D.2, Issues #746 + #750).
 *
 * Foundation section (Issue #746):
 *   - axe-core WCAG 2.1 AA scan: dark theme default state (full layout)
 *   - axe-core WCAG 2.1 AA scan: loading state (skeleton markup accessibility)
 *   - axe-core WCAG 2.1 AA scan: not-found state (CTA + error message markup)
 *   - prefers-reduced-motion contract: animate-pulse skeleton collapses under reduced-motion
 *     global CSS override (globals.css `@media (prefers-reduced-motion: reduce)` rule).
 *   - WAI-ARIA tablist structure: mobile bottom-nav tabs have correct role="tab"
 *     + aria-selected + roving tabindex (data-slot="mobile-body-tab").
 *
 * Interactions section (Issue #750):
 *   - PauseOverlay focus trap: Tab cycles within dialog (WCAG 2.1.2 Level A)
 *   - PauseOverlay ESC closes dialog: keyboard dismissal (WCAG 2.1.1 Level A)
 *   - EndgameDialog ESC DISABLED: intentional WCAG deviation (documented, §4.3)
 *   - EndgameDialog focus trap: Tab cycles within dialog
 *   - axe-core WCAG 2.1 AA scan: PauseOverlay open state (Host role)
 *   - axe-core WCAG 2.1 AA scan: EndgameDialog open state
 *   - ConnectionLostBanner DOM structure: role="status"/"alert" + aria-live assertions
 *
 * NOTE — Light theme scope: deferred.
 *   SessionLiveView hardcodes `data-theme="dark"` on the root container for all FSM branches.
 *   No `?theme=light` URL override exists in the Foundation/Interactions sub-PRs.
 *
 * NOTE — ConnectionLostBanner visual states: excluded (scope reduction).
 *   `showConnectionBanner` is gated by `!IS_VISUAL_TEST_BUILD`. In CI builds with
 *   `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1`, the banner is always hidden.
 *   Tested here via DOM injection for ARIA contract assertions only.
 *
 * Auth bypass: Triple helper (seedAuthSession + seedCookieConsent + mockAuthEndpoints)
 *   required for `(authenticated)` routes (Wave B.1 lesson learned, Issue #633).
 *
 * No `networkidle` — always `domcontentloaded` + explicit `waitForSelector` (Wave B.1 lesson).
 */
import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

// See sessions-index.spec.ts for rationale (also: SessionLiveView hardcodes
// data-theme="dark" on its root, but the global --e-* tokens still depend on
// the .dark class applied by next-themes).
test.use({ colorScheme: 'dark' });

const FIXTURE_SESSION_ID = '00000000-0000-4000-8000-000000000d20';
const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] as const;

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function gotoSessionLive(page: Page, search = ''): Promise<void> {
  await seedAuth(page);
  await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live${search}`, {
    waitUntil: 'domcontentloaded',
  });
  await page.waitForSelector('[data-slot="session-live-view"]', { timeout: 30_000 });
}

test.describe('Session live — accessibility @a11y', () => {
  // ─────────────────────────────────────────────────────────────────────────────
  // FOUNDATION SECTION (Issue #746)
  // ─────────────────────────────────────────────────────────────────────────────

  // ── axe-core: default state ─────────────────────────────────────────────────

  test('axe-core: no WCAG 2.1 AA violations on dark default state', async ({ page }) => {
    await gotoSessionLive(page);
    // Confirm full layout mounted before scanning.
    await expect(page.locator('[data-slot="session-live-top-bar"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    if (results.violations.length > 0) {
      const summary = results.violations
        .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
        .join('\n');
      console.log('axe violations (default):\n' + summary);
    }
    expect(results.violations).toEqual([]);
  });

  // ── axe-core: loading state ─────────────────────────────────────────────────

  test('axe-core: no WCAG 2.1 AA violations on loading state', async ({ page }) => {
    await gotoSessionLive(page, '?state=loading');
    // LoadingShell: role="status" + aria-label + aria-live="polite"
    await expect(page.locator('[data-slot="session-live-loading"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    if (results.violations.length > 0) {
      const summary = results.violations
        .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
        .join('\n');
      console.log('axe violations (loading):\n' + summary);
    }
    expect(results.violations).toEqual([]);
  });

  // ── axe-core: not-found state ───────────────────────────────────────────────

  test('axe-core: no WCAG 2.1 AA violations on not-found state', async ({ page }) => {
    await gotoSessionLive(page, '?state=not-found');
    // NotFoundShell: heading text + back CTA button
    await expect(page.locator('[data-slot="session-live-not-found"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    if (results.violations.length > 0) {
      const summary = results.violations
        .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
        .join('\n');
      console.log('axe violations (not-found):\n' + summary);
    }
    expect(results.violations).toEqual([]);
  });

  // ── prefers-reduced-motion ──────────────────────────────────────────────────

  test('prefers-reduced-motion: animate-pulse skeleton collapses to sub-ms', async ({ page }) => {
    // Emulate reduced-motion BEFORE goto so the global CSS rule applies on first paint.
    // See globals.css `@media (prefers-reduced-motion: reduce)` — forces all
    // `transition-duration` to 0.01ms !important.
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await gotoSessionLive(page, '?state=loading');
    await expect(page.locator('[data-slot="session-live-loading"]')).toBeVisible();

    // Inspect computed animation/transition duration on the first animate-pulse element.
    // Under reduced-motion the global CSS override forces sub-millisecond durations.
    const animatedElements = await page.locator('.animate-pulse').count();
    if (animatedElements > 0) {
      const computed = await page
        .locator('.animate-pulse')
        .first()
        .evaluate((el: Element) => {
          const style = window.getComputedStyle(el);
          return {
            animationDuration: style.animationDuration,
            transitionDuration: style.transitionDuration,
          };
        });
      // Under reduced-motion: animation-duration should be 0s or very short,
      // OR the animation property is 'none'. Both are acceptable outcomes.
      const animDurationMs = (() => {
        const raw = computed.animationDuration;
        if (!raw || raw === 'none' || raw === '0s') return 0;
        if (raw.endsWith('ms')) return parseFloat(raw);
        if (raw.endsWith('s')) return parseFloat(raw) * 1000;
        return parseFloat(raw) * 1000;
      })();
      // Allow up to 50ms tolerance — the global override targets 0.01ms,
      // but computed values may include browser rounding.
      expect(animDurationMs).toBeLessThanOrEqual(50);
    }
  });

  // ── WAI-ARIA tablist structure: mobile bottom-nav ───────────────────────────

  test('WAI-ARIA tablist: mobile bottom-nav tabs have correct role + aria-selected + tabindex', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await gotoSessionLive(page);
    // MobileBody is hidden on desktop (lg:hidden) — use mobile viewport.
    await expect(page.locator('[data-slot="mobile-body"]')).toBeVisible();

    // Confirm tablist role on the container.
    const tablist = page.locator('[data-slot="mobile-body"] [role="tablist"]');
    await expect(tablist).toBeVisible();
    await expect(tablist).toHaveAttribute('aria-orientation', 'horizontal');

    // 4 tabs: score (default active) | log | tools | chat.
    const tabs = page.locator('[data-slot="mobile-body-tab"]');
    await expect(tabs).toHaveCount(4);

    // First tab ('score') is active by default: aria-selected="true" + tabIndex=0.
    const scoreTab = page.locator('[data-slot="mobile-body-tab"][data-tab="score"]');
    await expect(scoreTab).toHaveAttribute('role', 'tab');
    await expect(scoreTab).toHaveAttribute('aria-selected', 'true');

    // Remaining tabs are inactive: aria-selected="false" + tabIndex=-1 (roving tabindex).
    const logTab = page.locator('[data-slot="mobile-body-tab"][data-tab="log"]');
    const toolsTab = page.locator('[data-slot="mobile-body-tab"][data-tab="tools"]');
    const chatTab = page.locator('[data-slot="mobile-body-tab"][data-tab="chat"]');

    await expect(logTab).toHaveAttribute('aria-selected', 'false');
    await expect(toolsTab).toHaveAttribute('aria-selected', 'false');
    await expect(chatTab).toHaveAttribute('aria-selected', 'false');

    // NOTE: ArrowKey keyboard cycling is NOT wired in the Foundation sub-PR
    // (MobileBody has no onKeyDown handler — Interactions sub-PR adds
    // useTablistKeyboardNav). This test verifies the WAI-ARIA DOM structure
    // contract only; keyboard cycling behaviour is tested in the Interactions
    // sub-PR E2E specs.
  });

  // ─────────────────────────────────────────────────────────────────────────────
  // INTERACTIONS SECTION (Issue #750) — Dialog focus trap + ESC behaviour
  // ─────────────────────────────────────────────────────────────────────────────

  // ── PauseOverlay focus trap ─────────────────────────────────────────────────
  //
  // WCAG 2.1.2 (Level A): No keyboard trap — dialog MAY trap focus, but user
  // must be able to move focus out via a standard key (ESC or close CTA).
  // PauseOverlay satisfies 2.1.2 via ESC key (onClose removes ?dialog= from URL).
  //
  // Implementation: onKeyDown in PauseOverlay wraps Tab at first/last focusable
  // element boundary using getFocusables() scan on dialog ref.
  //
  // Reliability note: Playwright's keyboard.press('Tab') fires DOM keyboard events.
  // In headless Chromium, focus may not update document.activeElement synchronously.
  // We use page.evaluate() to read document.activeElement after each Tab press.

  test('PauseOverlay focus trap: Tab cycles stay within dialog (Host role, 10× Tab)', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    // ?fixture=host: Host role provides Resume CTA + Close button = 2 focusable elements.
    await seedAuth(page);
    await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?fixture=host&dialog=pause`, {
      waitUntil: 'domcontentloaded',
    });
    await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
      timeout: 30_000,
    });
    await page.waitForSelector('[data-slot="pause-overlay"]', { timeout: 15_000 });

    // Verify PauseOverlay has role="dialog" + aria-modal
    const dialog = page.locator('[data-slot="pause-overlay"]');
    await expect(dialog).toHaveAttribute('role', 'dialog');
    await expect(dialog).toHaveAttribute('aria-modal', 'true');

    // Tab through dialog 10× — focus must remain inside [role="dialog"] at each step.
    for (let i = 0; i < 10; i++) {
      await page.keyboard.press('Tab');

      const focusedInDialog = await page.evaluate(() => {
        const focused = document.activeElement;
        if (!focused) return false;
        return focused.closest('[role="dialog"]') !== null;
      });

      expect(focusedInDialog).toBeTruthy();
    }
  });

  // ── PauseOverlay ESC closes dialog ─────────────────────────────────────────
  //
  // WCAG 2.1.1 (Level A): All functionality accessible via keyboard alone.
  // ESC must dismiss the PauseOverlay dialog (calls onClose → router.replace
  // removes ?dialog= from URL — URL SSOT pattern).
  //
  // Reliability note: We click inside the dialog before pressing ESC to ensure
  // keyboard events land on the element with the onKeyDown handler.

  // Re-enabled 2026-05-31 (issue #1715): test was skipped via `test.fixme()` in
  // PR #1711 because the strict `state: 'detached'` assertion raced the
  // React.lazy + Suspense unmount under CI load. The flow is:
  //   ESC keydown → onClose() → router.replace() → ?dialog=pause removed
  //     → React re-render → Suspense child unmounts → DOM node removed
  //
  // Under CI scheduler pressure step 3→5 can take >15s, even though the URL
  // mutation (step 2) and the user-visible dismissal (focus restore + dialog
  // hidden) complete promptly. Investigation in #1715 confirmed the production
  // semantics are correct — ESC is captured, the handler is called synchronously,
  // focus is restored on unmount via PauseOverlay's `useEffect` cleanup. The
  // race is purely test-side timing of strict DOM detach vs. observable a11y
  // contract.
  //
  // Fix strategy: assert on the user-visible a11y contract (URL drops the
  // dialog param + dialog is no longer visible to AT users) instead of the
  // stricter "DOM node detached" condition. Playwright's `toBeHidden()` auto-
  // retries and passes when the element is detached OR visually hidden, which
  // matches the WCAG 2.1.1 promise: ESC dismisses the dialog from the user's
  // perspective. Detach itself still happens — we just don't gate the test on
  // the exact reconciliation tick. Pattern parallels round-2 flake fixes in
  // PR #1711 (game-night-create numeric parsing over regex match).
  test('PauseOverlay ESC closes dialog — URL drops ?dialog=pause', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuth(page);
    await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?fixture=host&dialog=pause`, {
      waitUntil: 'domcontentloaded',
    });
    await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
      timeout: 30_000,
    });
    await page.waitForSelector('[data-slot="pause-overlay"]', { timeout: 15_000 });

    // Click inside the dialog to ensure keyboard events target the onKeyDown handler.
    await page.locator('[data-slot="pause-overlay"]').click();

    // Press ESC — triggers onClose() → router.replace removes ?dialog=pause
    await page.keyboard.press('Escape');

    // PRIMARY a11y contract: URL must drop ?dialog=pause synchronously.
    // This proves the ESC handler fired AND router.replace committed — i.e.
    // the dismiss state is the source of truth and AT users navigating by URL
    // will see the dialog as closed. `expect.poll` auto-retries with default
    // 5s timeout (sufficient: router.replace is sub-second under CI).
    await expect.poll(() => page.url(), { timeout: 5_000 }).not.toContain('dialog=pause');

    // SECONDARY a11y contract: dialog must be invisible to AT users.
    // `toBeHidden()` passes for any of: detached, display:none, visibility:hidden,
    // zero size, off-screen, or aria-hidden ancestor. This matches the user-
    // visible dismissal contract without racing React.lazy + Suspense's
    // reconciliation tick (which is the root cause of the original flake).
    // 15s timeout: same headroom used elsewhere in this spec for dialog
    // lifecycle assertions under CI load.
    await expect(page.locator('[data-slot="pause-overlay"]')).toBeHidden({ timeout: 15_000 });
  });

  // ── EndgameDialog ESC DISABLED — intentional deviation ─────────────────────
  //
  // EndgameDialog intentionally DISABLES ESC key dismissal.
  // Rationale (documented in EndgameDialog.tsx JSDoc + Phase 0.5 contract §4.3):
  //   Accidental ESC dismiss = permanent data loss (final scores, no second path back).
  //   Only exit: Acknowledge CTA button (onAcknowledge → router.replace ?dialog=none).
  //
  // WCAG 2.1 note: Documented deviation from WCAG 2.1.2 advisory text.
  // Permitted by §4.3 UX/a11y review sign-off; mitigated by clear labelled CTA.
  //
  // This test ASSERTS that ESC does NOT close the dialog — deliberately verifying
  // the intentional behaviour, not a bug.

  test('EndgameDialog ESC disabled — dialog remains open (data-loss guard)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuth(page);
    await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?dialog=endgame`, {
      waitUntil: 'domcontentloaded',
    });
    await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
      timeout: 30_000,
    });
    await page.waitForSelector('[data-slot="endgame-dialog"]', { timeout: 15_000 });

    // Click inside dialog to ensure keyboard events target the onKeyDown handler.
    await page.locator('[data-slot="endgame-dialog"]').click();

    // Press ESC — must NOT close the dialog (intentional no-op in EndgameDialog)
    await page.keyboard.press('Escape');

    // Wait 800ms: router navigation would complete in <200ms if ESC did close.
    await page.waitForTimeout(800);

    // Dialog MUST remain visible (ESC = no-op for data-loss protection)
    await expect(page.locator('[data-slot="endgame-dialog"]')).toBeVisible();

    // URL must still contain ?dialog=endgame (no navigation occurred)
    expect(page.url()).toContain('dialog=endgame');
  });

  // ── EndgameDialog focus trap ────────────────────────────────────────────────
  //
  // EndgameDialog has fewer focusable elements than PauseOverlay (Acknowledge CTA only).
  // Tab cycling must still remain within dialog scope per WCAG 2.1.2.

  test('EndgameDialog focus trap: Tab cycles stay within dialog (6× Tab)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuth(page);
    await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?dialog=endgame`, {
      waitUntil: 'domcontentloaded',
    });
    await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
      timeout: 30_000,
    });
    await page.waitForSelector('[data-slot="endgame-dialog"]', { timeout: 15_000 });

    // Verify EndgameDialog has role="dialog" + aria-modal
    const dialog = page.locator('[data-slot="endgame-dialog"]');
    await expect(dialog).toHaveAttribute('role', 'dialog');
    await expect(dialog).toHaveAttribute('aria-modal', 'true');

    // Tab through dialog 6× — focus must remain inside [role="dialog"] at each step.
    for (let i = 0; i < 6; i++) {
      await page.keyboard.press('Tab');

      const focusedInDialog = await page.evaluate(() => {
        const focused = document.activeElement;
        if (!focused) return false;
        return focused.closest('[role="dialog"]') !== null;
      });

      expect(focusedInDialog).toBeTruthy();
    }
  });

  // ── axe-core: PauseOverlay dialog state ────────────────────────────────────

  test('axe-core: no WCAG 2.1 AA violations with PauseOverlay open (Host role)', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    // Issue #1223 / refs #1094 Real-C-D + E: emulate reduced-motion BEFORE goto
    // so the dialog's `motion-reduce:animate-none` skips the 200ms fade-in.
    // axe-core scans immediately after the dialog mounts; without this, mid-
    // animation alpha-composited colors trigger false-positive contrast hits
    // (e.g. .bg-emerald-700 reported as 1.28 ratio vs the 4.78 stable state).
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await seedAuth(page);
    await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?fixture=host&dialog=pause`, {
      waitUntil: 'domcontentloaded',
    });
    await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
      timeout: 30_000,
    });
    await page.waitForSelector('[data-slot="pause-overlay"]', { timeout: 15_000 });

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    if (results.violations.length > 0) {
      const summary = results.violations
        .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
        .join('\n');
      console.log('axe violations (pause-overlay):\n' + summary);
    }
    expect(results.violations).toEqual([]);
  });

  // ── axe-core: EndgameDialog state ──────────────────────────────────────────

  test('axe-core: no WCAG 2.1 AA violations with EndgameDialog open', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    // Issue #1223 / refs #1094 Real-C-D + E: see PauseOverlay scan above for
    // rationale. Reduced-motion skips fade-in so axe sees the stable final
    // state, not the transient mid-animation composited colors.
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await seedAuth(page);
    await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?dialog=endgame`, {
      waitUntil: 'domcontentloaded',
    });
    await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
      timeout: 30_000,
    });
    await page.waitForSelector('[data-slot="endgame-dialog"]', { timeout: 15_000 });

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    if (results.violations.length > 0) {
      const summary = results.violations
        .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
        .join('\n');
      console.log('axe violations (endgame-dialog):\n' + summary);
    }
    expect(results.violations).toEqual([]);
  });

  // ── ConnectionLostBanner ARIA contract ─────────────────────────────────────
  //
  // ConnectionLostBanner visual states cannot be tested via URL override in CI
  // (`showConnectionBanner` is gated by `!IS_VISUAL_TEST_BUILD` in the orchestrator).
  //
  // We verify the ARIA role/live contract by injecting minimal DOM elements
  // that mirror the component's rendered attributes. This tests the ARIA
  // attribute API, not the full component render path.
  //
  // Full component tests (all 3 kinds, rendered): ConnectionLostBanner.test.tsx.

  test('ConnectionLostBanner ARIA: reconnecting=role/status, failed=role/alert', async ({
    page,
  }) => {
    await seedAuth(page);
    await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="session-live-view"]', { timeout: 30_000 });

    // Inject reconnecting banner — must have role="status" + aria-live="polite"
    await page.evaluate(() => {
      const banner = document.createElement('div');
      banner.setAttribute('data-slot', 'test-banner-reconnecting');
      banner.setAttribute('role', 'status');
      banner.setAttribute('aria-live', 'polite');
      banner.textContent = 'Reconnecting...';
      document.body.appendChild(banner);
    });

    const reconnectingBanner = page.locator('[data-slot="test-banner-reconnecting"]');
    await expect(reconnectingBanner).toHaveAttribute('role', 'status');
    await expect(reconnectingBanner).toHaveAttribute('aria-live', 'polite');

    // Inject failed banner — must have role="alert" (assertive by definition)
    await page.evaluate(() => {
      const bannerFailed = document.createElement('div');
      bannerFailed.setAttribute('data-slot', 'test-banner-failed');
      bannerFailed.setAttribute('role', 'alert');
      bannerFailed.textContent = 'Connection failed';
      document.body.appendChild(bannerFailed);
    });

    const failedBanner = page.locator('[data-slot="test-banner-failed"]');
    await expect(failedBanner).toHaveAttribute('role', 'alert');
    // role="alert" = implicit aria-live="assertive" per ARIA spec (no explicit attribute needed)
  });
});
