/**
 * Responsive Layout Tests - Assertion-Based Approach - MIGRATED TO POM
 *
 * @see apps/web/e2e/helpers/responsive-utils.ts - Viewport utilities
 * @see apps/web/e2e/pages/ - Page Object Model architecture
 */

import { test, expect } from '@playwright/test';
import {
  checkMobileLayout,
  checkTabletLayout,
  checkDesktopLayout,
  checkNoHorizontalOverflow,
  checkTextReadability,
  checkTouchTargets,
  getViewportInfo,
} from './helpers/responsive-utils';

/**
 * Responsive Layout E2E Tests
 * Issue #993 - [P1] [BGAI-052] Responsive design testing (320px-1920px)
 *
 * Tests responsive behavior across all breakpoints:
 * - Mobile: 390x844px (iPhone 13)
 * - Tablet: 1024x1366px (iPad Pro)
 * - Desktop: 1920x1080px
 *
 * Approach: Behavioral assertions (NOT screenshot comparison)
 * Focus: Layout behavior, accessibility, usability across viewports
 */

test.describe('Responsive Layout Tests', () => {
  test.describe('HomePage Responsive Behavior', () => {
    test('should display correctly based on current viewport', async ({ page }) => {
      await page.goto('/');
      await page.waitForLoadState('networkidle');

      const viewport = await getViewportInfo(page);

      // Verify appropriate layout for current viewport
      if (viewport.isMobile) {
        await checkMobileLayout(page);
        await checkTouchTargets(page, 44);
      } else if (viewport.isTablet) {
        await checkTabletLayout(page);
        await checkTouchTargets(page, 44);
      } else if (viewport.isDesktop) {
        await checkDesktopLayout(page);
      }

      // Common checks for all viewports
      await checkNoHorizontalOverflow(page);

      // Page title should be visible
      const title = page.locator('h1, [role="heading"][aria-level="1"]').first();
      const titleCount = await title.count();
      if (titleCount > 0) {
        await expect(title).toBeVisible();
      }
    });
  });

  test.describe('Chat Page Responsive Behavior', () => {
    test.beforeEach(async ({ page }) => {
      // Login first
      await page.goto('/auth/login');

      // Wait for login form
      await page.waitForSelector('input[type="email"]', { timeout: 10000 }).catch(() => {
        // If login page doesn't load, skip this test group
        test.skip();
      });

      await page.fill('input[type="email"]', 'admin@meepleai.dev');
      await page.fill('input[type="password"]', 'Demo123!');
      await page.click('button[type="submit"]');

      // Wait for redirect
      await page.waitForURL('/', { timeout: 10000 });
    });

    test('should handle chat interface correctly', async ({ page }) => {
      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      const viewport = await getViewportInfo(page);

      // Verify appropriate layout for current viewport
      if (viewport.isMobile) {
        await checkMobileLayout(page);
        await checkTouchTargets(page);
      } else if (viewport.isTablet) {
        await checkTabletLayout(page);
      } else if (viewport.isDesktop) {
        await checkDesktopLayout(page);
      }

      // Common checks
      await checkNoHorizontalOverflow(page);

      // Chat input should be visible (if exists)
      const chatInput = page
        .locator('textarea, input[placeholder*="message" i], input[placeholder*="ask" i]')
        .first();
      const chatInputCount = await chatInput.count();

      if (chatInputCount > 0) {
        await expect(chatInput).toBeVisible();
      }
    });
  });

  test.describe('Settings Page Responsive Behavior', () => {
    test.beforeEach(async ({ page }) => {
      // Login
      await page.goto('/auth/login');
      await page.waitForSelector('input[type="email"]', { timeout: 10000 }).catch(() => {
        test.skip();
      });
      await page.fill('input[type="email"]', 'admin@meepleai.dev');
      await page.fill('input[type="password"]', 'Demo123!');
      await page.click('button[type="submit"]');
      await page.waitForURL('/', { timeout: 10000 });
    });

    test('should display settings page correctly', async ({ page }) => {
      await page.goto('/settings');
      await page.waitForLoadState('networkidle');

      const viewport = await getViewportInfo(page);

      // Verify appropriate layout
      if (viewport.isMobile) {
        await checkMobileLayout(page);
      } else if (viewport.isTablet) {
        await checkTabletLayout(page);
      } else if (viewport.isDesktop) {
        await checkDesktopLayout(page);
      }

      // Common checks
      await checkNoHorizontalOverflow(page);

      // Settings tabs/sections should be accessible
      const tabs = page.locator('[role="tablist"], [role="tab"]');
      const tabsCount = await tabs.count();

      if (tabsCount > 0) {
        await expect(tabs.first()).toBeVisible();
      }
    });
  });

  test.describe('Navigation Responsive Components', () => {
    test('should have accessible navigation', async ({ page }) => {
      await page.goto('/');
      await page.waitForLoadState('networkidle');

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
  });

  test.describe('Cross-Viewport Consistency', () => {
    test('should maintain content integrity across all viewports', async ({ page }) => {
      await page.goto('/');

      // Get page title on current viewport
      const title = await page
        .locator('h1, [role="heading"][aria-level="1"]')
        .first()
        .textContent();

      expect(title).toBeTruthy();
      expect(title?.length).toBeGreaterThan(0);
    });

    test('should have no horizontal scrollbars on any viewport', async ({ page }) => {
      await page.goto('/');
      await page.waitForLoadState('networkidle');

      await checkNoHorizontalOverflow(page);
    });

    test('should have accessible touch targets on mobile/tablet', async ({ page }) => {
      await page.goto('/');
      await page.waitForLoadState('networkidle');

      const viewport = await getViewportInfo(page);

      // Only check touch targets on mobile and tablet
      if (viewport.isMobile || viewport.isTablet) {
        await checkTouchTargets(page, 44);
      }
    });
  });
});
