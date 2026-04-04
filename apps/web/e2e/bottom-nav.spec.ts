/**
 * E2E tests for MobileTabBar component
 * Issue #1829 [UI-002] BottomNav Component (Mobile-First)
 * Updated: mobile UX overhaul — MobileTabBar replaces deprecated BottomNav
 *
 * Test Coverage:
 * - Mobile visibility (< 768px)
 * - Desktop hiding (≥ 768px)
 * - Navigation functionality (routes)
 * - Active state visual verification
 * - Touch target accessibility (44x44px)
 * - Keyboard navigation
 *
 * Architecture Notes (post mobile-ux overhaul):
 * - SmartFAB: deleted (replaced by morphing center tab)
 * - MobileBreadcrumb: deleted (replaced by inline breadcrumb in content)
 * - ContextualBottomNav: hidden on mobile (hidden md:flex)
 * - HandDrawer: desktop-only
 * - MobileTabBar: only bottom nav visible on mobile (md:hidden)
 */

import { test, expect } from '@playwright/test';

// Use mobile viewport by default
test.use({
  viewport: { width: 375, height: 667 }, // iPhone SE
});

test.describe('MobileTabBar - Mobile Navigation', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to authenticated page (assumes auth middleware)
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');
  });

  test('should be visible on mobile viewport (375px)', async ({ page }) => {
    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    await expect(tabBar).toBeVisible();
  });

  test('should render navigation items with correct labels', async ({ page }) => {
    // MobileTabBar shows auth-gated tabs; at minimum Dashboard and Discover are always visible
    await expect(page.getByTestId('mobile-tab-dashboard')).toBeVisible();
    await expect(page.getByTestId('mobile-tab-discover')).toBeVisible();
  });

  test('should have correct ARIA label', async ({ page }) => {
    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    await expect(tabBar).toHaveAttribute('aria-label', 'Primary navigation');
  });

  test('should navigate to library when Discover tab clicked', async ({ page }) => {
    const discoverLink = page.getByTestId('mobile-tab-discover');
    await discoverLink.click();

    await page.waitForURL('/library');
    expect(page.url()).toContain('/library');
  });

  test('should navigate to Chat page when Chat tab clicked (authenticated)', async ({ page }) => {
    const chatTab = page.getByTestId('mobile-tab-chat');
    const isVisible = await chatTab.isVisible().catch(() => false);
    if (!isVisible) test.skip(true, 'Chat tab only visible when authenticated');

    await chatTab.click();
    await page.waitForURL(/\/chat/);
    expect(page.url()).toContain('/chat');
  });

  test('should navigate to Profile page when Profile tab clicked (authenticated)', async ({
    page,
  }) => {
    const profileTab = page.getByTestId('mobile-tab-profile');
    const isVisible = await profileTab.isVisible().catch(() => false);
    if (!isVisible) test.skip(true, 'Profile tab only visible when authenticated');

    await profileTab.click();
    await page.waitForURL('/profile');
    expect(page.url()).toContain('/profile');
  });

  test('should mark Dashboard as active on /dashboard', async ({ page }) => {
    await page.goto('/dashboard');

    const dashboardLink = page.getByTestId('mobile-tab-dashboard');
    await expect(dashboardLink).toHaveAttribute('aria-current', 'page');
  });

  test('should mark Discover as active on /library', async ({ page }) => {
    await page.goto('/library');
    await page.waitForLoadState('networkidle');

    const discoverLink = page.getByTestId('mobile-tab-discover');
    await expect(discoverLink).toHaveAttribute('aria-current', 'page');
  });

  test('should have only one active tab at a time', async ({ page }) => {
    await page.goto('/library');
    await page.waitForLoadState('networkidle');

    const activeLinks = page.locator('[data-testid^="mobile-tab-"][aria-current="page"]');
    await expect(activeLinks).toHaveCount(1);
  });

  test('should have minimum 44x44px touch targets (WCAG 2.1 AA)', async ({ page }) => {
    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    // Select both link and button children (center tab may be a button)
    const targets = tabBar.locator('a, button[data-testid^="mobile-tab-"]');
    const count = await targets.count();

    for (let i = 0; i < count; i++) {
      const box = await targets.nth(i).boundingBox();
      expect(box).not.toBeNull();
      expect(box!.width).toBeGreaterThanOrEqual(44);
      expect(box!.height).toBeGreaterThanOrEqual(44);
    }
  });

  test('should be fixed at bottom of viewport', async ({ page }) => {
    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    const box = await tabBar.boundingBox();

    expect(box).not.toBeNull();
    const viewportHeight = page.viewportSize()!.height;
    // Allow for safe-area padding on devices
    expect(box!.y + box!.height).toBeGreaterThanOrEqual(viewportHeight - 60);
  });
});

test.describe('MobileTabBar - Desktop Visibility', () => {
  test('should be hidden on desktop viewport (≥768px)', async ({ page }) => {
    await page.setViewportSize({ width: 1024, height: 768 });
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');

    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    await expect(tabBar).toBeHidden();
  });

  test('should be hidden on tablet viewport (768px)', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');

    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    await expect(tabBar).toBeHidden();
  });
});

test.describe('MobileTabBar - Keyboard Navigation', () => {
  test('should support Tab key navigation', async ({ page }) => {
    await page.goto('/dashboard');

    // Tab to first link
    await page.keyboard.press('Tab');
    let focused = await page.evaluate(() => document.activeElement?.getAttribute('aria-label'));
    expect(focused).toBeTruthy();

    // Tab through remaining nav items
    for (let i = 0; i < 3; i++) {
      await page.keyboard.press('Tab');
    }

    // Should still be within navigation elements
    focused = await page.evaluate(() => document.activeElement?.getAttribute('aria-label'));
    expect(focused).toBeTruthy();
  });

  test('should activate Discover link with Enter key navigates to library', async ({ page }) => {
    await page.goto('/dashboard');

    const discoverLink = page.getByTestId('mobile-tab-discover');
    await discoverLink.focus();
    await page.keyboard.press('Enter');

    await page.waitForURL('/library');
    expect(page.url()).toContain('/library');
  });

  test('should show focus ring on keyboard focus', async ({ page }) => {
    await page.goto('/dashboard');

    const dashboardLink = page.getByTestId('mobile-tab-dashboard');
    await dashboardLink.focus();

    // Check for focus-visible ring (Tailwind: ring-2 ring-primary)
    const hasRing = await dashboardLink.evaluate(el => {
      const styles = window.getComputedStyle(el);
      // Check for ring styles (box-shadow or outline)
      return styles.boxShadow !== 'none' || styles.outline !== 'none';
    });

    expect(hasRing).toBeTruthy();
  });
});

test.describe('MobileTabBar - Visual Regression (Chromatic)', () => {
  test('should match visual snapshot - Dashboard active', async ({ page }) => {
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');

    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    await expect(tabBar).toHaveScreenshot('mobile-tab-bar-dashboard-active.png');
  });

  test('should match visual snapshot - Library active', async ({ page }) => {
    await page.goto('/library');
    await page.waitForLoadState('networkidle');

    const tabBar = page.locator('[data-testid="mobile-tab-bar"]');
    await expect(tabBar).toHaveScreenshot('mobile-tab-bar-library-active.png');
  });
});

test.describe('MobileTabBar - Responsive Breakpoints', () => {
  const viewports = [
    { name: 'Mobile S (320px)', width: 320, height: 568 },
    { name: 'Mobile M (375px)', width: 375, height: 667 },
    { name: 'Mobile L (425px)', width: 425, height: 812 },
    { name: 'Tablet (768px)', width: 768, height: 1024 },
  ];

  for (const viewport of viewports) {
    test(`should render correctly on ${viewport.name}`, async ({ page }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await page.goto('/dashboard');
      await page.waitForLoadState('networkidle');

      const tabBar = page.locator('[data-testid="mobile-tab-bar"]');

      if (viewport.width < 768) {
        await expect(tabBar).toBeVisible();

        // Core tabs always visible (auth-gated tabs may not appear without login)
        await expect(page.getByTestId('mobile-tab-dashboard')).toBeVisible();
        await expect(page.getByTestId('mobile-tab-discover')).toBeVisible();
      } else {
        // MobileTabBar is md:hidden — hidden at 768px and above
        await expect(tabBar).toBeHidden();
      }
    });
  }
});
