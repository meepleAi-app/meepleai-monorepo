/**
 * Gaming Hub Dashboard E2E Tests - Issue #4587
 * Epic #4575: Gaming Hub Dashboard - Phase 3
 */

import { test, expect } from '@playwright/test';

test.describe('Gaming Hub Dashboard', () => {
  test.beforeEach(async ({ page, context }) => {
    // Mock API responses at context level (CRITICAL for Next.js)
    await context.route('**/api/v1/users/me/stats', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalGames: 47,
          monthlyPlays: 12,
          monthlyPlaysChange: 15,
          weeklyPlayTime: '08:30:00',
          monthlyFavorites: 3,
        }),
      });
    });

    await context.route('**/api/v1/sessions/recent*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: '123',
            gameName: 'Catan',
            gameImageUrl: 'https://placehold.co/200/orange/white',
            sessionDate: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
            playerCount: 4,
            duration: '01:30:00',
            averageScore: 75,
            winnerName: 'Marco',
          },
        ]),
      });
    });

    await context.route('**/api/v1/users/me/games*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: '1',
              title: 'Catan',
              averageRating: 9.2,
              playCount: 5,
              isOwned: true,
              inWishlist: false,
            },
            {
              id: '2',
              title: 'Azul',
              averageRating: 8.5,
              playCount: 3,
              isOwned: true,
              inWishlist: false,
            },
          ],
          totalCount: 47,
          page: 1,
          pageSize: 20,
          totalPages: 3,
        }),
      });
    });
  });

  test('loads dashboard with all sections', async ({ page }) => {
    await page.goto('/');

    // Welcome banner
    await expect(page.getByText(/Benvenuto/)).toBeVisible();

    // Quick stats
    await expect(page.getByText('47')).toBeVisible();
    await expect(page.getByText('Giochi Collezione')).toBeVisible();

    // Recent sessions
    await expect(page.getByText('Sessioni Recenti')).toBeVisible();
    await expect(page.getByText('Catan')).toBeVisible();

    // Game collection
    await expect(page.getByText('I Miei Giochi')).toBeVisible();
  });

  test('empty states render when no data', async ({ page, context }) => {
    // Mock empty responses
    await context.route('**/api/v1/sessions/recent*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    await page.goto('/');

    await expect(page.getByText(/Nessuna partita recente/)).toBeVisible();
  });
});
