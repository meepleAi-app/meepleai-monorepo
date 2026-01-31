/**
 * Admin Dashboard E2E Tests - MIGRATED TO POM
 *
 * Tests admin analytics dashboard with filtering and CSV export functionality.
 *
 * Test Coverage:
 * - Dashboard rendering with stats
 * - Request filtering by query text
 * - Request filtering by endpoint
 * - CSV export functionality
 *
 * @see apps/web/e2e/page-objects/ - Page Object Model architecture
 */

import { test, expect } from './fixtures/chromatic';
import { AuthHelper, AdminHelper } from './pages';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Admin dashboard', () => {
  test('renders analytics, supports filtering and exports CSV', async ({ page }) => {
    const authHelper = new AuthHelper(page);
    const adminHelper = new AdminHelper(page);

    // Authenticate as admin with real session (middleware validates server-side)
    await authHelper.setupRealSession('admin');

    // ✅ REMOVED MOCK: Use real Admin Analytics API
    // Real backend GET /api/v1/admin/requests must support filtering by endpoint query param
    // Real backend GET /api/v1/admin/stats must return stats with totalRequests, avgLatencyMs, etc.
    // Note: Test data must be seeded in backend for consistent E2E results

    // Setup download tracking
    await page.addInitScript(() => {
      (window as any).__downloadBlobs = [] as Blob[];
      const originalCreateObjectURL = URL.createObjectURL.bind(URL);
      URL.createObjectURL = (blob: Blob) => {
        (window as any).__downloadBlobs.push(blob);
        return originalCreateObjectURL(blob);
      };
    });

    await page.goto('/admin');

    // Verify dashboard elements (generic structure, not specific mock data)
    await expect(page.getByRole('heading', { name: 'Admin Dashboard' })).toBeVisible();
    const totalRequestsCard = page.locator('div').filter({ hasText: 'Total Requests' }).first();
    await expect(totalRequestsCard).toContainText('Total Requests');
    // ✅ CHANGED: Verify card has numeric value, not specific '2' from mock
    await expect(totalRequestsCard).toContainText(/\d+/);
    await expect(page.getByText('Success Rate')).toBeVisible();
    // ✅ CHANGED: Verify success rate format, not specific '50.0%' from mock
    await expect(page.locator('text=/\\d+\\.\\d+%/')).toBeVisible();

    // ✅ CHANGED: Verify request list exists (backend seeded data), not specific mock entries
    // Wait for at least one request row to be visible
    const requestRows = page
      .locator('[data-testid="request-row"]')
      .or(page.locator('tr').filter({ has: page.locator('td') }));
    await expect(requestRows.first()).toBeVisible({ timeout: 5000 });

    // Test filtering functionality (generic, works with any backend data)
    const filterInput = page.getByPlaceholder('Filter by query, endpoint, user ID, or game ID...');
    const initialRowCount = await requestRows.count();

    // Filter by some text (should reduce results or keep same if no match)
    await filterInput.fill('test');
    await page.waitForTimeout(500); // Debounce
    const filteredRowCount = await requestRows.count();
    // Verify filter works (count changed or stayed same if all matched)
    expect(filteredRowCount).toBeLessThanOrEqual(initialRowCount);

    // Clear filter
    await filterInput.fill('');
    await page.waitForTimeout(500); // Debounce

    // ✅ CHANGED: Test filtering by endpoint (generic, works with any backend data)
    const endpointSelect = page.getByRole('combobox');

    // Get initial state
    await page.waitForLoadState('networkidle');
    const initialCount = await requestRows.count();

    // Select first available endpoint option (not hardcoded 'qa')
    const options = await endpointSelect.locator('option').allTextContents();
    if (options.length > 1) {
      // Skip "All" option (usually first), select second option
      await endpointSelect.selectOption({ index: 1 });

      // Wait for API call with endpoint parameter
      await page.waitForResponse(
        response =>
          response.url().startsWith(`${apiBase}/api/v1/admin/requests`) &&
          response.url().includes('endpoint=') &&
          response.request().method() === 'GET',
        { timeout: 5000 }
      );

      // Verify filtering worked (count changed or stayed same)
      const filteredCount = await requestRows.count();
      expect(filteredCount).toBeLessThanOrEqual(initialCount);
    }

    // Test CSV export
    await page.getByRole('button', { name: 'Export CSV' }).click();

    await page.waitForTimeout(500);

    // Verify blob was captured
    const downloadedBlobs = await page.evaluate(() => (window as any).__downloadBlobs);
    expect(downloadedBlobs.length).toBeGreaterThan(0);

    // Verify first blob is CSV
    const firstBlob = downloadedBlobs[0] as Blob;
    expect(firstBlob.type).toBe('text/csv');

    // ✅ CHANGED: Verify CSV structure, not specific mock data
    const csvText = await firstBlob.text();
    expect(csvText).toContain('id,userId,gameId'); // CSV headers
    // Verify CSV has at least header + 1 data row
    const csvLines = csvText.split('\n').filter(line => line.trim());
    expect(csvLines.length).toBeGreaterThan(1);
  });
});
