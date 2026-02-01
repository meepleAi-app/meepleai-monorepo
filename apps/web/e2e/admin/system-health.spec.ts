/**
 * ADM-17: System Health
 * Issue #3082 - P3 Low
 *
 * Tests system health monitoring:
 * - View service status
 * - Health check history
 * - Dependency status
 */

import { test, expect } from '../fixtures';
import { AdminPage } from '../pages';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupSystemHealthMocks(page: Page) {
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'admin', email: 'admin@meepleai.dev', displayName: 'Admin', role: 'Admin' },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/admin/health`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        status: 'healthy',
        services: [
          { name: 'API', status: 'healthy', latency: 45 },
          { name: 'Database', status: 'healthy', latency: 12 },
          { name: 'Redis', status: 'healthy', latency: 3 },
          { name: 'Qdrant', status: 'healthy', latency: 25 },
        ],
        uptime: '5d 12h 34m',
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/admin/**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) });
  });

  return {};
}

test.describe('ADM-17: System Health', () => {
  test('should display overall health status', async ({ page }) => {
    await setupSystemHealthMocks(page);
    await page.goto('/admin/system/health');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/healthy|status/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should show service status', async ({ page }) => {
    await setupSystemHealthMocks(page);
    await page.goto('/admin/system/health');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/api|database|redis|qdrant/i).first()).toBeVisible();
  });

  test('should show uptime', async ({ page }) => {
    await setupSystemHealthMocks(page);
    await page.goto('/admin/system/health');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/uptime|5d/i)).toBeVisible();
  });
});
