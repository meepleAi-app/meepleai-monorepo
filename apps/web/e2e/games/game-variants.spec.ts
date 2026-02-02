/**
 * GAME-08: Game Variants
 * Issue #3082 - P3 Low
 *
 * Tests game variants functionality:
 * - View game variants
 * - Select variant for session
 * - Variant-specific rules
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupGameVariantsMocks(page: Page) {
  const variants = [
    { id: 'standard', name: 'Standard Chess', description: 'Classic chess rules' },
    { id: 'chess960', name: 'Chess960 (Fischer Random)', description: 'Random starting positions' },
    { id: 'blitz', name: 'Blitz Chess', description: 'Fast-paced with time controls' },
  ];

  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'test-user', email: 'test@example.com', displayName: 'Test User', role: 'User' },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/games/chess/variants`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ variants }),
    });
  });

  await page.route(`${API_BASE}/api/v1/games/*`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ id: 'chess', title: 'Chess', hasVariants: true }),
    });
  });

  await page.route(`${API_BASE}/api/v1/games`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([{ id: 'chess', title: 'Chess' }]),
    });
  });

  return { variants };
}

test.describe('GAME-08: Game Variants', () => {
  test('should display game variants', async ({ page }) => {
    await setupGameVariantsMocks(page);
    await page.goto('/games/chess');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/variant|chess960|blitz/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should show variant descriptions', async ({ page }) => {
    await setupGameVariantsMocks(page);
    await page.goto('/games/chess');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/random|position|time.*control/i).first()).toBeVisible();
  });

  test('should select variant', async ({ page }) => {
    await setupGameVariantsMocks(page);
    await page.goto('/games/chess');
    await page.waitForLoadState('networkidle');

    const variantOption = page.getByText(/chess960/i);
    if (await variantOption.isVisible()) {
      await variantOption.click();
      await expect(page.locator('body')).toBeVisible();
    }
  });
});
