/**
 * E2E — /sessions/[id]/live mobile bottom-sheet (Issue #2374 G1 T9).
 *
 * Viewport 375×667 (iPhone SE). Covers the mockup canonical bottom-sheet
 * pattern (parts.jsx L266-286) shipped in T9 sess.46r:
 *   - Default mobile renders main column full-width (ChatAgentPanel +
 *     ActionLogTimeline stacked) — no bottom-nav.
 *   - Floating action button opens MobileBottomSheetDrawer.
 *   - Tab switch inside drawer updates ?mtab= URL.
 *   - Escape closes drawer (Radix Dialog default).
 *   - axe-AA 0 violations on default + drawer-open states.
 *
 * Pattern blueprint: e2e/a11y/session-live.spec.ts G1 desktop tests.
 *
 * Auth bypass: triple helper (seedAuthSession + seedCookieConsent +
 * mockAuthEndpoints) required for `(authenticated)` routes (Wave B.1 lesson #633).
 */
import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

// Mirror a11y spec: SessionLiveView hardcodes data-theme="dark" on its root, but
// the global tokens still depend on `.dark` class applied by next-themes.
test.use({ colorScheme: 'dark' });

const FIXTURE_SESSION_ID = '00000000-0000-4000-8000-000000000d20';
const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] as const;
const MOBILE_VIEWPORT = { width: 375, height: 667 } as const;

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function gotoMobileLive(page: Page, search = ''): Promise<void> {
  await page.setViewportSize({ width: MOBILE_VIEWPORT.width, height: MOBILE_VIEWPORT.height });
  await seedAuth(page);
  await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live${search}`, {
    waitUntil: 'domcontentloaded',
  });
  await page.waitForSelector('[data-slot="session-live-view"]', { timeout: 30_000 });
  await page.waitForSelector('[data-slot="mobile-body"]', { timeout: 15_000 });
}

test.describe('Session live — mobile bottom-sheet @mobile', () => {
  test('default mobile renders main content full-width (ChatAgentPanel + ActionLogTimeline)', async ({
    page,
  }) => {
    await gotoMobileLive(page);

    const mobileBody = page.locator('[data-slot="mobile-body"]');
    await expect(mobileBody).toBeVisible();
    await expect(mobileBody).toHaveAttribute('data-layout', 'mobile-sheet');

    // Main column = full-width LEFT-equivalent.
    await expect(mobileBody.locator('[data-slot="chat-agent-panel"]')).toBeVisible();
    await expect(mobileBody.locator('[data-slot="action-log-timeline"]')).toBeVisible();

    // No legacy bottom-nav.
    await expect(page.locator('[data-slot="mobile-body-tab"]')).toHaveCount(0);

    // Drawer is closed by default.
    await expect(page.locator('[data-slot="mobile-bottom-sheet"]')).toHaveCount(0);
  });

  test('FAB opens bottom-sheet drawer', async ({ page }) => {
    await gotoMobileLive(page);

    const fab = page.locator('[data-slot="mobile-body-open-sheet"]');
    await expect(fab).toBeVisible();
    await fab.click();

    const drawer = page.locator('[data-slot="mobile-bottom-sheet"]');
    await expect(drawer).toBeVisible();
    await expect(drawer).toHaveAttribute('role', 'dialog');
    await expect(drawer).toHaveAttribute('aria-modal', 'true');

    // Tab strip: 4 tabs.
    const tabs = drawer.locator('[role="tab"]');
    await expect(tabs).toHaveCount(4);
  });

  test('tab switch inside drawer updates ?mtab= URL', async ({ page }) => {
    await gotoMobileLive(page, '?msheet=open');

    const drawer = page.locator('[data-slot="mobile-bottom-sheet"]');
    await expect(drawer).toBeVisible();

    const turnTab = drawer.locator('[role="tab"]').nth(1);
    await turnTab.click();

    // URL must reflect the new active tab.
    await expect.poll(() => page.url(), { timeout: 5_000 }).toContain('mtab=turn');
  });

  test('Escape closes drawer (Radix Dialog default)', async ({ page }) => {
    await gotoMobileLive(page, '?msheet=open');

    const drawer = page.locator('[data-slot="mobile-bottom-sheet"]');
    await expect(drawer).toBeVisible();

    // Click inside drawer to make sure focus is captured.
    await drawer.click();
    await page.keyboard.press('Escape');

    // PRIMARY contract: URL drops ?msheet=open. (Mirrors PauseOverlay ESC pattern.)
    await expect.poll(() => page.url(), { timeout: 5_000 }).not.toContain('msheet=open');

    // SECONDARY: drawer is hidden (matches user-visible dismissal).
    await expect(drawer).toBeHidden({ timeout: 15_000 });
  });

  test('axe-core: no WCAG 2.1 AA violations on mobile default + drawer-open states', async ({
    page,
  }) => {
    // Skip animation timing so axe sees stable final colours (consistent with
    // PauseOverlay/EndgameDialog scans in a11y/session-live.spec.ts).
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // ─── State 1: default (drawer closed) ─────────────────────────────────
    await gotoMobileLive(page);

    const defaultResults = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    if (defaultResults.violations.length > 0) {
      const summary = defaultResults.violations
        .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
        .join('\n');
      console.log('axe violations (mobile default):\n' + summary);
    }
    expect(defaultResults.violations).toEqual([]);

    // ─── State 2: drawer open ─────────────────────────────────────────────
    await page.locator('[data-slot="mobile-body-open-sheet"]').click({ timeout: 5_000 });
    await expect(page.locator('[data-slot="mobile-bottom-sheet"]')).toBeVisible();

    const drawerResults = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    if (drawerResults.violations.length > 0) {
      const summary = drawerResults.violations
        .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
        .join('\n');
      console.log('axe violations (mobile drawer-open):\n' + summary);
    }
    expect(drawerResults.violations).toEqual([]);
  });
});
