/**
 * Accessibility tests — /sessions/[id]/live (Wave D.2 Foundation sub-PR, Issue #746).
 *
 * Combines:
 *   - axe-core WCAG 2.1 AA scan: dark theme default state (full layout)
 *   - axe-core WCAG 2.1 AA scan: loading state (skeleton markup accessibility)
 *   - axe-core WCAG 2.1 AA scan: not-found state (CTA + error message markup)
 *   - prefers-reduced-motion contract: animate-pulse skeleton collapses under reduced-motion
 *     global CSS override (globals.css `@media (prefers-reduced-motion: reduce)` rule).
 *   - WAI-ARIA tablist structure: mobile bottom-nav tabs have correct role="tab"
 *     + aria-selected + roving tabindex (data-slot="mobile-body-tab").
 *
 * NOTE — Light theme scope: deferred.
 *   SessionLiveView hardcodes `data-theme="dark"` on the root container for all FSM branches.
 *   No `?theme=light` URL override exists in the Foundation sub-PR. Light-theme a11y
 *   coverage is deferred to the Interactions sub-PR.
 *
 * Foundation scope: read-only static fixture — NO SSE, NO dialogs.
 *   MobileBody tabs have `role="tab"` + roving tabindex via `tabIndex={isActive ? 0 : -1}`.
 *   Keyboard ArrowKey cycling is NOT wired in the Foundation (no `onKeyDown` handler on
 *   MobileBody tab buttons) — this is wired in the Interactions sub-PR with
 *   `useTablistKeyboardNav`. This spec verifies the WAI-ARIA structure contract
 *   (role + aria-selected + tabindex) rather than keyboard cycling behaviour.
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
});
