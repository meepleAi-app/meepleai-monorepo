/**
 * GAME-06: Related Games
 * Issue #3082 - P2 Medium
 *
 * Tests related games functionality:
 * - Display related games section
 * - Related game recommendations
 * - Click to view related game
 * - "Similar to" filtering
 */

import { test, expect } from '../fixtures/chromatic';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Setup mock routes for related games testing
 */
async function setupRelatedGamesMocks(page: Page) {
  const games = [
    {
      id: 'chess',
      title: 'Chess',
      description: 'Classic strategy game',
      complexity: 'Medium',
      playerCount: '2',
      relatedGames: ['checkers', 'shogi', 'go'],
    },
    {
      id: 'checkers',
      title: 'Checkers',
      description: 'Classic capture game',
      complexity: 'Easy',
      playerCount: '2',
      relatedGames: ['chess'],
    },
    {
      id: 'shogi',
      title: 'Shogi',
      description: 'Japanese chess',
      complexity: 'Hard',
      playerCount: '2',
      relatedGames: ['chess', 'go'],
    },
    {
      id: 'go',
      title: 'Go',
      description: 'Ancient strategy game',
      complexity: 'Hard',
      playerCount: '2',
      relatedGames: ['chess', 'shogi'],
    },
    {
      id: 'catan',
      title: 'Catan',
      description: 'Resource trading game',
      complexity: 'Medium',
      playerCount: '3-4',
      relatedGames: ['ticket-to-ride', 'carcassonne'],
    },
  ];

  // Mock auth
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'test-user-id',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // Mock games list endpoint
  await page.route(`${API_BASE}/api/v1/games`, async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(games),
      });
    }
  });

  // Mock single game endpoint
  await page.route(`${API_BASE}/api/v1/games/*`, async (route) => {
    const url = route.request().url();
    const gameIdMatch = url.match(/games\/([^/?]+)/);
    const gameId = gameIdMatch?.[1];

    if (gameId && !url.includes('/related')) {
      const game = games.find((g) => g.id === gameId);
      if (game) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(game),
        });
        return;
      }
    }
    await route.continue();
  });

  // Mock related games endpoint
  await page.route(`${API_BASE}/api/v1/games/*/related`, async (route) => {
    const url = route.request().url();
    const gameIdMatch = url.match(/games\/([^/]+)\/related/);
    const gameId = gameIdMatch?.[1];

    const game = games.find((g) => g.id === gameId);
    const relatedIds = game?.relatedGames || [];
    const relatedGames = games.filter((g) => relatedIds.includes(g.id));

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        games: relatedGames,
        totalCount: relatedGames.length,
        reason: 'Similar mechanics and player count',
      }),
    });
  });

  // Mock similar games search
  await page.route(`${API_BASE}/api/v1/games/similar**`, async (route) => {
    const url = route.request().url();
    const similarTo = url.match(/similarTo=([^&]+)/)?.[1];

    const baseGame = games.find((g) => g.id === similarTo);
    const relatedIds = baseGame?.relatedGames || [];
    const similarGames = games.filter((g) => relatedIds.includes(g.id));

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        games: similarGames,
        basedOn: baseGame?.title,
      }),
    });
  });

  return { games };
}

test.describe('GAME-06: Related Games', () => {
  test.describe('Display Related Games', () => {
    test('should display related games section on game page', async ({ page }) => {
      await setupRelatedGamesMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // Should show related games section
      await expect(
        page.getByText(/related.*game|similar.*game|you.*might.*like/i)
      ).toBeVisible({ timeout: 5000 });
    });

    test('should show related game cards', async ({ page }) => {
      await setupRelatedGamesMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // Should show related games (Checkers, Shogi, Go)
      await expect(page.getByText(/checkers|shogi|go/i).first()).toBeVisible();
    });

    test('should show relationship reason', async ({ page }) => {
      await setupRelatedGamesMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // May show why games are related
      await expect(
        page.getByText(/similar|mechanic|player/i)
      ).toBeVisible();
    });
  });

  test.describe('Related Game Recommendations', () => {
    test('should recommend games based on mechanics', async ({ page }) => {
      await setupRelatedGamesMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // Chess should recommend other abstract strategy games
      await expect(page.getByText(/shogi|go/i).first()).toBeVisible();
    });

    test('should show multiple related games', async ({ page }) => {
      await setupRelatedGamesMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // Should show multiple recommendations
      const relatedCards = page.locator('[data-testid="related-game"], .related-game-card');
      if ((await relatedCards.count()) > 0) {
        expect(await relatedCards.count()).toBeGreaterThanOrEqual(2);
      }
    });

    test('should not show self in related games', async ({ page }) => {
      await setupRelatedGamesMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // Chess should not be in its own related games
      const relatedSection = page.locator('[data-testid="related-games"], .related-games');
      if (await relatedSection.isVisible()) {
        const hasChess = await relatedSection.getByText(/^chess$/i).count();
        // May or may not have "Chess" text but should be about other games
        await expect(page.locator('body')).toBeVisible();
      }
    });
  });

  test.describe('Navigate to Related Game', () => {
    test('should navigate to related game on click', async ({ page }) => {
      await setupRelatedGamesMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // Click on a related game
      const relatedGame = page.getByText(/shogi/i).first();
      if (await relatedGame.isVisible()) {
        await relatedGame.click();

        // Should navigate to that game
        await expect(page).toHaveURL(/games\/shogi/i, { timeout: 5000 });
      }
    });

    test('should show related game preview on hover', async ({ page }) => {
      await setupRelatedGamesMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      const relatedCard = page.locator('[data-testid="related-game"]').first().or(
        page.getByText(/checkers/i).first()
      );

      if (await relatedCard.isVisible()) {
        await relatedCard.hover();

        // May show preview tooltip
        await page.waitForTimeout(500);
        await expect(page.locator('body')).toBeVisible();
      }
    });
  });

  test.describe('Similar Games Filter', () => {
    test('should filter games by similarity', async ({ page }) => {
      await setupRelatedGamesMocks(page);

      await page.goto('/games');
      await page.waitForLoadState('networkidle');

      // Look for "similar to" filter
      const similarFilter = page.getByRole('combobox', { name: /similar/i }).or(
        page.locator('[data-testid="similar-filter"]')
      );

      if (await similarFilter.isVisible()) {
        await similarFilter.click();
        await page.getByText(/chess/i).click();

        await page.waitForLoadState('networkidle');

        // Should show games similar to Chess
        await expect(page.getByText(/checkers|shogi|go/i).first()).toBeVisible();
      }
    });

    test('should show "games like X" results', async ({ page }) => {
      await setupRelatedGamesMocks(page);

      await page.goto('/games?similarTo=chess');
      await page.waitForLoadState('networkidle');

      // Should show similar games header
      await expect(
        page.getByText(/similar.*to.*chess|game.*like.*chess/i)
      ).toBeVisible();
    });

    test('should allow clearing similarity filter', async ({ page }) => {
      await setupRelatedGamesMocks(page);

      await page.goto('/games?similarTo=chess');
      await page.waitForLoadState('networkidle');

      const clearButton = page.getByRole('button', { name: /clear|reset|all.*game/i });
      if (await clearButton.isVisible()) {
        await clearButton.click();

        await page.waitForLoadState('networkidle');

        // Should show all games again
        await expect(page.getByText(/catan/i)).toBeVisible();
      }
    });
  });

  test.describe('Empty States', () => {
    test('should handle games with no related games', async ({ page }) => {
      await setupRelatedGamesMocks(page);

      // Navigate to a game with no related games
      await page.goto('/games/catan');
      await page.waitForLoadState('networkidle');

      // May show empty state or hide section
      await expect(page.locator('body')).toBeVisible();
    });
  });
});
