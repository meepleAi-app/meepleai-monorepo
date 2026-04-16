/**
 * Regression: mobile viewport must not overflow horizontally.
 *
 * Original bug (2026-04-16): DesktopShell's TopBar had desktop-only layout
 * with shrink-0 NavLinks + desktop-sized SearchPill + trailing group,
 * producing ~105px horizontal overflow at 485px viewport.
 *
 * Fix: hamburger menu below md breakpoint, icon-only search pill on mobile,
 * compact logo, responsive MiniNavSlot padding, overflow-x-clip safety nets
 * on body + shell <main>.
 */
import { test, expect } from '@playwright/test';

import { checkNoHorizontalOverflow } from './helpers/responsive-utils';

// Run in the mobile-chrome project where viewport is 390×844
test.describe('Mobile shell — no horizontal overflow', () => {
  test.beforeEach(async ({ page }) => {
    // Mock auth to bypass login/middleware for authenticated routes
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

    // Minimal data mocks so pages render without errors
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
      // Wait for the UserShell TopBar to hydrate
      await expect(page.locator('[data-testid="top-bar"]')).toBeVisible();

      // Primary assertion: scrollWidth must equal clientWidth on html
      const { scrollW, clientW } = await page.evaluate(() => ({
        scrollW: document.documentElement.scrollWidth,
        clientW: document.documentElement.clientWidth,
      }));
      expect(
        scrollW,
        `horizontal overflow of ${scrollW - clientW}px on ${path}`
      ).toBeLessThanOrEqual(clientW);

      // Secondary assertion via shared helper (body + main containers)
      await checkNoHorizontalOverflow(page);
    });
  }

  test('hamburger menu button is visible on mobile', async ({ page }) => {
    await page.goto('/dashboard');
    const hamburger = page.getByRole('button', { name: /apri menu/i });
    await expect(hamburger).toBeVisible();
  });

  test('desktop primary nav is hidden on mobile', async ({ page }) => {
    await page.goto('/dashboard');
    const desktopNav = page.locator('nav[aria-label="Primary"]');
    // Element is in DOM but hidden via CSS (`hidden md:flex`)
    await expect(desktopNav).toBeHidden();
  });
});
