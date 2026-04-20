/**
 * Regression: mobile viewport must not produce a horizontal page scrollbar.
 *
 * Background (2026-04-16): on current DesktopShell (TopBar legacy) the primary
 * nav links were `shrink-0` and the trailing Chat+Notif+UserMenu cluster was
 * also `shrink-0`, producing ~105px overflow at ≤~485px viewport, observed on
 * staging `meepleai.app`. A follow-up navigation refactor (PR #431, TopBarV2)
 * replaces the top bar with a mobile-first hamburger layout, but the underlying
 * defense — `overflow-x: clip` on `body` and the shell `<main>` — is valuable
 * for both architectures: any future component that mis-sizes content on a
 * narrow viewport should not leak into a page-level horizontal scrollbar.
 *
 * This test asserts only the cross-architecture contract (no page-level
 * horizontal scroll), not the internal shell composition.
 */
import { test, expect } from '@playwright/test';

import { checkNoHorizontalOverflow } from './helpers/responsive-utils';

// Runs in the `mobile-chrome` project (Pixel 5, 390×844).
test.describe('Mobile shell — no horizontal page scrollbar', () => {
  test.beforeEach(async ({ page }) => {
    // Bypass auth via mocks so pages render under PLAYWRIGHT_AUTH_BYPASS
    await page.context().route('**/api/v1/auth/me', async route => {
      await route.fulfill({
        json: {
          id: 'test-user',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
          tier: 'free',
        },
      });
    });

    // Minimal data mocks — pages render empty states without errors
    await page.context().route('**/api/v1/library/*', async route => {
      await route.fulfill({ json: { items: [], total: 0 } });
    });
    await page.context().route('**/api/v1/games*', async route => {
      await route.fulfill({ json: { games: [], total: 0 } });
    });
    await page.context().route('**/api/v1/sessions/active*', async route => {
      await route.fulfill({ json: { sessions: [] } });
    });
    await page.context().route('**/api/v1/agents*', async route => {
      await route.fulfill({ json: [] });
    });
  });

  for (const path of ['/dashboard', '/library']) {
    test(`no page scrollbar on ${path}`, async ({ page }) => {
      await page.goto(path);
      // Wait for any sticky top navigation to be visible (shell-agnostic).
      await page.waitForLoadState('domcontentloaded');

      const { scrollW, clientW } = await page.evaluate(() => ({
        scrollW: document.documentElement.scrollWidth,
        clientW: document.documentElement.clientWidth,
      }));
      expect(
        scrollW,
        `horizontal overflow of ${scrollW - clientW}px on ${path}`
      ).toBeLessThanOrEqual(clientW);

      // Belt-and-braces: body + main containers
      await checkNoHorizontalOverflow(page);
    });
  }
});
