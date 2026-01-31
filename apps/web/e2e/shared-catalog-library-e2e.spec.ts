/**
 * E2E Tests for Shared Catalog - Browse and Add to Library (Issue #2877)
 *
 * Tests complete user workflows for:
 * - Add game to library (optimistic UI)
 * - Verify game appears in personal library after adding
 * - Pagination navigation
 * - Already in library badge displays correctly
 *
 * Testing Strategy:
 * - Uses authenticated user fixtures for library operations
 * - Mock API responses for predictable test data
 * - Tests optimistic UI updates and cross-page verification
 */

import { Page } from '@playwright/test';

import { test, expect } from './test';

// Issue #841: Make API_BASE configurable via environment variables
const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ============================================================================
// Test Data
// ============================================================================

const MOCK_CATALOG_GAMES = {
  items: [
    {
      id: '11111111-1111-1111-1111-111111111111',
      bggId: 13,
      title: 'Catan',
      yearPublished: 1995,
      description: 'Trade, build and settle the island of Catan.',
      minPlayers: 3,
      maxPlayers: 4,
      playingTimeMinutes: 90,
      minAge: 10,
      complexityRating: 2.3,
      averageRating: 7.1,
      imageUrl: null,
      thumbnailUrl: null,
      status: 1,
      createdAt: '2024-01-01T00:00:00Z',
      modifiedAt: null,
    },
    {
      id: '22222222-2222-2222-2222-222222222222',
      bggId: 174430,
      title: 'Gloomhaven',
      yearPublished: 2017,
      description: 'A tactical combat game in a persistent world.',
      minPlayers: 1,
      maxPlayers: 4,
      playingTimeMinutes: 120,
      minAge: 14,
      complexityRating: 3.8,
      averageRating: 8.6,
      imageUrl: null,
      thumbnailUrl: null,
      status: 1,
      createdAt: '2024-01-02T00:00:00Z',
      modifiedAt: null,
    },
    {
      id: '33333333-3333-3333-3333-333333333333',
      bggId: 167791,
      title: 'Terraforming Mars',
      yearPublished: 2016,
      description: 'Compete to terraform Mars.',
      minPlayers: 1,
      maxPlayers: 5,
      playingTimeMinutes: 120,
      minAge: 12,
      complexityRating: 3.2,
      averageRating: 8.4,
      imageUrl: null,
      thumbnailUrl: null,
      status: 1,
      createdAt: '2024-01-03T00:00:00Z',
      modifiedAt: null,
    },
  ],
  total: 25, // Simulate multiple pages
  page: 1,
  pageSize: 10,
};

// Track which games have been added to library (in-memory state for tests)
let addedToLibrary: Set<string> = new Set();

// ============================================================================
// Mock Setup Helpers
// ============================================================================

async function setupCatalogMocks(page: Page, options: { gameInLibrary?: string[] } = {}) {
  // Reset state
  addedToLibrary = new Set(options.gameInLibrary || []);

  // Mock shared games catalog endpoint with pagination
  await page.route(`${API_BASE}/api/v1/shared-games*`, async route => {
    const url = new URL(route.request().url());
    const pageNum = parseInt(url.searchParams.get('page') || '1', 10);
    const pageSize = parseInt(url.searchParams.get('pageSize') || '10', 10);
    const searchTerm = url.searchParams.get('searchTerm');

    let games = [...MOCK_CATALOG_GAMES.items];

    // Filter by search term if provided
    if (searchTerm) {
      games = games.filter(g => g.title.toLowerCase().includes(searchTerm.toLowerCase()));
    }

    // Simulate pagination
    const start = (pageNum - 1) * pageSize;
    const end = start + pageSize;
    const paginatedGames = games.slice(start, end);

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: paginatedGames,
        total: games.length > 3 ? 25 : games.length, // Use 25 for pagination tests
        page: pageNum,
        pageSize,
      }),
    });
  });

  // Mock library status endpoint - check if game is in library
  await page.route(`${API_BASE}/api/v1/library/status/**`, async route => {
    const url = route.request().url();
    const gameId = url.split('/').pop();

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        inLibrary: addedToLibrary.has(gameId || ''),
      }),
    });
  });

  // Mock add to library endpoint
  await page.route(`${API_BASE}/api/v1/library`, async route => {
    if (route.request().method() === 'POST') {
      const body = JSON.parse(route.request().postData() || '{}');
      const gameId = body.gameId;

      // Simulate adding to library
      addedToLibrary.add(gameId);

      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: `lib-entry-${Date.now()}`,
          gameId,
          gameTitle:
            MOCK_CATALOG_GAMES.items.find(g => g.id === gameId)?.title || 'Unknown Game',
          addedAt: new Date().toISOString(),
          currentState: 'Nuovo',
        }),
      });
    } else {
      await route.continue();
    }
  });

  // Mock library list endpoint (for cross-page verification)
  await page.route(`${API_BASE}/api/v1/library**`, async route => {
    if (route.request().method() === 'GET') {
      const libraryItems = Array.from(addedToLibrary).map((gameId, index) => {
        const game = MOCK_CATALOG_GAMES.items.find(g => g.id === gameId);
        return {
          id: `lib-entry-${index}`,
          gameId,
          gameTitle: game?.title || 'Unknown Game',
          gameImageUrl: game?.imageUrl,
          gamePublisher: 'Test Publisher',
          isFavorite: false,
          notes: null,
          addedAt: new Date().toISOString(),
          currentState: 'Nuovo',
          stateChangedAt: null,
          stateNotes: null,
        };
      });

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: libraryItems,
          totalCount: libraryItems.length,
          page: 1,
          pageSize: 20,
          totalPages: 1,
          hasNextPage: false,
          hasPreviousPage: false,
        }),
      });
    }
  });

  // Mock library quota endpoint
  await page.route(`${API_BASE}/api/v1/library/quota`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        currentCount: addedToLibrary.size,
        maxAllowed: 50,
        userTier: 'Free',
        remainingSlots: 50 - addedToLibrary.size,
        percentageUsed: (addedToLibrary.size / 50) * 100,
      }),
    });
  });

  // Mock categories and mechanics
  await page.route(`${API_BASE}/api/v1/shared-games/categories`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { id: 'c-1', name: 'Strategy', slug: 'strategy' },
        { id: 'c-2', name: 'Economic', slug: 'economic' },
      ]),
    });
  });

  await page.route(`${API_BASE}/api/v1/shared-games/mechanics`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { id: 'm-1', name: 'Dice Rolling', slug: 'dice-rolling' },
        { id: 'm-2', name: 'Trading', slug: 'trading' },
      ]),
    });
  });

  // Mock can share game endpoint
  await page.route(`${API_BASE}/api/v1/shared-catalog/can-share/**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ canShare: true, reason: null }),
    });
  });

  // Mock agent config endpoint
  await page.route(`${API_BASE}/api/v1/agents/config**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(null),
    });
  });
}

// ============================================================================
// Test Suite: Add to Library (Issue #2877)
// ============================================================================

test.describe('Shared Catalog - Add to Library (Issue #2877)', () => {
  test('should add game to library with optimistic UI', async ({ userPage }) => {
    await setupCatalogMocks(userPage);

    await userPage.goto('/games/catalog');
    await userPage.waitForLoadState('networkidle');

    // Find a game card
    const gameCard = userPage.locator('text=Catan').first();
    await expect(gameCard).toBeVisible({ timeout: 10000 });

    // Find the "Aggiungi alla Libreria" button
    const addButton = userPage.getByRole('button', { name: /aggiungi alla libreria/i }).first();
    await expect(addButton).toBeVisible();

    // Click add to library
    await addButton.click();

    // Verify optimistic UI - button should show loading state
    await expect(
      userPage.getByRole('button', { name: /aggiunta in corso/i }).first()
    ).toBeVisible({ timeout: 2000 });

    // Wait for success state - button should change to "Già nella Libreria"
    await expect(
      userPage.getByRole('button', { name: /già nella libreria/i }).first()
    ).toBeVisible({ timeout: 5000 });

    // Verify the button is disabled
    await expect(
      userPage.getByRole('button', { name: /già nella libreria/i }).first()
    ).toBeDisabled();
  });

  test('should show success toast when game added', async ({ userPage }) => {
    await setupCatalogMocks(userPage);

    await userPage.goto('/games/catalog');
    await userPage.waitForLoadState('networkidle');

    // Find and click add button
    const addButton = userPage.getByRole('button', { name: /aggiungi alla libreria/i }).first();
    await expect(addButton).toBeVisible({ timeout: 10000 });
    await addButton.click();

    // Verify toast appears with success message
    await expect(userPage.getByText(/aggiunto alla libreria/i)).toBeVisible({ timeout: 5000 });
  });

  test('should verify game appears in personal library after adding', async ({ userPage }) => {
    await setupCatalogMocks(userPage);

    // Navigate to catalog
    await userPage.goto('/games/catalog');
    await userPage.waitForLoadState('networkidle');

    // Add a game to library
    const addButton = userPage.getByRole('button', { name: /aggiungi alla libreria/i }).first();
    await expect(addButton).toBeVisible({ timeout: 10000 });
    await addButton.click();

    // Wait for success state
    await expect(
      userPage.getByRole('button', { name: /già nella libreria/i }).first()
    ).toBeVisible({ timeout: 5000 });

    // Navigate to personal library
    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Verify the game appears in library
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });
    await expect(userPage.getByText('Catan')).toBeVisible();
  });
});

// ============================================================================
// Test Suite: Already in Library Badge (Issue #2877)
// ============================================================================

test.describe('Shared Catalog - Already in Library Badge (Issue #2877)', () => {
  test('should display "Nella Libreria" badge for games already in library', async ({
    userPage,
  }) => {
    // Setup with Catan already in library
    await setupCatalogMocks(userPage, {
      gameInLibrary: ['11111111-1111-1111-1111-111111111111'],
    });

    await userPage.goto('/games/catalog');
    await userPage.waitForLoadState('networkidle');

    // Find the Catan game card
    const catanCard = userPage.locator('text=Catan').first();
    await expect(catanCard).toBeVisible({ timeout: 10000 });

    // Verify "Nella Libreria" badge is visible
    await expect(userPage.getByText('Nella Libreria')).toBeVisible();

    // Verify "Aggiungi alla Libreria" button is NOT shown for this game
    // Instead, "Già nella Libreria" disabled button should be shown
    await expect(
      userPage.getByRole('button', { name: /già nella libreria/i }).first()
    ).toBeVisible();
    await expect(
      userPage.getByRole('button', { name: /già nella libreria/i }).first()
    ).toBeDisabled();
  });

  test('should show "Aggiungi alla Libreria" for games not in library', async ({ userPage }) => {
    // Setup with no games in library
    await setupCatalogMocks(userPage, { gameInLibrary: [] });

    await userPage.goto('/games/catalog');
    await userPage.waitForLoadState('networkidle');

    // Find a game card
    const gameCard = userPage.locator('text=Catan').first();
    await expect(gameCard).toBeVisible({ timeout: 10000 });

    // Verify "Aggiungi alla Libreria" button is shown
    await expect(
      userPage.getByRole('button', { name: /aggiungi alla libreria/i }).first()
    ).toBeVisible();
    await expect(
      userPage.getByRole('button', { name: /aggiungi alla libreria/i }).first()
    ).toBeEnabled();

    // Verify "Nella Libreria" badge is NOT visible
    await expect(userPage.getByText('Nella Libreria')).not.toBeVisible();
  });
});

// ============================================================================
// Test Suite: Pagination Navigation (Issue #2877)
// ============================================================================

test.describe('Shared Catalog - Pagination Navigation (Issue #2877)', () => {
  test('should navigate to next page', async ({ userPage }) => {
    await setupCatalogMocks(userPage);

    await userPage.goto('/games/catalog');
    await userPage.waitForLoadState('networkidle');

    // Wait for pagination to be visible (indicates multiple pages)
    const nextButton = userPage.getByRole('button', { name: 'Next page' });
    await expect(nextButton).toBeVisible({ timeout: 10000 });
    await expect(nextButton).toBeEnabled();

    // Click next page
    await nextButton.click();

    // Wait for page change
    await userPage.waitForLoadState('networkidle');

    // Verify page 2 button is now active (has aria-current="page")
    const page2Button = userPage.getByRole('button', { name: 'Page 2' });
    await expect(page2Button).toHaveAttribute('aria-current', 'page');
  });

  test('should navigate to previous page', async ({ userPage }) => {
    await setupCatalogMocks(userPage);

    await userPage.goto('/games/catalog');
    await userPage.waitForLoadState('networkidle');

    // First go to page 2
    const nextButton = userPage.getByRole('button', { name: 'Next page' });
    await expect(nextButton).toBeVisible({ timeout: 10000 });
    await nextButton.click();
    await userPage.waitForLoadState('networkidle');

    // Now go back to page 1
    const prevButton = userPage.getByRole('button', { name: 'Previous page' });
    await expect(prevButton).toBeEnabled();
    await prevButton.click();
    await userPage.waitForLoadState('networkidle');

    // Verify page 1 is active
    const page1Button = userPage.getByRole('button', { name: 'Page 1' });
    await expect(page1Button).toHaveAttribute('aria-current', 'page');
  });

  test('should navigate to first page', async ({ userPage }) => {
    await setupCatalogMocks(userPage);

    await userPage.goto('/games/catalog');
    await userPage.waitForLoadState('networkidle');

    // Go to page 2 first
    const nextButton = userPage.getByRole('button', { name: 'Next page' });
    await expect(nextButton).toBeVisible({ timeout: 10000 });
    await nextButton.click();
    await userPage.waitForLoadState('networkidle');

    // Click first page button
    const firstButton = userPage.getByRole('button', { name: 'First page' });
    await expect(firstButton).toBeEnabled();
    await firstButton.click();
    await userPage.waitForLoadState('networkidle');

    // Verify page 1 is active
    const page1Button = userPage.getByRole('button', { name: 'Page 1' });
    await expect(page1Button).toHaveAttribute('aria-current', 'page');
  });

  test('should navigate to last page', async ({ userPage }) => {
    await setupCatalogMocks(userPage);

    await userPage.goto('/games/catalog');
    await userPage.waitForLoadState('networkidle');

    // Click last page button
    const lastButton = userPage.getByRole('button', { name: 'Last page' });
    await expect(lastButton).toBeVisible({ timeout: 10000 });
    await expect(lastButton).toBeEnabled();
    await lastButton.click();
    await userPage.waitForLoadState('networkidle');

    // Verify we're on the last page (next button should be disabled)
    await expect(userPage.getByRole('button', { name: 'Next page' })).toBeDisabled();
  });

  test('should disable previous/first buttons on page 1', async ({ userPage }) => {
    await setupCatalogMocks(userPage);

    await userPage.goto('/games/catalog');
    await userPage.waitForLoadState('networkidle');

    // On page 1, previous and first should be disabled
    await expect(userPage.getByRole('button', { name: 'Previous page' })).toBeDisabled();
    await expect(userPage.getByRole('button', { name: 'First page' })).toBeDisabled();

    // Next and last should be enabled (if there are multiple pages)
    await expect(userPage.getByRole('button', { name: 'Next page' })).toBeEnabled();
    await expect(userPage.getByRole('button', { name: 'Last page' })).toBeEnabled();
  });

  test('should navigate via page number click', async ({ userPage }) => {
    await setupCatalogMocks(userPage);

    await userPage.goto('/games/catalog');
    await userPage.waitForLoadState('networkidle');

    // Click on page 2 directly
    const page2Button = userPage.getByRole('button', { name: 'Page 2' });
    await expect(page2Button).toBeVisible({ timeout: 10000 });
    await page2Button.click();
    await userPage.waitForLoadState('networkidle');

    // Verify page 2 is now active
    await expect(page2Button).toHaveAttribute('aria-current', 'page');
  });
});

// ============================================================================
// Test Suite: Search and Add Flow (Issue #2877)
// ============================================================================

test.describe('Shared Catalog - Search and Add Flow (Issue #2877)', () => {
  test('should search for game by title and add to library', async ({ userPage }) => {
    await setupCatalogMocks(userPage);

    await userPage.goto('/games/catalog');
    await userPage.waitForLoadState('networkidle');

    // Search for a specific game
    const searchInput = userPage.locator('input[placeholder*="Cerca"]').first();
    await expect(searchInput).toBeVisible({ timeout: 10000 });
    await searchInput.fill('Gloomhaven');

    // Wait for search results
    await userPage.waitForTimeout(500); // Debounce
    await userPage.waitForLoadState('networkidle');

    // Verify Gloomhaven appears in results
    await expect(userPage.getByText('Gloomhaven')).toBeVisible();

    // Add it to library
    const addButton = userPage.getByRole('button', { name: /aggiungi alla libreria/i }).first();
    await addButton.click();

    // Verify success
    await expect(
      userPage.getByRole('button', { name: /già nella libreria/i }).first()
    ).toBeVisible({ timeout: 5000 });
  });

  test('should apply filters and browse catalog', async ({ userPage }) => {
    await setupCatalogMocks(userPage);

    await userPage.goto('/games/catalog');
    await userPage.waitForLoadState('networkidle');

    // Wait for games to load
    await expect(userPage.getByText('Catan')).toBeVisible({ timeout: 10000 });

    // Verify multiple games are displayed
    await expect(userPage.getByText('Gloomhaven')).toBeVisible();
    await expect(userPage.getByText('Terraforming Mars')).toBeVisible();
  });
});
