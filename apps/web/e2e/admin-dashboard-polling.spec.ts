/**
 * Admin Dashboard Polling E2E Test - Issue #888
 *
 * Tests dashboard dynamic behavior with 30s auto-refresh:
 * 1. Login as admin
 * 2. Navigate to dashboard
 * 3. Verify 12 metrics displayed
 * 4. Verify activity feed
 * 5. Wait for auto-refresh (30s)
 * 6. Verify metrics updated
 * 7. Quick actions navigation
 *
 * Effort: 5h
 * Dependencies: #885 (Dashboard page), #886 (API integration + polling)
 */

import { test as base, expect, Page } from './fixtures/chromatic';
import { AdminHelper } from './pages';

const test = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);

    // Setup admin auth (skip navigation - we'll do it manually)
    await adminHelper.setupAdminAuth(true);

    // ✅ REMOVED MOCK: Use real Admin Analytics and Activity APIs
    // Real backend GET /api/v1/admin/analytics must return metrics with 30s polling
    // Real backend GET /api/v1/admin/activity must return recent events
    // Note: Polling test verifies API calls, not specific value changes

    await use(page);
  },
});

test.describe('Admin Dashboard Polling - Issue #888', () => {
  test('should display 12 core metrics on dashboard load', async ({ adminPage: page }) => {
    // Step 1 & 2: Navigate to dashboard
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Verify page title
    await expect(page.getByRole('heading', { name: 'Dashboard Overview' })).toBeVisible({
      timeout: 10000,
    });

    // ✅ CHANGED: Verify 12 core metrics labels (not specific mock values)
    // Backend returns real data, test verifies structure only
    await expect(page.getByText('Total Users')).toBeVisible();
    await expect(page.getByText('Active Sessions')).toBeVisible();
    await expect(page.getByText('Total Games')).toBeVisible();
    await expect(page.getByText('API Requests (24h)')).toBeVisible();
    await expect(page.getByText('Total PDFs')).toBeVisible();
    await expect(page.getByText('Total Chat Messages')).toBeVisible();
    await expect(page.getByText('Avg Confidence')).toBeVisible();
    await expect(page.getByText('Total RAG Requests')).toBeVisible();
    await expect(page.getByText('Avg Latency (24h)')).toBeVisible();
    await expect(page.getByText('Error Rate (24h)')).toBeVisible();
    await expect(page.getByText('Active Alerts')).toBeVisible();
    await expect(page.getByText('Total Tokens')).toBeVisible();
  });

  test('should display activity feed', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // ✅ CHANGED: Verify activity feed structure (not specific mock events)
    await expect(page.getByRole('heading', { name: 'Recent Activity' })).toBeVisible();
    // Verify at least one activity event is displayed (backend seeded data)
    const activityItems = page
      .locator('[data-testid="activity-item"]')
      .or(page.locator('li, div').filter({ hasText: /registered|uploaded|created|updated/i }));
    await expect(activityItems.first()).toBeVisible({ timeout: 5000 });
  });

  test('should auto-refresh metrics after 30 seconds', async ({ adminPage: page }) => {
    // Navigate to dashboard
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // ✅ CHANGED: Verify polling behavior by tracking API calls, not specific value changes
    // Step 1: Wait for initial analytics API call
    const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
    let initialCallReceived = false;

    await page.waitForResponse(
      response => {
        if (response.url().startsWith(`${apiBase}/api/v1/admin/analytics`)) {
          initialCallReceived = true;
          return true;
        }
        return false;
      },
      { timeout: 5000 }
    );

    expect(initialCallReceived).toBe(true);

    // Step 2: Wait for 30s polling refresh (React Query interval - Issue #886)
    // Verify second API call happens after 30s
    const secondCall = page.waitForResponse(
      response => response.url().startsWith(`${apiBase}/api/v1/admin/analytics`),
      { timeout: 35000 } // 35s to account for timing variations
    );

    await secondCall;
    // If we reach here, polling worked ✅
  });

  test('should navigate via quick actions', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Step 7: Quick actions navigation
    // Verify Quick Actions section exists
    await expect(page.getByRole('heading', { name: 'Quick Actions' })).toBeVisible();

    // Test "Manage Users" action
    await expect(page.getByRole('link', { name: /Manage Users/i })).toBeVisible();
    await page.getByRole('link', { name: /Manage Users/i }).click();
    await expect(page).toHaveURL('/admin/users');

    // Navigate back to dashboard
    await page.getByRole('link', { name: 'Dashboard' }).click();
    await expect(page).toHaveURL('/admin');

    // Test "View Alerts" action
    await expect(page.getByRole('link', { name: /View Alerts/i })).toBeVisible();
    await page.getByRole('link', { name: /View Alerts/i }).click();
    await expect(page).toHaveURL('/admin/configuration');

    // Navigate back to dashboard
    await page.getByRole('link', { name: 'Dashboard' }).click();
    await expect(page).toHaveURL('/admin');

    // Test "Prompts" action
    await expect(page.getByRole('link', { name: /Prompts/i })).toBeVisible();
    await page.getByRole('link', { name: /Prompts/i }).click();
    await expect(page).toHaveURL('/admin/prompts');

    // Navigate back to dashboard
    await page.getByRole('link', { name: 'Dashboard' }).click();
    await expect(page).toHaveURL('/admin');

    // Test "Cache" action
    await expect(page.getByRole('link', { name: /Cache/i })).toBeVisible();
    await page.getByRole('link', { name: /Cache/i }).click();
    await expect(page).toHaveURL('/admin/cache');
  });

  test('should show active alerts badge in quick actions', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Verify alert badge shows active alerts count
    const alertsAction = page.locator('text=View Alerts').locator('..');
    await expect(alertsAction).toBeVisible();

    // Badge should show "2" (activeAlerts from mock data)
    await expect(page.locator('text=View Alerts').locator('..').locator('text=2')).toBeVisible();
  });

  test('should show last updated timestamp', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Verify last updated timestamp is displayed
    await expect(page.getByText(/Last updated:/)).toBeVisible();

    // Verify timestamp updates after refresh
    const firstTimestamp = await page.getByText(/Last updated:/).textContent();

    // Wait for refresh
    await page.waitForTimeout(31000);

    // Get new timestamp
    const secondTimestamp = await page.getByText(/Last updated:/).textContent();

    // Timestamps should be different
    expect(firstTimestamp).not.toBe(secondTimestamp);
  });

  test('complete dashboard polling journey', async ({ adminPage: page }) => {
    // 1. Navigate to dashboard
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // 2. Verify initial load with 12 core metrics
    await expect(page.getByRole('heading', { name: 'Dashboard Overview' })).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByText('Total Users')).toBeVisible();
    await expect(page.getByText('1,247')).toBeVisible();

    // 3. Verify activity feed
    await expect(page.getByRole('heading', { name: 'Recent Activity' })).toBeVisible();
    await expect(page.getByText(/New user registered/)).toBeVisible();

    // 4. Verify quick actions
    await expect(page.getByRole('heading', { name: 'Quick Actions' })).toBeVisible();
    await expect(page.getByRole('link', { name: /Manage Users/i })).toBeVisible();

    // 5. Wait for auto-refresh (30s)
    await page.waitForTimeout(31000);

    // 6. Verify metrics updated after polling
    await expect(page.getByText('1,257')).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('45')).toBeVisible();

    // 7. Test navigation via quick action
    await page.getByRole('link', { name: /Manage Users/i }).click();
    await expect(page).toHaveURL('/admin/users');

    // 8. Navigate back and verify dashboard still works
    await page.getByRole('link', { name: 'Dashboard' }).click();
    await expect(page).toHaveURL('/admin');
    await expect(page.getByText('Total Users')).toBeVisible();
  });

  test('should handle system status refresh', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Verify System Status section
    await expect(page.getByRole('heading', { name: 'System Status' })).toBeVisible();

    // Verify service statuses are displayed
    await expect(page.getByText('Database')).toBeVisible();
    await expect(page.getByText('Redis Cache')).toBeVisible();
    await expect(page.getByText('Vector Store')).toBeVisible();
    await expect(page.getByText('AI Services')).toBeVisible();

    // Click refresh button if available
    const refreshButton = page.getByRole('button', { name: /refresh/i }).first();
    if (await refreshButton.isVisible()) {
      await refreshButton.click();
      await page.waitForTimeout(1000);
      // Verify system status is still visible after refresh
      await expect(page.getByRole('heading', { name: 'System Status' })).toBeVisible();
    }
  });

  /**
   * Journey 6: Real-time updates (30s polling verification) - Issue #2915
   *
   * Tests real-time polling behavior with visual verification:
   * 1. Load dashboard with initial metrics
   * 2. Capture initial state screenshot
   * 3. Wait 30 seconds for auto-refresh
   * 4. Verify polling API call occurred
   * 5. Verify UI updated with new data
   * 6. Capture post-update screenshot
   */
  test('Journey 6: real-time polling updates with visual verification', async ({
    adminPage: page,
  }) => {
    const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

    // 1. Navigate to dashboard
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // 2. Verify initial load
    await expect(page.getByRole('heading', { name: 'Dashboard Overview' })).toBeVisible({
      timeout: 10000,
    });

    // 3. Wait for initial analytics API call
    await page.waitForResponse(
      response => response.url().startsWith(`${apiBase}/api/v1/admin/analytics`),
      { timeout: 5000 }
    );

    // 4. Capture initial state screenshot
    await expect(page).toHaveScreenshot('polling-before-update.png');

    // 5. Record timestamp before waiting
    const timestampBefore = await page.getByText(/Last updated:/).textContent();

    // 6. Wait for 30s polling refresh
    console.log('⏳ Waiting 30 seconds for auto-refresh polling...');

    // Setup listener for second API call
    const secondCallPromise = page.waitForResponse(
      response => response.url().startsWith(`${apiBase}/api/v1/admin/analytics`),
      { timeout: 35000 }
    );

    // Wait for the polling to trigger
    await secondCallPromise;
    console.log('✅ Polling API call detected after 30s');

    // 7. Wait for UI to update after polling
    await page.waitForTimeout(1000);

    // 8. Verify timestamp changed (indicates UI refresh)
    const timestampAfter = await page.getByText(/Last updated:/).textContent();
    expect(timestampAfter).not.toBe(timestampBefore);

    // 9. Verify metrics still visible after update
    await expect(page.getByText('Total Users')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Recent Activity' })).toBeVisible();

    // 10. Capture post-update screenshot for visual regression
    await expect(page).toHaveScreenshot('polling-after-update.png');

    console.log('✅ Journey 6 complete: Real-time polling verified');
  });
});
