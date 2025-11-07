import { test as base, expect } from '@playwright/test';
import { loginAsAdmin } from './fixtures/auth';
import { getTextMatcher, t } from './fixtures/i18n';

const test = base.extend<{ adminPage: any }>({
  adminPage: async ({ page }, use) => {
    // Set up auth mocks first (but skip navigation)
    await loginAsAdmin(page, true);

    // Set up analytics API mocks BEFORE any navigation
    await page.route('**/api/v1/admin/analytics*', async (route) => {
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
            totalTokensUsed: 5000
          },
          userTrend: [{ date: '2025-11-05', count: 2 }],
          sessionTrend: [{ date: '2025-11-05', count: 5 }],
          apiRequestTrend: [{ date: '2025-11-05', count: 100 }],
          pdfUploadTrend: [{ date: '2025-11-05', count: 3 }],
          chatMessageTrend: [{ date: '2025-11-05', count: 15 }],
          generatedAt: new Date().toISOString()
        })
      });
    });

    await use(page);
  }
});

test.describe("Analytics Dashboard E2E", () => {

  test("should display analytics dashboard with metrics", async ({ adminPage: page }) => {
    // Navigate to analytics
    await page.goto("/admin/analytics");
    await page.waitForLoadState('networkidle');

    // Wait for page to load (increased timeout for API calls)
    await expect(page.getByText(getTextMatcher("admin.analytics.dashboard"))).toBeVisible({ timeout: 15000 });

    // Check metric cards are visible
    await expect(page.getByText(getTextMatcher("admin.analytics.totalUsers"))).toBeVisible();
    await expect(page.getByText(getTextMatcher("admin.analytics.activeSessions"))).toBeVisible();
    await expect(page.getByText(getTextMatcher("admin.analytics.apiRequestsToday"))).toBeVisible();
    await expect(page.getByText(getTextMatcher("admin.analytics.totalPdfDocuments"))).toBeVisible();
    await expect(page.getByText(getTextMatcher("admin.analytics.totalChatMessages"))).toBeVisible();
    await expect(page.getByText(getTextMatcher("admin.analytics.avgConfidenceScore"))).toBeVisible();
    await expect(page.getByText(getTextMatcher("admin.analytics.totalRagRequests"))).toBeVisible();
    await expect(page.getByText(getTextMatcher("admin.analytics.totalTokensUsed"))).toBeVisible();
  });

  test("should display charts", async ({ adminPage: page }) => {
    await page.goto("/admin/analytics");
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');

    // Wait for dashboard to load (increased timeout)
    await expect(page.getByText(getTextMatcher("admin.analytics.dashboard"))).toBeVisible({ timeout: 15000 });

    // Check chart titles are visible using heading role to avoid strict mode violation
    await expect(page.getByText(getTextMatcher("admin.analytics.userRegistrations"))).toBeVisible();
    await expect(page.getByText(getTextMatcher("admin.analytics.sessionCreations"))).toBeVisible();
    await expect(page.locator('h2', { hasText: getTextMatcher("admin.analytics.apiRequests") })).toBeVisible();
    await expect(page.getByText(getTextMatcher("admin.analytics.pdfUploads"))).toBeVisible();
    await expect(page.locator('h2', { hasText: getTextMatcher("admin.analytics.chatMessages") })).toBeVisible();
  });

  test("should allow changing time period filter", async ({ adminPage: page }) => {
    await page.goto("/admin/analytics");
    await page.waitForLoadState('networkidle');

    // Wait for initial load
    await expect(page.getByText(getTextMatcher("admin.analytics.dashboard"))).toBeVisible();

    // Change to 7 days
    await page.selectOption('select', '7');

    // Wait for data to refresh (check that page doesn't error)
    await page.waitForTimeout(1000);
    await expect(page.getByText(getTextMatcher("admin.analytics.dashboard"))).toBeVisible();
  });

  test("should toggle auto-refresh", async ({ adminPage: page }) => {
    await page.goto("/admin/analytics");
    await page.waitForLoadState('networkidle');

    // Wait for dashboard
    await expect(page.getByText(getTextMatcher("admin.analytics.dashboard"))).toBeVisible();

    // Auto-refresh should be ON by default
    await expect(page.getByRole("button", { name: getTextMatcher("admin.analytics.autoRefreshOn") })).toBeVisible();

    // Toggle off (use force: true to handle nextjs-portal overlay)
    await page.getByRole("button", { name: getTextMatcher("admin.analytics.autoRefreshOn") }).click({ force: true });
    await expect(page.getByRole("button", { name: getTextMatcher("admin.analytics.autoRefreshOff") })).toBeVisible();

    // Toggle back on (use force: true to handle nextjs-portal overlay)
    await page.getByRole("button", { name: getTextMatcher("admin.analytics.autoRefreshOff") }).click({ force: true });
    await expect(page.getByRole("button", { name: getTextMatcher("admin.analytics.autoRefreshOn") })).toBeVisible();
  });

  test("should refresh data when refresh button clicked", async ({ adminPage: page }) => {
    await page.goto("/admin/analytics");
    await page.waitForLoadState('networkidle');

    // Wait for initial load
    await expect(page.getByText(getTextMatcher("admin.analytics.dashboard"))).toBeVisible();

    // Get initial last updated time
    const initialUpdateText = await page.getByText(getTextMatcher("admin.analytics.lastUpdated")).textContent();

    // Wait a moment
    await page.waitForTimeout(1000);

    // Click refresh button (use force: true to handle nextjs-portal overlay and strict mode)
    await page.locator('button').filter({ hasText: getTextMatcher("admin.analytics.refresh") }).first().click({ force: true });

    // Wait for refresh to complete
    await page.waitForTimeout(1000);

    // Check that last updated time has changed
    const newUpdateText = await page.getByText(getTextMatcher("admin.analytics.lastUpdated")).textContent();
    expect(newUpdateText).not.toBe(initialUpdateText);
  });

  test("should export CSV", async ({ adminPage: page }) => {
    await page.goto("/admin/analytics");
    await page.waitForLoadState('networkidle');

    // Wait for dashboard
    await expect(page.getByText(getTextMatcher("admin.analytics.dashboard"))).toBeVisible();

    // Click export CSV button THEN wait for download (use force: true to handle nextjs-portal overlay)
    await page.getByRole("button", { name: getTextMatcher("admin.analytics.exportCsv") }).click({ force: true });
    const download = await page.waitForEvent("download");
    
    expect(download.suggestedFilename()).toMatch(/analytics-.+\.csv/);

    // Check for success toast
    await expect(page.getByText(getTextMatcher("admin.analytics.exportedCsv"))).toBeVisible();
  });

  test("should export JSON", async ({ adminPage: page }) => {
    await page.goto("/admin/analytics");
    await page.waitForLoadState('networkidle');

    // Wait for dashboard
    await expect(page.getByText(getTextMatcher("admin.analytics.dashboard"))).toBeVisible();

    // Click export JSON button THEN wait for download (use force: true to handle nextjs-portal overlay)
    await page.getByRole("button", { name: getTextMatcher("admin.analytics.exportJson") }).click({ force: true });
    const download = await page.waitForEvent("download");
    
    expect(download.suggestedFilename()).toMatch(/analytics-.+\.json/);

    // Check for success toast
    await expect(page.getByText(getTextMatcher("admin.analytics.exportedJson"))).toBeVisible();
  });

  test("should have back to users link", async ({ adminPage: page }) => {
    await page.goto("/admin/analytics");
    await page.waitForLoadState('networkidle');

    // Check back link exists
    const backLink = page.getByRole("link", { name: getTextMatcher("admin.analytics.backToUsers") });
    await expect(backLink).toBeVisible();

    // Click and verify navigation (use force: true to handle nextjs-portal overlay)
    await backLink.click({ force: true });
    await expect(page).toHaveURL("http://localhost:3000/admin/users");
  });
});