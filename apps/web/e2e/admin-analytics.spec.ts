/**
 * Admin Analytics Dashboard E2E - MIGRATED TO POM
 *
 * Tests analytics dashboard metrics display and export functionality.
 *
 * @see apps/web/e2e/pages/ - Page Object Model architecture
 */

import { test as base, expect, Page } from './fixtures/chromatic';
import { getTextMatcher } from './fixtures/i18n';
import { AdminHelper } from './pages';

const test = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);

    // Setup admin auth (skip navigation)
    await adminHelper.setupAdminAuth(true);

    // Set up analytics API mocks BEFORE any navigation
    await page.route('**/api/v1/admin/analytics*', async route => {
      const url = route.request().url();

      // Handle export endpoint
      if (url.includes('/export')) {
        const format = JSON.parse(route.request().postData() || '{}').format || 'csv';
        const content =
          format === 'csv'
            ? 'Date,Users,Sessions\n2025-11-05,10,5'
            : JSON.stringify({
                metrics: { totalUsers: 10 },
                generatedAt: new Date().toISOString(),
              });

        await route.fulfill({
          status: 200,
          contentType: format === 'csv' ? 'text/csv' : 'application/json',
          headers: {
            'Content-Disposition': `attachment; filename="analytics-${new Date().toISOString().split('T')[0]}.${format}"`,
          },
          body: content,
        });
      } else {
        // Handle regular analytics endpoint
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            metrics: {
              totalUsers: 10,
              activeSessions: 5,
              apiRequestsToday: 100,
              totalPdfDocuments: 20,
              totalChatMessages: 50,
              averageConfidenceScore: 0.85,
              totalRagRequests: 30,
              totalTokensUsed: 5000,
            },
            userTrend: [{ date: '2025-11-05', count: 2 }],
            sessionTrend: [{ date: '2025-11-05', count: 5 }],
            apiRequestTrend: [{ date: '2025-11-05', count: 100 }],
            pdfUploadTrend: [{ date: '2025-11-05', count: 3 }],
            chatMessageTrend: [{ date: '2025-11-05', count: 15 }],
            generatedAt: new Date().toISOString(),
          }),
        });
      }
    });

    await use(page);
  },
});

test.describe('Analytics Dashboard E2E', () => {
  test('should display analytics dashboard with metrics', async ({ adminPage: page }) => {
    await page.goto('/admin/analytics');
    await page.waitForLoadState('networkidle');

    // Wait for page to load
    await expect(page.getByText(getTextMatcher('admin.analytics.dashboard'))).toBeVisible({
      timeout: 15000,
    });

    // Check metric cards are visible
    await expect(page.getByText(getTextMatcher('admin.analytics.totalUsers'))).toBeVisible();
    await expect(page.getByText(getTextMatcher('admin.analytics.activeSessions'))).toBeVisible();
    await expect(page.getByText(getTextMatcher('admin.analytics.apiRequestsToday'))).toBeVisible();
    await expect(page.getByText(getTextMatcher('admin.analytics.totalPdfDocuments'))).toBeVisible();
    await expect(page.getByText(getTextMatcher('admin.analytics.totalChatMessages'))).toBeVisible();
    await expect(
      page.getByText(getTextMatcher('admin.analytics.avgConfidenceScore'))
    ).toBeVisible();
    await expect(page.getByText(getTextMatcher('admin.analytics.totalRagRequests'))).toBeVisible();
    await expect(page.getByText(getTextMatcher('admin.analytics.totalTokensUsed'))).toBeVisible();
  });

  test('should display charts', async ({ adminPage: page }) => {
    await page.goto('/admin/analytics');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');

    // Wait for dashboard to load
    await expect(page.getByText(getTextMatcher('admin.analytics.dashboard'))).toBeVisible({
      timeout: 15000,
    });

    // Check chart titles are visible
    await expect(page.getByText(getTextMatcher('admin.analytics.userRegistrations'))).toBeVisible();
    await expect(page.getByText(getTextMatcher('admin.analytics.sessionCreations'))).toBeVisible();
    await expect(
      page.locator('h2', { hasText: getTextMatcher('admin.analytics.apiRequests') })
    ).toBeVisible();
    await expect(page.getByText(getTextMatcher('admin.analytics.pdfUploads'))).toBeVisible();
    await expect(
      page.locator('h2', { hasText: getTextMatcher('admin.analytics.chatMessages') })
    ).toBeVisible();
  });

  test('should allow changing time period filter', async ({ adminPage: page }) => {
    await page.goto('/admin/analytics');
    await page.waitForLoadState('networkidle');

    // Wait for initial load
    await expect(page.getByText(getTextMatcher('admin.analytics.dashboard'))).toBeVisible();

    // Change to 7 days
    await page.selectOption('select', '7');

    // Wait for data to refresh
    await expect(page.getByText(getTextMatcher('admin.analytics.dashboard'))).toBeVisible();
  });

  test('should toggle auto-refresh', async ({ adminPage: page }) => {
    await page.goto('/admin/analytics');
    await page.waitForLoadState('networkidle');

    // Wait for dashboard
    await expect(page.getByText(getTextMatcher('admin.analytics.dashboard'))).toBeVisible();

    // Auto-refresh should be ON by default
    await expect(
      page.getByRole('button', { name: getTextMatcher('admin.analytics.autoRefreshOn') })
    ).toBeVisible();

    // Toggle off
    await page
      .getByRole('button', { name: getTextMatcher('admin.analytics.autoRefreshOn') })
      .click({ force: true });
    await expect(
      page.getByRole('button', { name: getTextMatcher('admin.analytics.autoRefreshOff') })
    ).toBeVisible();

    // Toggle back on
    await page
      .getByRole('button', { name: getTextMatcher('admin.analytics.autoRefreshOff') })
      .click({ force: true });
    await expect(
      page.getByRole('button', { name: getTextMatcher('admin.analytics.autoRefreshOn') })
    ).toBeVisible();
  });

  test('should refresh data when refresh button clicked', async ({ adminPage: page }) => {
    await page.goto('/admin/analytics');
    await page.waitForLoadState('networkidle');

    // Wait for initial load
    await expect(page.getByText(getTextMatcher('admin.analytics.dashboard'))).toBeVisible();

    // Get initial last updated time
    const initialUpdateText = await page
      .getByText(getTextMatcher('admin.analytics.lastUpdated'))
      .textContent();

    await page.waitForTimeout(1000);

    // Click refresh button
    await page
      .locator('button')
      .filter({ hasText: getTextMatcher('admin.analytics.refresh') })
      .first()
      .click({ force: true });

    await page.waitForTimeout(1000);

    // Check that last updated time has changed
    const newUpdateText = await page
      .getByText(getTextMatcher('admin.analytics.lastUpdated'))
      .textContent();
    expect(newUpdateText).not.toBe(initialUpdateText);
  });

  test('should export CSV', async ({ adminPage: page }) => {
    await page.goto('/admin/analytics');
    await page.waitForLoadState('networkidle');

    // Wait for dashboard
    await expect(page.getByText(getTextMatcher('admin.analytics.dashboard'))).toBeVisible();

    // Click export CSV button THEN wait for download
    await page
      .getByRole('button', { name: getTextMatcher('admin.analytics.exportCsv') })
      .click({ force: true });
    const download = await page.waitForEvent('download');

    expect(download.suggestedFilename()).toMatch(/analytics-.+\.csv/);

    // Check for success toast
    await expect(page.getByText(getTextMatcher('admin.analytics.exportedCsv'))).toBeVisible();
  });

  test('should export JSON', async ({ adminPage: page }) => {
    await page.goto('/admin/analytics');
    await page.waitForLoadState('networkidle');

    // Wait for dashboard
    await expect(page.getByText(getTextMatcher('admin.analytics.dashboard'))).toBeVisible();

    // Click export JSON button THEN wait for download
    await page
      .getByRole('button', { name: getTextMatcher('admin.analytics.exportJson') })
      .click({ force: true });
    const download = await page.waitForEvent('download');

    expect(download.suggestedFilename()).toMatch(/analytics-.+\.json/);

    // Check for success toast
    await expect(page.getByText(getTextMatcher('admin.analytics.exportedJson'))).toBeVisible();
  });

  test('should have back to users link', async ({ adminPage: page }) => {
    await page.goto('/admin/analytics');
    await page.waitForLoadState('networkidle');

    // Check back link exists
    const backLink = page.getByRole('link', {
      name: getTextMatcher('admin.analytics.backToUsers'),
    });
    await expect(backLink).toBeVisible();

    // Click and verify navigation
    await backLink.click({ force: true });
    await expect(page).toHaveURL('http://localhost:3000/admin/users');
  });
});
