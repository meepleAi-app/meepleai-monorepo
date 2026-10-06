/**
 * Session History E2E Tests (Issue #3165 - GST-006)
 *
 * Tests for session history page and detail modal
 */

import { test, expect } from '@playwright/test';

test.describe('Toolkit - Session History', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/toolkit/history');
  });

  test('should display history page', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /session history/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /filters/i })).toBeVisible();
  });

  test('should show filters', async ({ page }) => {
    // Game filter
    await expect(page.getByLabel(/game/i)).toBeVisible();

    // Date range
    await expect(page.getByLabel(/start date/i)).toBeVisible();
    await expect(page.getByLabel(/end date/i)).toBeVisible();

    // Reset button
    await expect(page.getByRole('button', { name: /reset/i })).toBeVisible();
  });

  test('should reset filters', async ({ page }) => {
    // Set some filters
    const startDate = page.getByLabel(/start date/i);
    await startDate.fill('2026-01-01');

    const endDate = page.getByLabel(/end date/i);
    await endDate.fill('2026-01-31');

    // Reset
    await page.getByRole('button', { name: /reset/i }).click();

    // Verify cleared
    await expect(startDate).toHaveValue('');
    await expect(endDate).toHaveValue('');
  });

  test('should show empty state when no sessions', async ({ page }) => {
    await expect(page.getByText(/no sessions found/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /start your first session/i })).toBeVisible();
  });

  test('should navigate to toolkit from empty state', async ({ page }) => {
    await page.getByRole('button', { name: /start your first session/i }).click();
    await page.waitForURL('/toolkit');
  });

  test('should be mobile responsive', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/toolkit/history');

    await expect(page.getByRole('heading', { name: /session history/i })).toBeVisible();
  });

  test('should work in dark mode', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.goto('/toolkit/history');

    await expect(page.getByRole('heading', { name: /session history/i })).toBeVisible();
  });
});
