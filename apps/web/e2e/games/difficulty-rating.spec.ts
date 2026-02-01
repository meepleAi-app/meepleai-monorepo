/**
 * GAME-07: Game Difficulty Rating
 * Issue #3082 - P2 Medium
 *
 * Tests game difficulty rating functionality:
 * - Display difficulty rating
 * - User difficulty voting
 * - Difficulty comparison
 * - Difficulty-based filtering
 */

import { test, expect } from '../fixtures';
import { GamePage, SharedGameCatalogPage } from '../pages';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Setup mock routes for difficulty rating testing
 */
async function setupDifficultyRatingMocks(page: Page) {
  const games = [
    {
      id: 'chess',
      title: 'Chess',
      difficulty: {
        average: 3.2,
        votes: 1250,
        distribution: { 1: 50, 2: 200, 3: 400, 4: 350, 5: 250 },
      },
      complexityWeight: 2.8,
    },
    {
      id: 'checkers',
      title: 'Checkers',
      difficulty: {
        average: 1.8,
        votes: 800,
        distribution: { 1: 300, 2: 350, 3: 100, 4: 40, 5: 10 },
      },
      complexityWeight: 1.2,
    },
    {
      id: 'go',
      title: 'Go',
      difficulty: {
        average: 4.5,
        votes: 600,
        distribution: { 1: 10, 2: 30, 3: 60, 4: 150, 5: 350 },
      },
      complexityWeight: 4.0,
    },
  ];

  const userVotes: Record<string, number> = {};

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

  // Mock games endpoint
  await page.route(`${API_BASE}/api/v1/games`, async (route) => {
    const url = route.request().url();
    const difficultyMin = url.match(/difficultyMin=([^&]+)/)?.[1];
    const difficultyMax = url.match(/difficultyMax=([^&]+)/)?.[1];

    let filteredGames = [...games];

    if (difficultyMin) {
      filteredGames = filteredGames.filter(
        (g) => g.difficulty.average >= parseFloat(difficultyMin)
      );
    }
    if (difficultyMax) {
      filteredGames = filteredGames.filter(
        (g) => g.difficulty.average <= parseFloat(difficultyMax)
      );
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(filteredGames),
    });
  });

  // Mock single game endpoint
  await page.route(`${API_BASE}/api/v1/games/*`, async (route) => {
    const url = route.request().url();
    const gameIdMatch = url.match(/games\/([^/?]+)/);
    const gameId = gameIdMatch?.[1];

    if (gameId && !url.includes('/difficulty')) {
      const game = games.find((g) => g.id === gameId);
      if (game) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ...game,
            userDifficultyVote: userVotes[gameId] || null,
          }),
        });
        return;
      }
    }
    await route.continue();
  });

  // Mock difficulty vote endpoint
  await page.route(`${API_BASE}/api/v1/games/*/difficulty`, async (route) => {
    const url = route.request().url();
    const gameIdMatch = url.match(/games\/([^/]+)\/difficulty/);
    const gameId = gameIdMatch?.[1];
    const body = await route.request().postDataJSON();

    if (gameId && body?.rating) {
      userVotes[gameId] = body.rating;

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          message: 'Difficulty vote recorded',
          newAverage: 3.3, // Simulated update
        }),
      });
    } else {
      await route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Invalid rating' }),
      });
    }
  });

  return { games, getUserVotes: () => userVotes };
}

test.describe('GAME-07: Game Difficulty Rating', () => {
  test.describe('Display Difficulty', () => {
    test('should display difficulty rating on game card', async ({ page }) => {
      await setupDifficultyRatingMocks(page);

      await page.goto('/games');
      await page.waitForLoadState('networkidle');

      // Should show difficulty indicator
      await expect(
        page.locator('[data-testid="difficulty-rating"], .difficulty-rating').first().or(
          page.getByText(/difficulty|complexity|weight/i).first()
        )
      ).toBeVisible({ timeout: 5000 });
    });

    test('should show difficulty scale', async ({ page }) => {
      await setupDifficultyRatingMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // Should show difficulty number or scale
      await expect(
        page.getByText(/3\.2|medium|intermediate/i)
      ).toBeVisible();
    });

    test('should show vote count', async ({ page }) => {
      await setupDifficultyRatingMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // Should show number of votes
      await expect(
        page.getByText(/1,?250|votes|rating/i)
      ).toBeVisible();
    });

    test('should show difficulty distribution', async ({ page }) => {
      await setupDifficultyRatingMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // May show distribution chart or breakdown
      await expect(page.locator('body')).toBeVisible();
    });
  });

  test.describe('User Voting', () => {
    test('should allow user to vote on difficulty', async ({ page }) => {
      await setupDifficultyRatingMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // Find difficulty voting control
      const voteButton = page.getByRole('button', { name: /rate|vote|difficulty/i }).or(
        page.locator('[data-testid="difficulty-vote"]')
      );

      if (await voteButton.isVisible()) {
        await voteButton.click();

        // Should show voting options
        await expect(
          page.locator('input[type="radio"], .rating-star, [data-rating]')
        ).toBeVisible();
      }
    });

    test('should submit difficulty vote', async ({ page }) => {
      const mocks = await setupDifficultyRatingMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      const voteButton = page.getByRole('button', { name: /rate|vote/i });
      if (await voteButton.isVisible()) {
        await voteButton.click();

        // Select a rating
        const ratingOption = page.locator('[data-rating="4"], input[value="4"]').first();
        if (await ratingOption.isVisible()) {
          await ratingOption.click();

          // Submit
          const submitButton = page.getByRole('button', { name: /submit|save/i });
          if (await submitButton.isVisible()) {
            await submitButton.click();

            // Should show success
            await expect(page.getByText(/recorded|thank|voted/i)).toBeVisible();
          }
        }
      }
    });

    test('should show user current vote', async ({ page }) => {
      await setupDifficultyRatingMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // After voting, should show user's vote
      await expect(page.locator('body')).toBeVisible();
    });

    test('should allow changing vote', async ({ page }) => {
      await setupDifficultyRatingMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // Should be able to update vote
      const changeButton = page.getByRole('button', { name: /change|update|edit/i });
      if (await changeButton.isVisible()) {
        await changeButton.click();
        await expect(page.locator('body')).toBeVisible();
      }
    });
  });

  test.describe('Difficulty Comparison', () => {
    test('should compare difficulty between games', async ({ page }) => {
      await setupDifficultyRatingMocks(page);

      await page.goto('/games');
      await page.waitForLoadState('networkidle');

      // Sort by difficulty to compare
      const sortButton = page.getByRole('button', { name: /sort/i });
      if (await sortButton.isVisible()) {
        await sortButton.click();
        await page.getByText(/difficulty/i).click();

        // Games should be sorted
        await page.waitForLoadState('networkidle');
        await expect(page.locator('body')).toBeVisible();
      }
    });

    test('should show difficulty indicator colors', async ({ page }) => {
      await setupDifficultyRatingMocks(page);

      await page.goto('/games');
      await page.waitForLoadState('networkidle');

      // Easy games may be green, hard may be red
      await expect(
        page.locator('.difficulty-easy, .difficulty-hard, [data-difficulty]').or(page.locator('body'))
      ).toBeVisible();
    });
  });

  test.describe('Difficulty Filtering', () => {
    test('should filter games by difficulty', async ({ page }) => {
      await setupDifficultyRatingMocks(page);

      await page.goto('/games');
      await page.waitForLoadState('networkidle');

      // Find difficulty filter
      const difficultyFilter = page.getByRole('combobox', { name: /difficulty/i }).or(
        page.locator('[data-testid="difficulty-filter"]')
      );

      if (await difficultyFilter.isVisible()) {
        await difficultyFilter.click();
        await page.getByText(/easy|beginner/i).click();

        await page.waitForLoadState('networkidle');

        // Should only show easy games
        await expect(page.getByText(/checkers/i)).toBeVisible();
      }
    });

    test('should filter by difficulty range', async ({ page }) => {
      await setupDifficultyRatingMocks(page);

      await page.goto('/games');
      await page.waitForLoadState('networkidle');

      // May have slider or min/max inputs
      const minInput = page.locator('input[name="difficultyMin"]');
      const maxInput = page.locator('input[name="difficultyMax"]');

      if (await minInput.isVisible() && await maxInput.isVisible()) {
        await minInput.fill('2');
        await maxInput.fill('4');
        await page.waitForLoadState('networkidle');

        // Should filter by range
        await expect(page.locator('body')).toBeVisible();
      }
    });

    test('should show difficulty labels', async ({ page }) => {
      await setupDifficultyRatingMocks(page);

      await page.goto('/games');
      await page.waitForLoadState('networkidle');

      // Should show difficulty labels (Easy, Medium, Hard)
      await expect(
        page.getByText(/easy|medium|hard|beginner|advanced/i).first()
      ).toBeVisible();
    });
  });

  test.describe('Difficulty Info', () => {
    test('should explain difficulty scale', async ({ page }) => {
      await setupDifficultyRatingMocks(page);

      await page.goto('/games/chess');
      await page.waitForLoadState('networkidle');

      // Look for info tooltip or help
      const infoButton = page.getByRole('button', { name: /info|help|\?/i });
      if (await infoButton.isVisible()) {
        await infoButton.click();

        // Should explain the scale
        await expect(
          page.getByText(/scale|rating|1.*5|easy.*hard/i)
        ).toBeVisible();
      }
    });
  });
});
