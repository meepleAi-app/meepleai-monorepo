/**
 * E2E Tests for Game Search & Browse (Issue #843 Phase 3) - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/ - Page Object Model architecture
 */

import { test, expect } from './fixtures/chromatic';
import { GamePage } from './pages/game/GamePage';
import { AuthHelper, USER_FIXTURES } from './pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * E2E Tests for Game Search & Browse (Issue #843 Phase 3)
 *
 * Test Coverage:
 * - Browse games list on homepage
 * - Search by game name (exact match, partial)
 * - Search with no results
 * - Filter by category (if implemented)
 * - Sort by name (A-Z, Z-A)
 * - Sort by date added (newest, oldest)
 * - Pagination (next, previous, page numbers)
 * - Games per page selection (10, 25, 50)
 * - Game card displays (name, image, description)
 * - Click game card navigates to chat
 * - Search with special characters
 * - Search case insensitivity
 * - Clear search resets results
 *
 * Testing Strategy:
 * - Use Page Object Model (GamePage)
 * - Mock game API responses with realistic data
 * - Test search, filter, sort, and pagination
 * - Verify game card interactions and navigation
 *
 * Expected Pass Rate: 85%+ (13/15 tests passing)
 */

// Mock game data
const MOCK_GAMES = [
  {
    id: 'chess-001',
    title: 'Chess',
    description: 'Classic strategy board game',
    category: 'strategy',
    createdAt: '2025-01-15T00:00:00Z',
    imageUrl: '/images/chess.jpg',
  },
  {
    id: 'catan-002',
    title: 'Settlers of Catan',
    description: 'Resource management and trading game',
    category: 'strategy',
    createdAt: '2025-01-10T00:00:00Z',
    imageUrl: '/images/catan.jpg',
  },
  {
    id: 'uno-003',
    title: 'UNO',
    description: 'Fast-paced card game',
    category: 'card',
    createdAt: '2025-01-20T00:00:00Z',
    imageUrl: '/images/uno.jpg',
  },
  {
    id: 'monopoly-004',
    title: 'Monopoly',
    description: 'Property trading board game',
    category: 'family',
    createdAt: '2025-01-05T00:00:00Z',
    imageUrl: '/images/monopoly.jpg',
  },
  {
    id: 'scrabble-005',
    title: 'Scrabble',
    description: 'Word-building tile game',
    category: 'word',
    createdAt: '2025-01-12T00:00:00Z',
    imageUrl: '/images/scrabble.jpg',
  },
];

test.describe('Game Search & Browse', () => {
  let gamePage: GamePage;

  test.beforeEach(async ({ page }) => {
    gamePage = new GamePage(page);
    const authHelper = new AuthHelper(page);

    // Disable animations for stable tests
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Mock authentication using AuthHelper
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock default game list (all games)
    await page.route(`${API_BASE}/api/v1/games*`, async route => {
      const url = new URL(route.request().url());
      const search = url.searchParams.get('search')?.toLowerCase();
      const sortBy = url.searchParams.get('sortBy') || 'name-asc';
      const category = url.searchParams.get('category');
      const page = parseInt(url.searchParams.get('page') || '1', 10);
      const limit = parseInt(url.searchParams.get('limit') || '10', 10);

      let filteredGames = [...MOCK_GAMES];

      // Apply search filter
      if (search) {
        filteredGames = filteredGames.filter(game => game.title.toLowerCase().includes(search));
      }

      // Apply category filter
      if (category && category !== 'all') {
        filteredGames = filteredGames.filter(game => game.category === category);
      }

      // Apply sorting
      filteredGames.sort((a, b) => {
        switch (sortBy) {
          case 'name-asc':
            return a.title.localeCompare(b.title);
          case 'name-desc':
            return b.title.localeCompare(a.title);
          case 'date-asc':
            return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
          case 'date-desc':
            return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
          default:
            return 0;
        }
      });

      // Apply pagination
      const startIndex = (page - 1) * limit;
      const endIndex = startIndex + limit;
      const paginatedGames = filteredGames.slice(startIndex, endIndex);

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: paginatedGames,
          total: filteredGames.length,
          page,
          limit,
          totalPages: Math.ceil(filteredGames.length / limit),
        }),
      });
    });
  });

  /**
   * Test Group 1: Basic Browsing
   */
  test.describe('Basic Browsing', () => {
    test('browse games list on homepage', async ({ page }) => {
      await gamePage.goto();

      // Wait for game list to load
      await gamePage.assertGameListVisible();

      // Should show at least some games
      const gameCount = await page.locator('[data-testid="game-card"]').count();
      expect(gameCount).toBeGreaterThan(0);
      expect(gameCount).toBeLessThanOrEqual(10); // Default page size
    });
  });

  /**
   * Test Group 2: Search Functionality
   */
  test.describe('Search Functionality', () => {
    test('search by game name (exact match)', async ({ page }) => {
      await gamePage.goto();
      await gamePage.searchGames('Chess');

      // Should find exactly Chess
      await gamePage.assertGameCardDisplays(0, 'Chess');
      await gamePage.assertGameCount(1);
    });

    test('search by partial name', async ({ page }) => {
      await gamePage.goto();
      await gamePage.searchGames('cat'); // Matches "Settlers of Catan"

      // Should find games containing "cat"
      const gameCount = await page.locator('[data-testid="game-card"]').count();
      expect(gameCount).toBeGreaterThan(0);
      await gamePage.assertGameCardDisplays(0, 'Catan');
    });

    test('search with no results', async ({ page }) => {
      await gamePage.goto();
      await gamePage.searchGames('NonexistentGame12345');

      // Should show no results message
      await gamePage.assertNoResults();
      await gamePage.assertGameCount(0);
    });

    test('search with special characters', async ({ page }) => {
      await gamePage.goto();
      await gamePage.searchGames('Chess!@#$');

      // Should handle special characters gracefully (strip or ignore)
      // Either show no results or show Chess (depending on implementation)
      const noResultsVisible = await page.locator('text=/no games found/i').isVisible();
      const chessVisible = await page.locator('text=Chess').isVisible();
      expect(noResultsVisible || chessVisible).toBe(true);
    });

    test('search case insensitivity', async ({ page }) => {
      await gamePage.goto();

      // Search with different cases
      await gamePage.searchGames('CHESS');
      await gamePage.assertGameCardDisplays(0, 'Chess');

      await gamePage.clearSearch();
      await gamePage.searchGames('chess');
      await gamePage.assertGameCardDisplays(0, 'Chess');

      await gamePage.clearSearch();
      await gamePage.searchGames('ChEsS');
      await gamePage.assertGameCardDisplays(0, 'Chess');
    });

    test('clear search resets results', async ({ page }) => {
      await gamePage.goto();

      // Search for specific game
      await gamePage.searchGames('Chess');
      await gamePage.assertGameCount(1);

      // Clear search
      await gamePage.clearSearch();

      // Should show all games again
      const gameCount = await page.locator('[data-testid="game-card"]').count();
      expect(gameCount).toBeGreaterThan(1);
    });
  });

  /**
   * Test Group 3: Filtering
   */
  test.describe('Filtering', () => {
    test('filter by category (if implemented)', async ({ page }) => {
      await gamePage.goto();

      // Check if category filter exists
      const categoryFilter = page.getByRole('combobox', { name: /category/i });
      const filterExists = (await categoryFilter.count()) > 0;

      if (filterExists) {
        await gamePage.filterByCategory('strategy');

        // Should show only strategy games
        const gameCount = await page.locator('[data-testid="game-card"]').count();
        expect(gameCount).toBeGreaterThan(0);

        // Verify Chess and Catan are shown (both strategy)
        const chessVisible = await page.locator('text=Chess').isVisible();
        const catanVisible = await page.locator('text=Catan').isVisible();
        expect(chessVisible || catanVisible).toBe(true);
      } else {
        // Skip test if category filter not implemented
        test.skip();
      }
    });
  });

  /**
   * Test Group 4: Sorting
   */
  test.describe('Sorting', () => {
    test('sort by name (A-Z)', async ({ page }) => {
      await gamePage.goto();
      await gamePage.sortBy('name-asc');

      // First game should be Chess (alphabetically first in our mock data)
      await gamePage.assertGameCardDisplays(0, 'Chess');
    });

    test('sort by name (Z-A)', async ({ page }) => {
      await gamePage.goto();
      await gamePage.sortBy('name-desc');

      // First game should be UNO (alphabetically last in our mock data)
      await gamePage.assertGameCardDisplays(0, 'UNO');
    });

    test('sort by date added (newest)', async ({ page }) => {
      await gamePage.goto();
      await gamePage.sortBy('date-desc');

      // Newest game is UNO (2025-01-20)
      await gamePage.assertGameCardDisplays(0, 'UNO');
    });

    test('sort by date added (oldest)', async ({ page }) => {
      await gamePage.goto();
      await gamePage.sortBy('date-asc');

      // Oldest game is Monopoly (2025-01-05)
      await gamePage.assertGameCardDisplays(0, 'Monopoly');
    });
  });

  /**
   * Test Group 5: Pagination
   */
  test.describe('Pagination', () => {
    test('pagination (next page, previous page)', async ({ page }) => {
      await gamePage.goto();
      await gamePage.setGamesPerPage(10); // Show 10 games per page

      // Should show 2 games on first page
      await gamePage.assertGameCount(2);

      // Go to next page
      await gamePage.goToNextPage();

      // Should show different games
      await gamePage.assertGameCount(2);

      // Go back to previous page
      await gamePage.goToPreviousPage();

      // Should show original games
      await gamePage.assertGameCount(2);
    });

    test('pagination (page numbers)', async ({ page }) => {
      await gamePage.goto();
      await gamePage.setGamesPerPage(10);

      // Check if page number buttons exist
      const page2Button = page.getByRole('button', { name: /^2$/ });
      const paginationExists = (await page2Button.count()) > 0;

      if (paginationExists) {
        // Go to page 2 using page number
        await gamePage.goToPage(2);

        // Should show page 2 games
        await gamePage.assertGameCount(2);

        // Go back to page 1
        await gamePage.goToPage(1);
        await gamePage.assertGameCount(2);
      } else {
        // Skip if page numbers not implemented (only prev/next)
        test.skip();
      }
    });

    test('games per page selection (10, 25, 50)', async ({ page }) => {
      await gamePage.goto();

      // Set to 10 per page (default)
      await gamePage.setGamesPerPage(10);
      let gameCount = await page.locator('[data-testid="game-card"]').count();
      expect(gameCount).toBeLessThanOrEqual(10);

      // Set to 25 per page (should show all 5 mock games)
      await gamePage.setGamesPerPage(25);
      gameCount = await page.locator('[data-testid="game-card"]').count();
      expect(gameCount).toBe(5); // All mock games fit in one page

      // Set to 50 per page (should still show all 5)
      await gamePage.setGamesPerPage(50);
      gameCount = await page.locator('[data-testid="game-card"]').count();
      expect(gameCount).toBe(5);
    });
  });

  /**
   * Test Group 6: Game Card Display
   */
  test.describe('Game Card Display', () => {
    test('game card displays correctly (name, image, description)', async ({ page }) => {
      await gamePage.goto();

      // Get first game card
      const gameName = await gamePage.getGameName(0);
      const hasImage = await gamePage.hasGameImage(0);
      const gameDescription = await gamePage.getGameDescription(0);

      // Verify card has required elements
      expect(gameName).toBeTruthy();
      expect(gameName.length).toBeGreaterThan(0);
      expect(hasImage).toBe(true);
      expect(gameDescription).toBeTruthy();
      expect(gameDescription.length).toBeGreaterThan(0);
    });

    test('click game card navigates to chat', async ({ page }) => {
      await gamePage.goto();

      // Mock chat page endpoint
      await page.route(`${API_BASE}/api/v1/games/*/agents`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      });

      // Click first game card
      await gamePage.clickGameCard(0);

      // Should navigate to chat page (or game-specific page)
      await page.waitForURL(/\/(chat|game)/);
      const currentUrl = page.url();
      expect(currentUrl).toMatch(/\/(chat|game)/);
    });
  });
});
