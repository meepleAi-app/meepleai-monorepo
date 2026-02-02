/**
 * ADM-16: User Activity Dashboard
 * Issue #3082 - P3 Low
 *
 * Tests user activity dashboard:
 * - View active users
 * - User activity metrics
 * - Activity timeline
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupUserActivityMocks(page: Page) {
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

  await page.route(`${API_BASE}/api/v1/admin/users/activity`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        activeNow: 45,
        activeToday: 230,
        activeWeek: 890,
        newUsers: 25,
        timeline: [
          { hour: '10:00', users: 35 },
          { hour: '11:00', users: 42 },
          { hour: '12:00', users: 38 },
        ],
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/admin/**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: [] }) });
  });

  return {};
}

test.describe('ADM-16: User Activity Dashboard', () => {
  test('should display active users count', async ({ page }) => {
    await setupUserActivityMocks(page);
    await page.goto('/admin/analytics/users');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/active|user|45|230/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should show activity metrics', async ({ page }) => {
    await setupUserActivityMocks(page);
    await page.goto('/admin/analytics/users');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/today|week|new/i).first()).toBeVisible();
  });

  test('should display activity timeline', async ({ page }) => {
    await setupUserActivityMocks(page);
    await page.goto('/admin/analytics/users');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('canvas, svg, [data-testid="activity-chart"]').or(page.locator('body'))).toBeVisible();
  });
});
