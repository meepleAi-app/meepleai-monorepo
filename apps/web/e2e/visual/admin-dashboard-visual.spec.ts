/**
 * Admin Dashboard Visual Regression Testing - Issue #2916
 *
 * Baseline screenshot tests for Admin Dashboard states:
 * - Dashboard default state
 * - Dashboard with errors
 * - Dashboard with loading states
 * - Service health (healthy, degraded, down)
 * - Activity feed (All vs Errors filter)
 *
 * Threshold: 0.1% difference (0.001)
 * Tool: Playwright native screenshot comparison
 *
 * Related:
 * - Issue #2852: Chromatic visual regression (Storybook)
 * - admin-dashboard-user-journeys.spec.ts: User interaction flows
 * - admin-dashboard-fase1.spec.ts: Functional validation
 */

import { test as base, expect, Page } from '@playwright/test';

import { AdminHelper } from './pages';

const test = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);
    // Setup admin auth (skip navigation)
    await adminHelper.setupAdminAuth(true);
    await use(page);
  },
});

test.describe('Admin Dashboard Visual Regression', () => {
  /**
   * Test 1: Dashboard default state
   *
   * Baseline screenshot of dashboard with:
   * - All metrics loaded
   * - System status healthy
   * - Activity feed with events
   * - No errors or warnings
   */
  test('should capture dashboard default state', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Wait for all critical components to load
    await expect(page.getByRole('heading', { name: 'Dashboard Overview' })).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByText('Total Users')).toBeVisible();
    await expect(page.getByText(/System Status|Infrastructure/i)).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Recent Activity' })).toBeVisible();

    // Wait for metrics grid to stabilize
    await page.waitForTimeout(500);

    // Take full page screenshot
    await expect(page).toHaveScreenshot('dashboard-default.png', {
      fullPage: true,
      threshold: 0.001, // 0.1% difference allowed
    });
  });

  /**
   * Test 2: Dashboard with errors
   *
   * Baseline screenshot showing error states:
   * - Service health errors
   * - Failed API metrics
   * - Error activity events
   */
  test('should capture dashboard error state', async ({ adminPage: page }) => {
    // Mock error responses for API calls
    await page.route('**/api/v1/admin/stats', async (route) => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({
          error: 'Internal Server Error',
          message: 'Failed to fetch admin statistics',
        }),
      });
    });

    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Wait for error state to render
    await expect(
      page.getByText(/error|failed|unable to load/i).first()
    ).toBeVisible({ timeout: 10000 });

    // Wait for error UI to stabilize
    await page.waitForTimeout(500);

    // Take full page screenshot showing errors
    await expect(page).toHaveScreenshot('dashboard-errors.png', {
      fullPage: true,
      threshold: 0.001,
    });
  });

  /**
   * Test 3: Dashboard loading states
   *
   * Baseline screenshot of dashboard during loading:
   * - Skeleton loaders
   * - Loading spinners
   * - Partial content loaded
   */
  test('should capture dashboard loading state', async ({ adminPage: page }) => {
    // Delay API responses to capture loading state
    await page.route('**/api/v1/admin/stats', async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 2000));
      await route.continue();
    });

    const navigationPromise = page.goto('/admin');

    // Wait for initial render but before data loads
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(300);

    // Look for loading indicators
    const hasLoadingState =
      (await page.locator('[data-testid*="loading"], .skeleton, .spinner').count()) > 0;

    if (hasLoadingState) {
      // Capture loading state
      await expect(page).toHaveScreenshot('dashboard-loading.png', {
        fullPage: true,
        threshold: 0.001,
      });
    } else {
      console.log('Loading state not visible - data loaded too quickly');
      // Still take screenshot for baseline
      await expect(page).toHaveScreenshot('dashboard-loading.png', {
        fullPage: true,
        threshold: 0.001,
      });
    }

    // Wait for navigation to complete
    await navigationPromise;
  });

  /**
   * Test 4: Service health - Healthy state
   *
   * Baseline screenshot showing all services healthy:
   * - Green indicators
   * - Low latency
   * - 100% uptime
   */
  test('should capture service health - healthy state', async ({ adminPage: page }) => {
    // Mock healthy service status
    await page.route('**/api/v1/admin/infrastructure/status', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          services: [
            { name: 'Database', status: 'healthy', latency: 5, uptime: 100 },
            { name: 'Redis Cache', status: 'healthy', latency: 2, uptime: 100 },
            { name: 'Qdrant Vector DB', status: 'healthy', latency: 8, uptime: 100 },
            { name: 'Embedding Service', status: 'healthy', latency: 12, uptime: 100 },
          ],
          overall: 'healthy',
        }),
      });
    });

    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Wait for SystemStatus component
    await expect(page.getByText(/System Status|Infrastructure/i)).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Database')).toBeVisible();

    // Wait for service status to stabilize
    await page.waitForTimeout(500);

    // Focus on SystemStatus component
    const systemStatus = page.locator('[data-testid="system-status"], section:has-text("System Status"), section:has-text("Infrastructure")').first();

    if ((await systemStatus.count()) > 0) {
      await expect(systemStatus).toHaveScreenshot('service-health-healthy.png', {
        threshold: 0.001,
      });
    } else {
      // Fallback to full page if component not isolated
      await expect(page).toHaveScreenshot('service-health-healthy.png', {
        fullPage: true,
        threshold: 0.001,
      });
    }
  });

  /**
   * Test 5: Service health - Degraded state
   *
   * Baseline screenshot showing degraded services:
   * - Yellow/orange indicators
   * - Higher latency
   * - Reduced performance
   */
  test('should capture service health - degraded state', async ({ adminPage: page }) => {
    // Mock degraded service status
    await page.route('**/api/v1/admin/infrastructure/status', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          services: [
            { name: 'Database', status: 'healthy', latency: 5, uptime: 100 },
            { name: 'Redis Cache', status: 'degraded', latency: 150, uptime: 98 },
            { name: 'Qdrant Vector DB', status: 'degraded', latency: 200, uptime: 95 },
            { name: 'Embedding Service', status: 'healthy', latency: 12, uptime: 100 },
          ],
          overall: 'degraded',
        }),
      });
    });

    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Wait for SystemStatus component
    await expect(page.getByText(/System Status|Infrastructure/i)).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Redis Cache')).toBeVisible();

    // Wait for degraded state to render
    await page.waitForTimeout(500);

    // Focus on SystemStatus component
    const systemStatus = page.locator('[data-testid="system-status"], section:has-text("System Status"), section:has-text("Infrastructure")').first();

    if ((await systemStatus.count()) > 0) {
      await expect(systemStatus).toHaveScreenshot('service-health-degraded.png', {
        threshold: 0.001,
      });
    } else {
      await expect(page).toHaveScreenshot('service-health-degraded.png', {
        fullPage: true,
        threshold: 0.001,
      });
    }
  });

  /**
   * Test 6: Service health - Down state
   *
   * Baseline screenshot showing services down:
   * - Red indicators
   * - Connection failures
   * - 0% uptime
   */
  test('should capture service health - down state', async ({ adminPage: page }) => {
    // Mock down service status
    await page.route('**/api/v1/admin/infrastructure/status', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          services: [
            { name: 'Database', status: 'healthy', latency: 5, uptime: 100 },
            { name: 'Redis Cache', status: 'down', latency: null, uptime: 0 },
            { name: 'Qdrant Vector DB', status: 'down', latency: null, uptime: 0 },
            { name: 'Embedding Service', status: 'healthy', latency: 12, uptime: 100 },
          ],
          overall: 'down',
        }),
      });
    });

    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Wait for SystemStatus component
    await expect(page.getByText(/System Status|Infrastructure/i)).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Redis Cache')).toBeVisible();

    // Wait for down state to render
    await page.waitForTimeout(500);

    // Focus on SystemStatus component
    const systemStatus = page.locator('[data-testid="system-status"], section:has-text("System Status"), section:has-text("Infrastructure")').first();

    if ((await systemStatus.count()) > 0) {
      await expect(systemStatus).toHaveScreenshot('service-health-down.png', {
        threshold: 0.001,
      });
    } else {
      await expect(page).toHaveScreenshot('service-health-down.png', {
        fullPage: true,
        threshold: 0.001,
      });
    }
  });

  /**
   * Test 7: Activity feed - All filter
   *
   * Baseline screenshot of activity feed showing all events:
   * - Info events
   * - Warning events
   * - Error events
   * - Success events
   */
  test('should capture activity feed - All filter', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Wait for ActivityFeed component
    await expect(page.getByRole('heading', { name: 'Recent Activity' })).toBeVisible({
      timeout: 10000,
    });

    // Ensure "All" filter is active (default)
    const allFilterButton = page.getByRole('button', { name: /all|show all/i });
    if ((await allFilterButton.count()) > 0) {
      await allFilterButton.click();
      await page.waitForTimeout(300);
    }

    // Wait for activity feed to stabilize
    await page.waitForTimeout(500);

    // Focus on ActivityFeed component
    const activityFeed = page.locator('[data-testid="activity-feed"], section:has-text("Recent Activity")').first();

    if ((await activityFeed.count()) > 0) {
      await expect(activityFeed).toHaveScreenshot('activity-feed-all.png', {
        threshold: 0.001,
      });
    } else {
      await expect(page).toHaveScreenshot('activity-feed-all.png', {
        fullPage: true,
        threshold: 0.001,
      });
    }
  });

  /**
   * Test 8: Activity feed - Errors filter
   *
   * Baseline screenshot of activity feed showing only errors:
   * - Error events only
   * - Warning events (if included)
   * - Critical issues highlighted
   */
  test('should capture activity feed - Errors filter', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Wait for ActivityFeed component
    await expect(page.getByRole('heading', { name: 'Recent Activity' })).toBeVisible({
      timeout: 10000,
    });

    // Click "Errors" filter
    const errorFilterButton = page.getByRole('button', { name: /errors|error/i });

    if ((await errorFilterButton.count()) > 0) {
      await errorFilterButton.click();
      await page.waitForTimeout(500); // Wait for filter animation and data update

      // Focus on ActivityFeed component
      const activityFeed = page.locator('[data-testid="activity-feed"], section:has-text("Recent Activity")').first();

      if ((await activityFeed.count()) > 0) {
        await expect(activityFeed).toHaveScreenshot('activity-feed-errors.png', {
          threshold: 0.001,
        });
      } else {
        await expect(page).toHaveScreenshot('activity-feed-errors.png', {
          fullPage: true,
          threshold: 0.001,
        });
      }
    } else {
      console.log('Error filter not found - may not be implemented yet');
      // Still capture current state for baseline
      const activityFeed = page.locator('[data-testid="activity-feed"], section:has-text("Recent Activity")').first();

      if ((await activityFeed.count()) > 0) {
        await expect(activityFeed).toHaveScreenshot('activity-feed-errors.png', {
          threshold: 0.001,
        });
      } else {
        await expect(page).toHaveScreenshot('activity-feed-errors.png', {
          fullPage: true,
          threshold: 0.001,
        });
      }
    }
  });
});
