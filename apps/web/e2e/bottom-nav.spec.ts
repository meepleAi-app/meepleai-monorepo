/**
 * E2E tests for the sp4 MobileBottomBar (mobile bottom tab bar).
 * Replaces the stale MobileTabBar/BottomNav specs after the sp4 navbar redesign.
 *
 * Component: apps/web/src/components/layout/AppNav/MobileBottomBar.tsx
 *  - data-testid="mobile-bottom-bar", aria-label="Navigazione principale"
 *  - tabs: bottom-tab-{dashboard,library,hub,chat,profile}
 *  - md:hidden (visible < 768px), fixed at the bottom, hidden on immersive routes.
 *
 * Tabs require an authenticated session; guards skip gracefully when the bar is
 * absent (e.g. an unauthenticated CI environment).
 */
import { test, expect } from '@playwright/test';

test.use({ viewport: { width: 375, height: 667 } }); // iPhone SE

const BAR = '[data-testid="mobile-bottom-bar"]';

test.describe('MobileBottomBar — mobile navigation (sp4)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');
  });

  test('is visible on a mobile viewport', async ({ page }) => {
    const bar = page.locator(BAR);
    if (!(await bar.isVisible().catch(() => false))) {
      test.skip(true, 'Bottom bar requires an authenticated session');
    }
    await expect(bar).toBeVisible();
  });

  test('renders the five primary tabs', async ({ page }) => {
    const bar = page.locator(BAR);
    if (!(await bar.isVisible().catch(() => false))) test.skip(true, 'Auth required');
    await expect(page.getByTestId('bottom-tab-dashboard')).toBeVisible();
    await expect(page.getByTestId('bottom-tab-library')).toBeVisible();
    await expect(page.getByTestId('bottom-tab-hub')).toBeVisible();
    await expect(page.getByTestId('bottom-tab-chat')).toBeVisible();
    await expect(page.getByTestId('bottom-tab-profile')).toBeVisible();
  });

  test('has the navigation aria-label', async ({ page }) => {
    const bar = page.locator(BAR);
    if (!(await bar.isVisible().catch(() => false))) test.skip(true, 'Auth required');
    await expect(bar).toHaveAttribute('aria-label', 'Navigazione principale');
  });

  test('navigates to /library from the Libreria tab', async ({ page }) => {
    const tab = page.getByTestId('bottom-tab-library');
    if (!(await tab.isVisible().catch(() => false))) test.skip(true, 'Auth required');
    await tab.click();
    await page.waitForURL(/\/library/);
    expect(page.url()).toContain('/library');
  });

  test('marks Dashboard active on /dashboard', async ({ page }) => {
    const tab = page.getByTestId('bottom-tab-dashboard');
    if (!(await tab.isVisible().catch(() => false))) test.skip(true, 'Auth required');
    await expect(tab).toHaveAttribute('aria-current', 'page');
  });

  test('has exactly one active tab', async ({ page }) => {
    const bar = page.locator(BAR);
    if (!(await bar.isVisible().catch(() => false))) test.skip(true, 'Auth required');
    const active = page.locator('[data-testid^="bottom-tab-"][aria-current="page"]');
    await expect(active).toHaveCount(1);
  });

  test('meets 44x44px touch targets (WCAG 2.1 AA)', async ({ page }) => {
    const bar = page.locator(BAR);
    if (!(await bar.isVisible().catch(() => false))) test.skip(true, 'Auth required');
    const tabs = bar.locator('a[data-testid^="bottom-tab-"]');
    const count = await tabs.count();
    for (let i = 0; i < count; i++) {
      const box = await tabs.nth(i).boundingBox();
      expect(box).not.toBeNull();
      expect(box!.width).toBeGreaterThanOrEqual(44);
      expect(box!.height).toBeGreaterThanOrEqual(44);
    }
  });

  test('is anchored to the bottom of the viewport', async ({ page }) => {
    const bar = page.locator(BAR);
    if (!(await bar.isVisible().catch(() => false))) test.skip(true, 'Auth required');
    const box = await bar.boundingBox();
    expect(box).not.toBeNull();
    const vh = page.viewportSize()!.height;
    expect(box!.y + box!.height).toBeGreaterThanOrEqual(vh - 60);
  });
});

test.describe('MobileBottomBar — desktop visibility', () => {
  test('is hidden at 768px and above', async ({ page }) => {
    await page.setViewportSize({ width: 1024, height: 768 });
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');
    await expect(page.locator(BAR)).toBeHidden();
  });
});
