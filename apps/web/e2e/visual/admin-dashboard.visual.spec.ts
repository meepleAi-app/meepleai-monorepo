/**
 * Visual Regression Tests - Admin Dashboard
 *
 * Issue #2906: Visual regression testing for MeepleAI design system
 * Epic #2845: MeepleAI Design System Integration
 *
 * These tests capture screenshots and compare them against baseline images
 * to detect unintended visual changes in the admin dashboard.
 *
 * Baseline Management:
 * - Generate baselines: pnpm test:e2e:visual --update-snapshots
 * - Update specific test: pnpm test:e2e:visual --update-snapshots -g "dashboard overview"
 * - Baselines stored in: e2e/visual/__screenshots__/
 *
 * Configuration:
 * - maxDiffPixels: 100 (allow minor anti-aliasing differences)
 * - threshold: 0.2 (20% pixel difference tolerance)
 */

import { expect, test } from '@playwright/test';

// Helper: Login as admin user
async function loginAsAdmin(page: any) {
  // TODO: Replace with actual admin login flow
  // For now, assume test environment has auto-login or mock auth
  await page.goto('/admin');
  await page.waitForLoadState('networkidle');
}

// Helper: Wait for charts to render
async function waitForChartsToRender(page: any) {
  // Wait for chart canvas elements to be visible
  await page.waitForSelector('canvas', { state: 'visible', timeout: 10000 });
  // Additional wait for chart animations to complete
  await page.waitForTimeout(1000);
}

test.describe('Admin Dashboard - Visual Regression', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test.describe('Desktop (1920x1080)', () => {
    test.use({ viewport: { width: 1920, height: 1080 } });

    test('dashboard overview matches baseline', async ({ page }) => {
      await page.goto('/admin');
      await waitForChartsToRender(page);

      // Capture full page screenshot
      await expect(page).toHaveScreenshot('admin-dashboard-desktop.png', {
        fullPage: true,
        maxDiffPixels: 100,
        threshold: 0.2,
      });
    });

    test('metrics grid cards match baseline', async ({ page }) => {
      await page.goto('/admin');

      // Locate metrics grid section
      const metricsGrid = page.locator('.grid').first();
      await expect(metricsGrid).toBeVisible();

      // Capture metrics grid screenshot
      await expect(metricsGrid).toHaveScreenshot('metrics-grid-desktop.png', {
        maxDiffPixels: 50,
        threshold: 0.2,
      });
    });

    test('charts section matches baseline', async ({ page }) => {
      await page.goto('/admin');
      await waitForChartsToRender(page);

      // Locate charts section (second grid on page)
      const chartsSection = page.locator('.grid').nth(1);
      await expect(chartsSection).toBeVisible();

      // Capture charts screenshot
      await expect(chartsSection).toHaveScreenshot('charts-section-desktop.png', {
        maxDiffPixels: 150, // Charts may have minor variations
        threshold: 0.25,
      });
    });

    test('navigation and top bar match baseline', async ({ page }) => {
      await page.goto('/admin');

      // Capture top navigation area
      const topBar = page.locator('main > div').first();
      await expect(topBar).toBeVisible();

      await expect(topBar).toHaveScreenshot('navigation-topbar-desktop.png', {
        maxDiffPixels: 50,
        threshold: 0.2,
      });
    });

    test('filters and controls match baseline', async ({ page }) => {
      await page.goto('/admin');

      // Locate filter section
      const filterSection = page.locator('input[type="text"]').first().locator('..');
      await expect(filterSection).toBeVisible();

      await expect(filterSection).toHaveScreenshot('filters-controls-desktop.png', {
        maxDiffPixels: 50,
        threshold: 0.2,
      });
    });
  });

  test.describe('Tablet (768x1024)', () => {
    test.use({ viewport: { width: 768, height: 1024 } });

    test('dashboard overview tablet matches baseline', async ({ page }) => {
      await page.goto('/admin');
      await waitForChartsToRender(page);

      await expect(page).toHaveScreenshot('admin-dashboard-tablet.png', {
        fullPage: true,
        maxDiffPixels: 100,
        threshold: 0.2,
      });
    });

    test('metrics grid responsive layout matches baseline', async ({ page }) => {
      await page.goto('/admin');

      const metricsGrid = page.locator('.grid').first();
      await expect(metricsGrid).toBeVisible();

      await expect(metricsGrid).toHaveScreenshot('metrics-grid-tablet.png', {
        maxDiffPixels: 50,
        threshold: 0.2,
      });
    });
  });

  test.describe('Mobile (375x667)', () => {
    test.use({ viewport: { width: 375, height: 667 } });

    test('dashboard overview mobile matches baseline', async ({ page }) => {
      await page.goto('/admin');
      await waitForChartsToRender(page);

      await expect(page).toHaveScreenshot('admin-dashboard-mobile.png', {
        fullPage: true,
        maxDiffPixels: 100,
        threshold: 0.2,
      });
    });

    test('metrics grid mobile layout matches baseline', async ({ page }) => {
      await page.goto('/admin');

      const metricsGrid = page.locator('.grid').first();
      await expect(metricsGrid).toBeVisible();

      await expect(metricsGrid).toHaveScreenshot('metrics-grid-mobile.png', {
        maxDiffPixels: 50,
        threshold: 0.2,
      });
    });

    test('navigation mobile responsive matches baseline', async ({ page }) => {
      await page.goto('/admin');

      const topBar = page.locator('main > div').first();
      await expect(topBar).toBeVisible();

      await expect(topBar).toHaveScreenshot('navigation-mobile.png', {
        maxDiffPixels: 50,
        threshold: 0.2,
      });
    });
  });

  test.describe('Interactive States', () => {
    test.use({ viewport: { width: 1920, height: 1080 } });

    test('metric card hover state matches baseline', async ({ page }) => {
      await page.goto('/admin');

      // Find first metric card
      const metricCard = page.locator('.grid > div').first();
      await expect(metricCard).toBeVisible();

      // Hover over card
      await metricCard.hover();
      await page.waitForTimeout(300); // Wait for hover animation

      await expect(metricCard).toHaveScreenshot('metric-card-hover.png', {
        maxDiffPixels: 75,
        threshold: 0.2,
      });
    });

    test('button hover states match baseline', async ({ page }) => {
      await page.goto('/admin');

      // Find export button
      const exportButton = page.getByRole('button', { name: /export csv/i });
      await expect(exportButton).toBeVisible();

      // Hover over button
      await exportButton.hover();
      await page.waitForTimeout(200); // Wait for hover effect

      await expect(exportButton).toHaveScreenshot('export-button-hover.png', {
        maxDiffPixels: 50,
        threshold: 0.2,
      });
    });
  });

  test.describe('Loading States', () => {
    test.use({ viewport: { width: 1920, height: 1080 } });

    test('loading state matches baseline', async ({ page }) => {
      // Intercept API calls to simulate loading
      await page.route('**/api/v1/admin/**', route => {
        // Delay response to capture loading state
        setTimeout(() => route.continue(), 5000);
      });

      await page.goto('/admin');

      // Capture loading state before data loads
      await page.waitForTimeout(500);

      await expect(page).toHaveScreenshot('admin-dashboard-loading.png', {
        fullPage: false,
        maxDiffPixels: 100,
        threshold: 0.2,
      });
    });
  });

  test.describe('Background Texture System (Issue #2905)', () => {
    test.use({ viewport: { width: 1920, height: 1080 } });

    test('background texture visible in screenshot', async ({ page }) => {
      await page.goto('/admin');

      // Capture a section that should show the background texture
      const mainContainer = page.locator('main');
      await expect(mainContainer).toBeVisible();

      await expect(mainContainer).toHaveScreenshot('background-texture-visible.png', {
        maxDiffPixels: 100,
        threshold: 0.2,
      });
    });

    test('reduced motion disables texture', async ({ page, context }) => {
      // Enable prefers-reduced-motion
      await page.emulateMedia({ reducedMotion: 'reduce' });

      await page.goto('/admin');

      const mainContainer = page.locator('main');
      await expect(mainContainer).toBeVisible();

      await expect(mainContainer).toHaveScreenshot('background-texture-reduced-motion.png', {
        maxDiffPixels: 100,
        threshold: 0.2,
      });
    });
  });
});
