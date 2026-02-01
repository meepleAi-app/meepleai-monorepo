/**
 * Admin Analytics Dashboard E2E - MIGRATED TO POM
 *
 * Tests analytics dashboard metrics display and export functionality.
 *
 * @see apps/web/e2e/pages/ - Page Object Model architecture
 */

import { test as base, expect, Page } from './fixtures';
import { getTextMatcher } from './fixtures/i18n';
import { AdminHelper } from './pages';

const test = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);

    // Setup admin auth (skip navigation)
    await adminHelper.setupAdminAuth(true);

    // ✅ REMOVED MOCK: Use real Admin Analytics API
    // Real backend GET /api/v1/admin/analytics must return metrics + trend data
    // Real backend POST /api/v1/admin/analytics/export must support CSV/JSON export
    // Note: Test verifies UI structure only, no specific values checked

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

    // Wait for metrics grid using robust helper
    await waitForMetricsGrid(page);

    // Check chart headings are visible using semantic role selectors
    await expect(page.getByRole('heading', { name: /registrations|registrazioni/i, level: 3 })).toBeVisible();
    await expect(page.getByRole('heading', { name: /sessions|sessioni/i, level: 3 })).toBeVisible();
    await expect(page.getByRole('heading', { name: /api requests|richieste api/i, level: 2 })).toBeVisible();
    await expect(page.getByRole('heading', { name: /pdf|uploads/i, level: 3 })).toBeVisible();
    await expect(page.getByRole('heading', { name: /chat|messages|messaggi/i, level: 2 })).toBeVisible();
  });

  test('should allow changing time period filter', async ({ adminPage: page }) => {
    await page.goto('/admin/analytics');
    await page.waitForLoadState('networkidle');

    // Wait for metrics grid
    await waitForMetricsGrid(page);

    // Change to 7 days using semantic selector
    await page.getByRole('combobox', { name: /time period|periodo/i }).selectOption('7');

    // Verify metrics grid reloads
    await waitForMetricsGrid(page);
  });

  test('should toggle auto-refresh', async ({ adminPage: page }) => {
    await page.goto('/admin/analytics');
    await page.waitForLoadState('networkidle');

    // Wait for metrics grid
    await waitForMetricsGrid(page);

    // Auto-refresh button uses semantic role (more stable than text)
    const refreshToggle = page.getByRole('button', { name: /refresh|aggiorna/i });
    await expect(refreshToggle).toBeVisible();

    // Toggle off
    await refreshToggle.click({ force: true });
    await page.waitForTimeout(500);

    // Toggle back on
    await refreshToggle.click({ force: true });
    await expect(refreshToggle).toBeVisible();
  });

  test('should refresh data when refresh button clicked', async ({ adminPage: page }) => {
    await page.goto('/admin/analytics');
    await page.waitForLoadState('networkidle');

    // Wait for metrics grid
    await waitForMetricsGrid(page);

    // Get initial last updated time using semantic text (no strict matcher)
    const initialUpdateText = await page
      .getByText(/last updated|ultimo aggiornamento/i)
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
      .getByText(/last updated|ultimo aggiornamento/i)
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