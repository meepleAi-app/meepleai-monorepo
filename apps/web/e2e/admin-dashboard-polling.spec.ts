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

    let requestCount = 0;
    let currentMetrics = {
      totalUsers: 1247,
      activeSessions: 42,
      apiRequestsToday: 3456,
      totalPdfDocuments: 847,
      totalChatMessages: 15234,
      averageConfidenceScore: 0.942,
      totalRagRequests: 18547,
      totalTokensUsed: 15700000,
      totalGames: 125,
      apiRequests7d: 24891,
      apiRequests30d: 112034,
      averageLatency24h: 215.0,
      averageLatency7d: 228.0,
      errorRate24h: 0.025,
      activeAlerts: 2,
      resolvedAlerts: 37,
    };

    // Mock analytics API with dynamic data that changes on each request
    await page.route('**/api/v1/admin/analytics*', async route => {
      requestCount++;

      // Update metrics on second request (simulating real data changes)
      if (requestCount >= 2) {
        currentMetrics = {
          ...currentMetrics,
          totalUsers: currentMetrics.totalUsers + 10, // +10 users
          activeSessions: currentMetrics.activeSessions + 3, // +3 sessions
          apiRequestsToday: currentMetrics.apiRequestsToday + 150, // +150 requests
          totalChatMessages: currentMetrics.totalChatMessages + 25, // +25 messages
        };
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          metrics: currentMetrics,
          userTrend: [],
          sessionTrend: [],
          apiRequestTrend: [],
          pdfUploadTrend: [],
          chatMessageTrend: [],
          generatedAt: new Date().toISOString(),
        }),
      });
    });

    // Mock activity feed API
    await page.route('**/api/v1/admin/activity*', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          events: [
            {
              id: '1',
              eventType: 'UserRegistered',
              description: 'New user registered: john.doe@example.com',
              userId: 'user-123',
              userEmail: 'john.doe@example.com',
              timestamp: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
              severity: 'Info',
            },
            {
              id: '2',
              eventType: 'PdfUploaded',
              description: 'PDF uploaded: Catan-Rules.pdf (2456789 bytes)',
              userId: 'user-456',
              userEmail: 'alice@example.com',
              timestamp: new Date(Date.now() - 10 * 60 * 1000).toISOString(),
              severity: 'Info',
            },
            {
              id: '3',
              eventType: 'AlertCreated',
              description: 'Alert: High error rate detected (Severity: warning)',
              timestamp: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
              severity: 'Warning',
            },
          ],
          totalCount: 3,
          generatedAt: new Date().toISOString(),
        }),
      });
    });

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

    // Step 3: Verify 12 core metrics (Issue #888 specifies 12, not 16)
    // Core business metrics
    await expect(page.getByText('Total Users')).toBeVisible();
    await expect(page.getByText('1,247')).toBeVisible();

    await expect(page.getByText('Active Sessions')).toBeVisible();
    await expect(page.getByText('42')).toBeVisible();

    await expect(page.getByText('Total Games')).toBeVisible();
    await expect(page.getByText('125')).toBeVisible();

    await expect(page.getByText('API Requests (24h)')).toBeVisible();
    await expect(page.getByText('3,456')).toBeVisible();

    await expect(page.getByText('Total PDFs')).toBeVisible();
    await expect(page.getByText('847')).toBeVisible();

    await expect(page.getByText('Total Chat Messages')).toBeVisible();
    await expect(page.getByText('15,234')).toBeVisible();

    await expect(page.getByText('Avg Confidence')).toBeVisible();
    await expect(page.getByText('94.2%')).toBeVisible();

    await expect(page.getByText('Total RAG Requests')).toBeVisible();
    await expect(page.getByText('18,547')).toBeVisible();

    await expect(page.getByText('Avg Latency (24h)')).toBeVisible();
    await expect(page.getByText('215ms')).toBeVisible();

    await expect(page.getByText('Error Rate (24h)')).toBeVisible();
    await expect(page.getByText('2.5%')).toBeVisible();

    await expect(page.getByText('Active Alerts')).toBeVisible();
    await expect(page.getByText('2')).toBeVisible();

    await expect(page.getByText('Total Tokens')).toBeVisible();
    await expect(page.getByText('15.70M')).toBeVisible();
  });

  test('should display activity feed', async ({ adminPage: page }) => {
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Step 4: Verify activity feed
    await expect(page.getByRole('heading', { name: 'Recent Activity' })).toBeVisible();
    await expect(page.getByText(/New user registered.*john.doe@example.com/)).toBeVisible();
    await expect(page.getByText(/PDF uploaded.*Catan-Rules.pdf/)).toBeVisible();
    await expect(page.getByText(/Alert.*High error rate/)).toBeVisible();
  });

  test('should auto-refresh metrics after 30 seconds', async ({ adminPage: page }) => {
    // Navigate to dashboard
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');

    // Verify initial metrics
    await expect(page.getByText('Total Users')).toBeVisible();
    await expect(page.getByText('1,247')).toBeVisible();
    await expect(page.getByText('Active Sessions')).toBeVisible();
    await expect(page.getByText('42')).toBeVisible();

    // Step 5: Wait for 30s auto-refresh
    // React Query polling interval is 30s (Issue #886)
    await page.waitForTimeout(31000); // 31s to ensure refresh happens

    // Step 6: Verify metrics updated
    // After refresh, totalUsers should be 1257 (+10), activeSessions 45 (+3)
    await expect(page.getByText('1,257')).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('45')).toBeVisible();
    await expect(page.getByText('3,606')).toBeVisible(); // apiRequestsToday +150
    await expect(page.getByText('15,259')).toBeVisible(); // totalChatMessages +25
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
});
