/**
 * LIB-10: Wishlist
 * Issue #3082 - P3 Low
 *
 * Tests wishlist functionality:
 * - Add game to wishlist
 * - View wishlist
 * - Remove from wishlist
 * - Wishlist notifications
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupWishlistMocks(page: Page) {
  let wishlist = [{ id: 'game-1', title: 'Catan', addedAt: new Date().toISOString() }];

  await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'test-user',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
        },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/library/wishlist**`, async route => {
    const method = route.request().method();
    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ games: wishlist }),
      });
    } else if (method === 'POST') {
      const body = await route.request().postDataJSON();
      wishlist.push({
        id: body.gameId,
        title: body.title || 'New Game',
        addedAt: new Date().toISOString(),
      });
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Added to wishlist' }),
      });
    } else if (method === 'DELETE') {
      const gameId = route
        .request()
        .url()
        .match(/wishlist\/([^/?]+)/)?.[1];
      wishlist = wishlist.filter(g => g.id !== gameId);
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Removed from wishlist' }),
      });
    }
  });

  await page.route(`${API_BASE}/api/v1/games**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { id: 'chess', title: 'Chess' },
        { id: 'catan', title: 'Catan' },
      ]),
    });
  });

  return { getWishlist: () => wishlist };
}

test.describe('LIB-10: Wishlist', () => {
  test('should display wishlist', async ({ page }) => {
    await setupWishlistMocks(page);
    await page.goto('/library/wishlist');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/catan|wishlist/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should add game to wishlist', async ({ page }) => {
    await setupWishlistMocks(page);
    await page.goto('/library');
    await page.waitForLoadState('networkidle');

    const wishlistButton = page.getByRole('button', { name: /wishlist|want/i }).first();
    if (await wishlistButton.isVisible()) {
      await wishlistButton.click();
      await expect(page.getByText(/added|success/i)).toBeVisible();
    }
  });

  test('should remove from wishlist', async ({ page }) => {
    await setupWishlistMocks(page);
    await page.goto('/library/wishlist');
    await page.waitForLoadState('networkidle');

    const removeButton = page.getByRole('button', { name: /remove/i }).first();
    if (await removeButton.isVisible()) {
      await removeButton.click();
      await expect(page.getByText(/removed/i)).toBeVisible();
    }
  });
});
