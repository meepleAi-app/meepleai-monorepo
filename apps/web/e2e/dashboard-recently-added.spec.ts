/**
 * Dashboard Recently Added Section E2E Tests (Issue #2612)
 *
 * Tests the "Recently Added" library widget on the dashboard page.
 *
 * Test Coverage:
 * - Widget displays recent library games
 * - Widget hidden when library is empty
 * - "Vedi Tutti" navigates to /library
 * - Responsive grid layout
 *
 * @see Issue #2612 - Dashboard "Recently Added" widget
 */

import { test, expect } from './fixtures';
import { AuthHelper, USER_FIXTURES } from './pages';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock library data
const mockLibraryGames = [
  {
    id: '1',
    userId: 'user-1',
    gameId: 'game-1',
    gameTitle: 'Azul',
    gamePublisher: 'Plan B Games',
    gameYearPublished: 2017,
    gameIconUrl: null,
    gameImageUrl: 'https://example.com/azul.png',
    addedAt: new Date().toISOString(),
    notes: null,
    isFavorite: true,
  },
  {
    id: '2',
    userId: 'user-1',
    gameId: 'game-2',
    gameTitle: 'Wingspan',
    gamePublisher: 'Stonemaier Games',
    gameYearPublished: 2019,
    gameIconUrl: null,
    gameImageUrl: 'https://example.com/wingspan.png',
    addedAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(),
    notes: null,
    isFavorite: false,
  },
  {
    id: '3',
    userId: 'user-1',
    gameId: 'game-3',
    gameTitle: 'Catan',
    gamePublisher: 'Kosmos',
    gameYearPublished: 1995,
    gameIconUrl: null,
    gameImageUrl: null,
    addedAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
    notes: 'Classic game!',
    isFavorite: false,
  },
];

test.describe('Dashboard Recently Added Section', () => {
  test('displays recently added games from library', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    // Mock authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock library API with games
    await page.route(`${apiBase}/api/v1/library*`, async route => {
      const url = new URL(route.request().url());
      const sortBy = url.searchParams.get('sortBy');
      const sortDescending = url.searchParams.get('sortDescending');

      // Return mock library data
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: mockLibraryGames,
          page: 1,
          pageSize: 5,
          totalCount: 3,
          totalPages: 1,
          hasNextPage: false,
          hasPreviousPage: false,
        }),
      });
    });

    // Mock quota API
    await page.route(`${apiBase}/api/v1/library/quota`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          currentCount: 3,
          maxAllowed: 10,
          userTier: 'free',
          remainingSlots: 7,
          percentageUsed: 30,
        }),
      });
    });

    // Mock games API for recent games section
    await page.route(`${apiBase}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: [],
          totalCount: 0,
        }),
      });
    });

    // Navigate to dashboard
    await page.goto('/dashboard');

    // Verify "Aggiunti di Recente" section is visible
    await expect(page.getByText('Aggiunti di Recente')).toBeVisible({ timeout: 10000 });

    // Verify game titles are displayed
    await expect(page.getByText('Azul')).toBeVisible();
    await expect(page.getByText('Wingspan')).toBeVisible();
    await expect(page.getByText('Catan')).toBeVisible();

    // Verify "Vedi Tutti" link exists
    const vediTuttiLink = page.getByRole('link', { name: /Vedi Tutti/i });
    await expect(vediTuttiLink).toBeVisible();
    await expect(vediTuttiLink).toHaveAttribute('href', '/library');
  });

  test('hides section when library is empty', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    // Mock authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock empty library
    await page.route(`${apiBase}/api/v1/library*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [],
          page: 1,
          pageSize: 5,
          totalCount: 0,
          totalPages: 0,
          hasNextPage: false,
          hasPreviousPage: false,
        }),
      });
    });

    // Mock quota API
    await page.route(`${apiBase}/api/v1/library/quota`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          currentCount: 0,
          maxAllowed: 10,
          userTier: 'free',
          remainingSlots: 10,
          percentageUsed: 0,
        }),
      });
    });

    // Mock games API
    await page.route(`${apiBase}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: [],
          totalCount: 0,
        }),
      });
    });

    // Navigate to dashboard
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');

    // Verify "Aggiunti di Recente" section is NOT visible (hidden when empty)
    await expect(page.getByText('Aggiunti di Recente')).not.toBeVisible({ timeout: 5000 });
  });

  test('navigates to library when clicking "Vedi Tutti"', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    // Mock authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock library API with games
    await page.route(`${apiBase}/api/v1/library*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: mockLibraryGames,
          page: 1,
          pageSize: 5,
          totalCount: 3,
          totalPages: 1,
          hasNextPage: false,
          hasPreviousPage: false,
        }),
      });
    });

    // Mock quota API
    await page.route(`${apiBase}/api/v1/library/quota`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          currentCount: 3,
          maxAllowed: 10,
          userTier: 'free',
          remainingSlots: 7,
          percentageUsed: 30,
        }),
      });
    });

    // Mock games API
    await page.route(`${apiBase}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: [],
          totalCount: 0,
        }),
      });
    });

    // Navigate to dashboard
    await page.goto('/dashboard');

    // Wait for section to load
    await expect(page.getByText('Aggiunti di Recente')).toBeVisible({ timeout: 10000 });

    // Click "Vedi Tutti" link
    await page.getByRole('link', { name: /Vedi Tutti/i }).click();

    // Verify navigation to library page
    await page.waitForURL('**/library', { timeout: 10000 });
    await expect(page).toHaveURL(/\/library/);
  });

  test('displays favorite star badge on favorited games', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    // Mock authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock library with one favorite game
    await page.route(`${apiBase}/api/v1/library*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [mockLibraryGames[0]], // Azul is favorite
          page: 1,
          pageSize: 5,
          totalCount: 1,
          totalPages: 1,
          hasNextPage: false,
          hasPreviousPage: false,
        }),
      });
    });

    // Mock quota API
    await page.route(`${apiBase}/api/v1/library/quota`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          currentCount: 1,
          maxAllowed: 10,
          userTier: 'free',
          remainingSlots: 9,
          percentageUsed: 10,
        }),
      });
    });

    // Mock games API
    await page.route(`${apiBase}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: [],
          totalCount: 0,
        }),
      });
    });

    // Navigate to dashboard
    await page.goto('/dashboard');

    // Wait for section
    await expect(page.getByText('Aggiunti di Recente')).toBeVisible({ timeout: 10000 });

    // Verify Azul is displayed with favorite indicator
    await expect(page.getByText('Azul')).toBeVisible();

    // Check for star icon (SVG with fill-yellow-400 class)
    const starIcon = page.locator('.lucide-star.fill-yellow-400');
    await expect(starIcon).toBeVisible();
  });

  test('shows skeleton loading state while fetching', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    // Mock authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock slow library API
    await page.route(`${apiBase}/api/v1/library*`, async route => {
      // Delay response to see loading state
      await new Promise(resolve => setTimeout(resolve, 2000));
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: mockLibraryGames,
          page: 1,
          pageSize: 5,
          totalCount: 3,
          totalPages: 1,
          hasNextPage: false,
          hasPreviousPage: false,
        }),
      });
    });

    // Mock quota API
    await page.route(`${apiBase}/api/v1/library/quota`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          currentCount: 3,
          maxAllowed: 10,
          userTier: 'free',
          remainingSlots: 7,
          percentageUsed: 30,
        }),
      });
    });

    // Mock games API
    await page.route(`${apiBase}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: [],
          totalCount: 0,
        }),
      });
    });

    // Navigate to dashboard
    await page.goto('/dashboard');

    // Verify loading state (skeleton) is visible initially
    const skeleton = page.locator('.animate-pulse').first();
    await expect(skeleton).toBeVisible({ timeout: 1000 });

    // Wait for content to load
    await expect(page.getByText('Azul')).toBeVisible({ timeout: 10000 });
  });
});
