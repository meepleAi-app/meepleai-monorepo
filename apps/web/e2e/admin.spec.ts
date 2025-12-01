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
import { AuthHelper, AdminHelper, USER_FIXTURES } from './pages';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Admin dashboard', () => {
  test('renders analytics, supports filtering and exports CSV', async ({ page }) => {
    const authHelper = new AuthHelper(page);
    const adminHelper = new AdminHelper(page);

    // Authenticate as admin
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock admin requests with sample data
    const allRequests = [
      {
        id: 'req-1',
        userId: 'user-1',
        gameId: 'terraforming-mars',
        endpoint: 'qa',
        query: 'Strategie per Terraforming Mars?',
        responseSnippet: 'Concentra la produzione di ossigeno.',
        latencyMs: 320,
        tokenCount: 740,
        promptTokens: 400,
        completionTokens: 340,
        confidence: 0.92,
        status: 'Success',
        errorMessage: null,
        ipAddress: '127.0.0.1',
        userAgent: 'Playwright',
        createdAt: new Date('2025-01-06T10:15:00Z').toISOString(),
        model: 'gpt-4.1-mini',
        finishReason: 'stop',
      },
      {
        id: 'req-2',
        userId: 'user-2',
        gameId: 'meeple-arena',
        endpoint: 'setup',
        query: 'Meeple setup guidance',
        responseSnippet: 'Disponi tutti i meeple sul tabellone iniziale.',
        latencyMs: 480,
        tokenCount: 560,
        promptTokens: 260,
        completionTokens: 300,
        confidence: 0.76,
        status: 'Error',
        errorMessage: 'Timeout backend',
        ipAddress: '127.0.0.1',
        userAgent: 'Playwright',
        createdAt: new Date('2025-01-06T10:20:00Z').toISOString(),
        model: 'gpt-4.1-mini',
        finishReason: 'length',
      },
    ];

    const qaOnly = [allRequests[0]];

    // Mock requests endpoint with filtering support
    await page.route(new RegExp(`${apiBase}/api/v1/admin/requests.*`), async route => {
      const url = new URL(route.request().url());
      const endpoint = url.searchParams.get('endpoint');
      const body = endpoint === 'qa' ? { requests: qaOnly } : { requests: allRequests };

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(body),
      });
    });

    // Mock stats endpoint
    await adminHelper.mockAdminStats({
      totalRequests: 2,
      avgLatencyMs: 400,
      totalTokens: 1300,
      successRate: 0.5,
    });

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

    // Verify dashboard elements
    await expect(page.getByRole('heading', { name: 'Admin Dashboard' })).toBeVisible();
    const totalRequestsCard = page.locator('div').filter({ hasText: 'Total Requests' }).first();
    await expect(totalRequestsCard).toContainText('Total Requests');
    await expect(totalRequestsCard).toContainText('2');
    await expect(page.getByText('Success Rate')).toBeVisible();
    await expect(page.getByText('50.0%')).toBeVisible();

    // Verify request list
    await expect(page.getByText('Strategie per Terraforming Mars?')).toBeVisible();
    await expect(page.getByText('Meeple setup guidance')).toBeVisible();

    // Test filtering by text
    const filterInput = page.getByPlaceholder('Filter by query, endpoint, user ID, or game ID...');
    await filterInput.fill('Terraforming');

    await expect(page.locator('text=Meeple setup guidance')).toHaveCount(0);
    await expect(page.getByText('Strategie per Terraforming Mars?')).toBeVisible();

    // Clear filter
    await filterInput.fill('');

    // Test filtering by endpoint
    const endpointSelect = page.getByRole('combobox');
    await endpointSelect.selectOption('qa');

    await page.waitForResponse(response => {
      return (
        response.url().startsWith(`${apiBase}/api/v1/admin/requests`) &&
        response.url().includes('endpoint=qa') &&
        response.request().method() === 'GET'
      );
    });

    await expect(page.locator('text=Meeple setup guidance')).toHaveCount(0);
    await expect(page.getByText('Strategie per Terraforming Mars?')).toBeVisible();

    // Test CSV export
    await page.getByRole('button', { name: 'Export CSV' }).click();

    await page.waitForTimeout(500);

    // Verify blob was captured
    const downloadedBlobs = await page.evaluate(() => (window as any).__downloadBlobs);
    expect(downloadedBlobs.length).toBeGreaterThan(0);

    // Verify first blob is CSV
    const firstBlob = downloadedBlobs[0] as Blob;
    expect(firstBlob.type).toBe('text/csv');

    const csvText = await firstBlob.text();
    expect(csvText).toContain('id,userId,gameId');
    expect(csvText).toContain('req-1');
  });
});
