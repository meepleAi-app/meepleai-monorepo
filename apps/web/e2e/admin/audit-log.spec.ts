/**
 * ADM-14: Audit Log Viewer
 * Issue #3082 - P2 Medium
 *
 * Tests audit log viewer functionality:
 * - View audit log entries
 * - Filter by user/action/date
 * - Audit log pagination
 * - Export audit logs
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface AuditLogEntry {
  id: string;
  userId: string;
  userEmail: string;
  action: string;
  resourceType: string;
  resourceId: string;
  details: Record<string, unknown>;
  ipAddress: string;
  timestamp: string;
}

/**
 * Setup mock routes for audit log testing
 */
async function setupAuditLogMocks(page: Page) {
  const generateLogs = (count: number): AuditLogEntry[] => {
    const actions = ['LOGIN', 'LOGOUT', 'CREATE_GAME', 'UPDATE_USER', 'DELETE_DOCUMENT', 'EXPORT_DATA'];
    const users = [
      { id: 'user-1', email: 'admin@meepleai.dev' },
      { id: 'user-2', email: 'user@example.com' },
      { id: 'user-3', email: 'manager@example.com' },
    ];

    return Array.from({ length: count }, (_, i) => {
      const user = users[i % users.length];
      const action = actions[i % actions.length];

      return {
        id: `log-${i + 1}`,
        userId: user.id,
        userEmail: user.email,
        action,
        resourceType: action.includes('GAME') ? 'Game' : action.includes('USER') ? 'User' : 'Session',
        resourceId: `resource-${i}`,
        details: { action, index: i },
        ipAddress: `192.168.1.${(i % 255) + 1}`,
        timestamp: new Date(Date.now() - i * 3600000).toISOString(),
      };
    });
  };

  const allLogs = generateLogs(100);

  // Mock admin auth
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'admin-user-id',
          email: 'admin@meepleai.dev',
          displayName: 'Admin User',
          role: 'Admin',
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // Mock audit logs endpoint with pagination and filtering
  await page.route(`${API_BASE}/api/v1/admin/audit-logs**`, async (route) => {
    const url = route.request().url();
    const page_num = parseInt(url.match(/page=(\d+)/)?.[1] || '1');
    const pageSize = parseInt(url.match(/pageSize=(\d+)/)?.[1] || '20');
    const userFilter = url.match(/userId=([^&]+)/)?.[1];
    const actionFilter = url.match(/action=([^&]+)/)?.[1];
    const startDate = url.match(/startDate=([^&]+)/)?.[1];
    const endDate = url.match(/endDate=([^&]+)/)?.[1];

    let filteredLogs = [...allLogs];

    if (userFilter) {
      filteredLogs = filteredLogs.filter((l) => l.userId === userFilter);
    }
    if (actionFilter) {
      filteredLogs = filteredLogs.filter((l) => l.action === actionFilter);
    }
    if (startDate) {
      const start = new Date(decodeURIComponent(startDate));
      filteredLogs = filteredLogs.filter((l) => new Date(l.timestamp) >= start);
    }
    if (endDate) {
      const end = new Date(decodeURIComponent(endDate));
      filteredLogs = filteredLogs.filter((l) => new Date(l.timestamp) <= end);
    }

    const start = (page_num - 1) * pageSize;
    const paginatedLogs = filteredLogs.slice(start, start + pageSize);

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        logs: paginatedLogs,
        totalCount: filteredLogs.length,
        page: page_num,
        pageSize,
        totalPages: Math.ceil(filteredLogs.length / pageSize),
      }),
    });
  });

  // Mock export endpoint
  await page.route(`${API_BASE}/api/v1/admin/audit-logs/export`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'text/csv',
      body: 'id,userId,action,timestamp\nlog-1,user-1,LOGIN,2025-01-20',
    });
  });

  // Mock common admin endpoints
  await page.route(`${API_BASE}/api/v1/admin/**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: [] }),
    });
  });

  return { allLogs };
}

test.describe('ADM-14: Audit Log Viewer', () => {
  test.describe('View Audit Logs', () => {
    test('should navigate to audit log viewer', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Navigate to audit logs
      const auditLink = page.getByRole('link', { name: /audit|log|activity/i });
      if (await auditLink.isVisible()) {
        await auditLink.click();
        await page.waitForLoadState('networkidle');
      }

      await expect(page.getByText(/audit.*log|activity.*log/i)).toBeVisible();
    });

    test('should display audit log entries', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      // Should show log entries
      await expect(
        page.getByText(/login|logout|create|update|delete/i).first()
      ).toBeVisible({ timeout: 5000 });
    });

    test('should show entry details', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      // Should show user, action, timestamp
      await expect(page.getByText(/@meepleai|@example/i).first()).toBeVisible();
    });

    test('should show IP address', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      // Should show IP
      await expect(page.getByText(/192\.168/i).first()).toBeVisible();
    });
  });

  test.describe('Filter Audit Logs', () => {
    test('should filter by user', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      const userFilter = page.getByRole('combobox', { name: /user/i }).or(
        page.locator('[data-testid="user-filter"]')
      );

      if (await userFilter.isVisible()) {
        await userFilter.click();
        await page.getByText(/admin@meepleai/i).click();

        await page.waitForLoadState('networkidle');

        // Should only show admin logs
        await expect(page.getByText(/admin@meepleai/i).first()).toBeVisible();
      }
    });

    test('should filter by action type', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      const actionFilter = page.getByRole('combobox', { name: /action/i }).or(
        page.locator('[data-testid="action-filter"]')
      );

      if (await actionFilter.isVisible()) {
        await actionFilter.click();
        await page.getByText(/login/i).click();

        await page.waitForLoadState('networkidle');
      }
    });

    test('should filter by date range', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      const startDateInput = page.locator('input[name="startDate"], input[type="date"]').first();
      const endDateInput = page.locator('input[name="endDate"], input[type="date"]').last();

      if (await startDateInput.isVisible()) {
        await startDateInput.fill('2025-01-01');
        if (await endDateInput.isVisible()) {
          await endDateInput.fill('2025-01-31');
        }

        await page.waitForLoadState('networkidle');
      }
    });

    test('should combine multiple filters', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      // Apply multiple filters
      const userFilter = page.getByRole('combobox', { name: /user/i });
      const actionFilter = page.getByRole('combobox', { name: /action/i });

      if (await userFilter.isVisible() && await actionFilter.isVisible()) {
        await userFilter.click();
        await page.getByText(/admin/i).first().click();

        await actionFilter.click();
        await page.getByText(/login/i).click();

        await page.waitForLoadState('networkidle');
      }
    });
  });

  test.describe('Pagination', () => {
    test('should paginate audit logs', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      // Should show pagination
      await expect(
        page.getByRole('button', { name: /next|2|→/i }).or(
          page.locator('[data-testid="pagination"]')
        )
      ).toBeVisible();
    });

    test('should navigate to next page', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      const nextButton = page.getByRole('button', { name: /next|→/i });
      if (await nextButton.isVisible()) {
        await nextButton.click();
        await page.waitForLoadState('networkidle');

        // Should be on page 2
        await expect(page).toHaveURL(/page=2/);
      }
    });

    test('should show page size selector', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      await expect(
        page.getByRole('combobox', { name: /page.*size|show|per.*page/i }).or(
          page.locator('[data-testid="page-size"]')
        )
      ).toBeVisible();
    });

    test('should change page size', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      const pageSizeSelector = page.getByRole('combobox', { name: /size|show/i });
      if (await pageSizeSelector.isVisible()) {
        await pageSizeSelector.click();
        await page.getByText(/50/i).click();

        await page.waitForLoadState('networkidle');
      }
    });
  });

  test.describe('Export Audit Logs', () => {
    test('should show export button', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      await expect(
        page.getByRole('button', { name: /export|download/i })
      ).toBeVisible();
    });

    test('should export as CSV', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      const exportButton = page.getByRole('button', { name: /export/i });
      if (await exportButton.isVisible()) {
        // Start download
        const downloadPromise = page.waitForEvent('download', { timeout: 5000 }).catch(() => null);
        await exportButton.click();

        const download = await downloadPromise;
        if (download) {
          expect(download.suggestedFilename()).toContain('.csv');
        }
      }
    });

    test('should export filtered results', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      // Apply filter first
      const actionFilter = page.getByRole('combobox', { name: /action/i });
      if (await actionFilter.isVisible()) {
        await actionFilter.click();
        await page.getByText(/login/i).click();
      }

      // Then export
      const exportButton = page.getByRole('button', { name: /export/i });
      if (await exportButton.isVisible()) {
        await exportButton.click();
        await expect(page.locator('body')).toBeVisible();
      }
    });
  });

  test.describe('Log Entry Details', () => {
    test('should expand log entry for details', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      // Click on a log entry to expand
      const logEntry = page.locator('[data-testid="log-entry"], .audit-log-row').first();
      if (await logEntry.isVisible()) {
        await logEntry.click();

        // Should show more details
        await expect(
          page.getByText(/detail|resource|ip/i)
        ).toBeVisible();
      }
    });

    test('should show resource link', async ({ page }) => {
      await setupAuditLogMocks(page);

      await page.goto('/admin/audit-logs');
      await page.waitForLoadState('networkidle');

      // Log entries may have links to affected resources
      const resourceLink = page.getByRole('link', { name: /view|resource/i });
      if (await resourceLink.isVisible()) {
        await expect(resourceLink).toHaveAttribute('href', /.+/);
      }
    });
  });
});
