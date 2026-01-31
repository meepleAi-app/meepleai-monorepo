/**
 * Library Page - Playwright Visual Tests
 * Issue #2464, #2613, #2618 - User Library Management
 * Issue #2852 - Phase 3: Chromatic Visual Regression Testing
 *
 * SPECIAL NOTE: This page uses framer-motion with SSR disabled (next/dynamic ssr:false).
 * Chromatic requires Playwright-based tests for client-side animations to capture correctly.
 *
 * This file uses @chromatic-com/playwright for visual regression testing.
 *
 * Run with: npx playwright test --project=chromatic
 */

import { test, expect } from '@chromatic-com/playwright';

const LIBRARY_MOCK_DATA = {
  games: [
    {
      id: 'game-1',
      title: 'Catan',
      imageUrl: 'https://example.com/catan.jpg',
      isFavorite: true,
      notes: 'Great game for family nights!',
      averageRating: 7.2,
      addedAt: '2025-01-15T10:00:00Z',
    },
    {
      id: 'game-2',
      title: 'Ticket to Ride',
      imageUrl: 'https://example.com/ticket.jpg',
      isFavorite: false,
      notes: 'Perfect for beginners',
      averageRating: 7.4,
      addedAt: '2025-01-20T08:00:00Z',
    },
    {
      id: 'game-3',
      title: 'Gloomhaven',
      imageUrl: 'https://example.com/gloomhaven.jpg',
      isFavorite: true,
      notes: null,
      averageRating: 8.8,
      addedAt: '2025-01-25T14:30:00Z',
    },
  ],
  quota: {
    used: 3,
    limit: 50,
    percentageUsed: 6,
  },
};

test.describe('Library Page - Visual Tests', () => {
  test.beforeEach(async ({ page }) => {
    // Mock API responses
    await page.route('**/api/v1/library/games', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(LIBRARY_MOCK_DATA),
      });
    });

    await page.route('**/api/v1/auth/me', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'user-1',
          email: 'user@example.com',
          role: 'User',
        }),
      });
    });
  });

  test('should render library with games - desktop', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.goto('/library');

    // Wait for animations to complete
    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });
    await page.waitForTimeout(1000); // Allow framer-motion animations to settle

    // Chromatic automatically captures snapshot
    await expect(page).toHaveTitle(/Library/i);
  });

  test('should render library with games - tablet', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/library');

    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });
    await page.waitForTimeout(1000);
  });

  test('should render library with games - mobile', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/library');

    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });
    await page.waitForTimeout(1000);
  });

  test('should render loading state', async ({ page }) => {
    // Mock delayed response
    await page.route('**/api/v1/library/games', async route => {
      await page.waitForTimeout(5000);
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(LIBRARY_MOCK_DATA),
      });
    });

    await page.goto('/library');
    await page.waitForSelector('[class*="skeleton"]', { timeout: 2000 });
    // Chromatic captures loading skeleton state
  });

  test('should render empty state', async ({ page }) => {
    await page.route('**/api/v1/library/games', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: [],
          quota: { used: 0, limit: 50, percentageUsed: 0 },
        }),
      });
    });

    await page.goto('/library');
    await page.waitForSelector('text=/No games in your library/i', { timeout: 3000 });
  });

  test('should render error state', async ({ page }) => {
    await page.route('**/api/v1/library/games', async route => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Failed to load library' }),
      });
    });

    await page.goto('/library');
    await page.waitForSelector('text=/error/i', { timeout: 3000 });
  });

  test('should render with favorites filter active', async ({ page }) => {
    await page.goto('/library');
    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });

    // Click favorites filter
    const favoritesButton = page.locator('button:has-text("Favorites")');
    await favoritesButton.click();
    await page.waitForTimeout(1000); // Allow filter animation

    // Should show only favorite games
    const gameCards = page.locator('[data-testid="game-card"]');
    await expect(gameCards).toHaveCount(2); // Only 2 favorites in mock data
  });

  test('should render quota warning state', async ({ page }) => {
    await page.route('**/api/v1/library/games', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ...LIBRARY_MOCK_DATA,
          quota: { used: 45, limit: 50, percentageUsed: 90 },
        }),
      });
    });

    await page.goto('/library');
    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });
    await page.waitForTimeout(500);

    // Check for quota warning
    await expect(page.locator('text=/quota/i')).toBeVisible();
  });

  test('should render quota limit reached', async ({ page }) => {
    await page.route('**/api/v1/library/games', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ...LIBRARY_MOCK_DATA,
          quota: { used: 50, limit: 50, percentageUsed: 100 },
        }),
      });
    });

    await page.goto('/library');
    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });
    await page.waitForTimeout(500);
  });

  test('should render bulk selection mode', async ({ page }) => {
    await page.goto('/library');
    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });

    // Enter bulk selection mode
    const bulkSelectButton = page.locator('button:has-text("Select")');
    await bulkSelectButton.click();
    await page.waitForTimeout(500);

    // Select a game
    const firstCard = page.locator('[data-testid="game-card"]').first();
    await firstCard.click();
    await page.waitForTimeout(500);

    // Floating action bar should appear
    await expect(page.locator('[data-testid="bulk-action-bar"]')).toBeVisible();
  });

  test('should render large library with many games', async ({ page }) => {
    const largeLibrary = {
      games: Array.from({ length: 24 }, (_, i) => ({
        id: `game-${i + 1}`,
        title: `Board Game ${i + 1}`,
        imageUrl: `https://example.com/game${i + 1}.jpg`,
        isFavorite: i % 3 === 0,
        notes: i % 2 === 0 ? `Notes for game ${i + 1}` : null,
        averageRating: 6 + (i % 3),
        addedAt: new Date(2025, 0, i + 1).toISOString(),
      })),
      quota: { used: 24, limit: 50, percentageUsed: 48 },
    };

    await page.route('**/api/v1/library/games', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(largeLibrary),
      });
    });

    await page.goto('/library');
    await page.waitForSelector('[data-testid="game-card"]', { timeout: 5000 });
    await page.waitForTimeout(1500); // Allow staggered animations to complete

    const gameCards = page.locator('[data-testid="game-card"]');
    await expect(gameCards).toHaveCount(24);
  });
});
