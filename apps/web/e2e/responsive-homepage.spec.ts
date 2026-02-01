/**
 * Simplified Responsive Tests - HomePage Only (No Auth Required) - MIGRATED TO POM
 *
 * @see apps/web/e2e/helpers/responsive-utils.ts - Viewport utilities
 * @see apps/web/e2e/pages/home/HomePage.ts
 */

import { test, expect } from './fixtures';
import {
  getViewportInfo,
  checkMobileLayout,
  checkTabletLayout,
  checkDesktopLayout,
  checkNoHorizontalOverflow,
  checkTouchTargets,
} from './helpers/responsive-utils';

/**
 * HomePage Responsive Design Tests
 * Issue #993 - [P1] [BGAI-052] Responsive design testing (320px-1920px)
 *
 * Tests responsive behavior on public HomePage across:
 * - Mobile: 390×844px (iPhone 13)
 * - Tablet: 1024×1366px (iPad Pro)
 * - Desktop: 1920×1080px
 *
 * IMPORTANT: Run dev server manually first:
 *   cd apps/web && pnpm dev
 *
 * Then run tests:
 *   pnpm playwright test responsive-homepage
 */

test.describe('HomePage Responsive Design', () => {
  test('should display correctly on current viewport', async ({ page }) => {
    // Navigate to public HomePage
    await page.goto('/');
    await page.waitForLoadState('networkidle', { timeout: 60000 });

    // Get current viewport info
    const viewport = await getViewportInfo(page);

    // Verify appropriate layout for current viewport
    if (viewport.isMobile) {
      console.log('Testing MOBILE viewport:', viewport.width, 'x', viewport.height);
      await checkMobileLayout(page);
      await checkTouchTargets(page);
    } else if (viewport.isTablet) {
      console.log('Testing TABLET viewport:', viewport.width, 'x', viewport.height);
      await checkTabletLayout(page);
      await checkTouchTargets(page);
    } else if (viewport.isDesktop) {
      console.log('Testing DESKTOP viewport:', viewport.width, 'x', viewport.height);
      await checkDesktopLayout(page);
    }

    // Common checks for all viewports
    await checkNoHorizontalOverflow(page);

    // Page should have main content
    const main = page.locator('main, [role="main"]').first();
    await expect(main).toBeVisible();
  });

  test('should have accessible navigation', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle', { timeout: 60000 });

    const viewport = await getViewportInfo(page);

    // Navigation should exist
    const nav = page.locator('nav, [role="navigation"]').first();
    const navCount = await nav.count();
    expect(navCount).toBeGreaterThan(0);

    // On tablet/desktop, navigation should be visible
    if (viewport.isTablet || viewport.isDesktop) {
      await expect(nav).toBeVisible();
    }

    // Touch targets should be large enough on mobile/tablet
    if (viewport.isMobile || viewport.isTablet) {
      await checkTouchTargets(page);
    }
  });

  test('should have no horizontal overflow', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle', { timeout: 60000 });

    // No horizontal scrollbar on any viewport
    await checkNoHorizontalOverflow(page);
  });

  test('should maintain content integrity', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle', { timeout: 60000 });

    // Page should have a title
    const title = page.locator('h1, [role="heading"][aria-level="1"]').first();
    const titleCount = await title.count();

    expect(titleCount).toBeGreaterThan(0);

    if (titleCount > 0) {
      await expect(title).toBeVisible();
      const titleText = await title.textContent();
      expect(titleText).toBeTruthy();
      expect(titleText?.length).toBeGreaterThan(0);
    }
  });
});
