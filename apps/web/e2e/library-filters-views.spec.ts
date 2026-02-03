/**
 * E2E Tests for Library Filters and Views (Issue #2870, #2866, #2867)
 *
 * Tests complete user workflows for:
 * - Filter by state (Nuovo, InPrestito, Wishlist)
 * - Grid/List view toggle
 * - Visual regression tests
 */

import { Page } from '@playwright/test';

import { test, expect } from './test';

// Issue #841: Make API_BASE configurable via environment variables
const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Sample library games for testing with different states
const createMockLibraryGamesWithStates = () => {
  return [
    {
      id: 'lib-entry-1',
      gameId: 'game-1',
      gameTitle: 'Catan',
      gameImageUrl: null,
      gamePublisher: 'Kosmos',
      isFavorite: true,
      notes: null,
      addedAt: new Date(Date.now() - 86400000).toISOString(),
      currentState: 'Nuovo',
      stateChangedAt: null,
      stateNotes: null,
    },
    {
      id: 'lib-entry-2',
      gameId: 'game-2',
      gameTitle: 'Ticket to Ride',
      gameImageUrl: null,
      gamePublisher: 'Days of Wonder',
      isFavorite: false,
      notes: 'Great gateway game',
      addedAt: new Date(Date.now() - 2 * 86400000).toISOString(),
      currentState: 'InPrestito',
      stateChangedAt: new Date().toISOString(),
      stateNotes: 'Lent to Marco',
    },
    {
      id: 'lib-entry-3',
      gameId: 'game-3',
      gameTitle: 'Wingspan',
      gameImageUrl: null,
      gamePublisher: 'Stonemaier Games',
      isFavorite: true,
      notes: null,
      addedAt: new Date(Date.now() - 3 * 86400000).toISOString(),
      currentState: 'Wishlist',
      stateChangedAt: null,
      stateNotes: null,
    },
    {
      id: 'lib-entry-4',
      gameId: 'game-4',
      gameTitle: 'Azul',
      gameImageUrl: null,
      gamePublisher: 'Plan B Games',
      isFavorite: false,
      notes: null,
      addedAt: new Date(Date.now() - 4 * 86400000).toISOString(),
      currentState: 'Owned',
      stateChangedAt: null,
      stateNotes: null,
    },
    {
      id: 'lib-entry-5',
      gameId: 'game-5',
      gameTitle: 'Pandemic',
      gameImageUrl: null,
      gamePublisher: 'Z-Man Games',
      isFavorite: false,
      notes: null,
      addedAt: new Date(Date.now() - 5 * 86400000).toISOString(),
      currentState: 'Nuovo',
      stateChangedAt: null,
      stateNotes: null,
    },
  ];
};

// Setup mock routes for library with state filtering support
async function setupLibraryFilterMocks(page: Page) {
  const mockGames = createMockLibraryGamesWithStates();

  // Mock library endpoint with state filter support
  await page.route(`${API_BASE}/api/v1/library**`, async route => {
    const method = route.request().method();
    const url = new URL(route.request().url());

    if (method === 'GET') {
      let filteredGames = [...mockGames];

      // Apply state filter
      const stateFilters = url.searchParams.getAll('stateFilter');
      if (stateFilters.length > 0) {
        filteredGames = filteredGames.filter(g => stateFilters.includes(g.currentState));
      }

      // Apply favorites filter
      const favoritesOnly = url.searchParams.get('favoritesOnly');
      if (favoritesOnly === 'true') {
        filteredGames = filteredGames.filter(g => g.isFavorite);
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: filteredGames,
          totalCount: filteredGames.length,
          page: 1,
          pageSize: 20,
          totalPages: 1,
          hasNextPage: false,
          hasPreviousPage: false,
        }),
      });
    } else if (method === 'PUT' || method === 'PATCH') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    } else if (method === 'DELETE') {
      await route.fulfill({ status: 204 });
    } else {
      await route.continue();
    }
  });

  // Mock library quota endpoint
  await page.route(`${API_BASE}/api/v1/library/quota`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        currentCount: mockGames.length,
        maxAllowed: 50,
        userTier: 'Free',
        remainingSlots: 50 - mockGames.length,
        percentageUsed: (mockGames.length / 50) * 100,
      }),
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

  // Mock can share game endpoint
  await page.route(`${API_BASE}/api/v1/shared-catalog/can-share/**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ canShare: true, reason: null }),
    });
  });

  return mockGames;
}

test.describe('Library State Filters (Issue #2866, #2870)', () => {
  test('should filter by Nuovo state', async ({ userPage }) => {
    const mockGames = await setupLibraryFilterMocks(userPage);
    const nuovoGames = mockGames.filter(g => g.currentState === 'Nuovo');

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Verify initial count shows all games
    const initialCount = await userPage.locator('[data-testid="game-card"]').count();
    expect(initialCount).toBe(mockGames.length);

    // Click on "Nuovo" filter chip
    const nuovoChip = userPage.locator('[data-testid="filter-chip-nuovo"]');
    await expect(nuovoChip).toBeVisible();
    await nuovoChip.click();

    // Wait for filter to apply
    await userPage.waitForLoadState('networkidle');

    // Verify only Nuovo games are shown
    const filteredCount = await userPage.locator('[data-testid="game-card"]').count();
    expect(filteredCount).toBe(nuovoGames.length);

    // Verify chip is active (has active color class)
    await expect(nuovoChip).toHaveAttribute('aria-pressed', 'true');
  });

  test('should filter by InPrestito state', async ({ userPage }) => {
    const mockGames = await setupLibraryFilterMocks(userPage);
    const inPrestitoGames = mockGames.filter(g => g.currentState === 'InPrestito');

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Click on "In Prestito" filter chip
    const inPrestitoChip = userPage.locator('[data-testid="filter-chip-inprestito"]');
    await expect(inPrestitoChip).toBeVisible();
    await inPrestitoChip.click();

    // Wait for filter to apply
    await userPage.waitForLoadState('networkidle');

    // Verify only InPrestito games are shown
    const filteredCount = await userPage.locator('[data-testid="game-card"]').count();
    expect(filteredCount).toBe(inPrestitoGames.length);

    // Verify the game card has state border (red for InPrestito)
    const firstCard = userPage.locator('[data-testid="game-card"]').first();
    await expect(firstCard).toHaveAttribute('data-game-state', 'InPrestito');
  });

  test('should filter by Wishlist state', async ({ userPage }) => {
    const mockGames = await setupLibraryFilterMocks(userPage);
    const wishlistGames = mockGames.filter(g => g.currentState === 'Wishlist');

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Click on "Wishlist" filter chip
    const wishlistChip = userPage.locator('[data-testid="filter-chip-wishlist"]');
    await expect(wishlistChip).toBeVisible();
    await wishlistChip.click();

    // Wait for filter to apply
    await userPage.waitForLoadState('networkidle');

    // Verify only Wishlist games are shown
    const filteredCount = await userPage.locator('[data-testid="game-card"]').count();
    expect(filteredCount).toBe(wishlistGames.length);
  });

  test('should filter by Favorites', async ({ userPage }) => {
    const mockGames = await setupLibraryFilterMocks(userPage);
    const favoriteGames = mockGames.filter(g => g.isFavorite);

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Click on "Preferiti" filter chip
    const favoritesChip = userPage.locator('[data-testid="filter-chip-favorites"]');
    await expect(favoritesChip).toBeVisible();
    await favoritesChip.click();

    // Wait for filter to apply
    await userPage.waitForLoadState('networkidle');

    // Verify only favorite games are shown
    const filteredCount = await userPage.locator('[data-testid="game-card"]').count();
    expect(filteredCount).toBe(favoriteGames.length);
  });

  test('should clear state filter when clicking "Tutti"', async ({ userPage }) => {
    const mockGames = await setupLibraryFilterMocks(userPage);

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // First apply a state filter
    const nuovoChip = userPage.locator('[data-testid="filter-chip-nuovo"]');
    await nuovoChip.click();
    await userPage.waitForLoadState('networkidle');

    // Then click "Tutti" to clear filters
    const tuttiChip = userPage.locator('[data-testid="filter-chip-all"]');
    await expect(tuttiChip).toBeVisible();
    await tuttiChip.click();

    // Wait for filter to clear
    await userPage.waitForLoadState('networkidle');

    // Verify all games are shown
    const count = await userPage.locator('[data-testid="game-card"]').count();
    expect(count).toBe(mockGames.length);

    // Verify "Tutti" chip is active
    await expect(tuttiChip).toHaveAttribute('aria-pressed', 'true');
  });

  test('should show filter count badges', async ({ userPage }) => {
    await setupLibraryFilterMocks(userPage);

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Verify filter chips show counts
    // "Tutti" should show total count (5)
    const tuttiChip = userPage.locator('[data-testid="filter-chip-all"]');
    await expect(tuttiChip).toContainText('5');

    // "Preferiti" should show favorites count (2)
    const favoritesChip = userPage.locator('[data-testid="filter-chip-favorites"]');
    await expect(favoritesChip).toContainText('2');

    // "Nuovo" should show count (2)
    const nuovoChip = userPage.locator('[data-testid="filter-chip-nuovo"]');
    await expect(nuovoChip).toContainText('2');

    // "In Prestito" should show count (1)
    const inPrestitoChip = userPage.locator('[data-testid="filter-chip-inprestito"]');
    await expect(inPrestitoChip).toContainText('1');
  });
});

test.describe('Library View Mode Toggle (Issue #2866, #2867, #2870)', () => {
  test('should toggle between grid and list view', async ({ userPage }) => {
    await setupLibraryFilterMocks(userPage);

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Verify initial view is grid (default)
    const firstCard = userPage.locator('[data-testid="game-card"]').first();
    await expect(firstCard).toHaveAttribute('data-view-mode', 'grid');

    // Click list view toggle button
    const listViewButton = userPage.locator('button[aria-label="Vista a lista"]');
    await expect(listViewButton).toBeVisible();
    await listViewButton.click();

    // Verify cards are now in list view
    await expect(firstCard).toHaveAttribute('data-view-mode', 'list');

    // Verify list view button is now pressed
    await expect(listViewButton).toHaveAttribute('aria-pressed', 'true');

    // Click grid view toggle button
    const gridViewButton = userPage.locator('button[aria-label="Vista a griglia"]');
    await expect(gridViewButton).toBeVisible();
    await gridViewButton.click();

    // Verify cards are back in grid view
    await expect(firstCard).toHaveAttribute('data-view-mode', 'grid');

    // Verify grid view button is now pressed
    await expect(gridViewButton).toHaveAttribute('aria-pressed', 'true');
  });

  test('should show state badge in grid view', async ({ userPage }) => {
    await setupLibraryFilterMocks(userPage);

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Click on "Nuovo" filter to get Nuovo games
    const nuovoChip = userPage.locator('[data-testid="filter-chip-nuovo"]');
    await nuovoChip.click();
    await userPage.waitForLoadState('networkidle');

    // Verify game card shows "Nuovo" state badge
    const firstCard = userPage.locator('[data-testid="game-card"]').first();
    const stateBadge = firstCard.locator('text=Nuovo').first();
    await expect(stateBadge).toBeVisible();
  });

  test('should show state badge in list view', async ({ userPage }) => {
    await setupLibraryFilterMocks(userPage);

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Switch to list view
    const listViewButton = userPage.locator('button[aria-label="Vista a lista"]');
    await listViewButton.click();

    // Click on "In Prestito" filter
    const inPrestitoChip = userPage.locator('[data-testid="filter-chip-inprestito"]');
    await inPrestitoChip.click();
    await userPage.waitForLoadState('networkidle');

    // Verify game card shows "In Prestito" state badge
    const firstCard = userPage.locator('[data-testid="game-card"]').first();
    const stateBadge = firstCard.locator('text=In Prestito');
    await expect(stateBadge).toBeVisible();
  });

  test('should have state-colored borders on cards', async ({ userPage }) => {
    await setupLibraryFilterMocks(userPage);

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Filter for "Nuovo" games - should have green border
    const nuovoChip = userPage.locator('[data-testid="filter-chip-nuovo"]');
    await nuovoChip.click();
    await userPage.waitForLoadState('networkidle');

    const nuovoCard = userPage.locator('[data-testid="game-card"][data-game-state="Nuovo"]').first();
    await expect(nuovoCard).toBeVisible();
    // Verify it has the border-l-green-500 class (state-coded border)
    await expect(nuovoCard).toHaveClass(/border-l-green-500/);

    // Filter for "InPrestito" games - should have red border
    const inPrestitoChip = userPage.locator('[data-testid="filter-chip-inprestito"]');
    await inPrestitoChip.click();
    await userPage.waitForLoadState('networkidle');

    const inPrestitoCard = userPage
      .locator('[data-testid="game-card"][data-game-state="InPrestito"]')
      .first();
    await expect(inPrestitoCard).toBeVisible();
    await expect(inPrestitoCard).toHaveClass(/border-l-red-500/);
  });
});

test.describe('Library Quick Actions Menu (Issue #2867, #2870)', () => {
  test('should open quick actions dropdown in grid view', async ({ userPage }) => {
    await setupLibraryFilterMocks(userPage);

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Find and click the more actions button (3 dots)
    const firstCard = userPage.locator('[data-testid="game-card"]').first();
    const moreButton = firstCard.locator('button').filter({ has: userPage.locator('svg.lucide-more-vertical') });
    await expect(moreButton).toBeVisible();
    await moreButton.click();

    // Verify dropdown menu is visible with expected options
    await expect(userPage.locator('text=Modifica Note')).toBeVisible();
    await expect(userPage.locator('text=Configura Agente')).toBeVisible();
    await expect(userPage.locator('text=Carica PDF')).toBeVisible();
    await expect(userPage.locator('text=Rimuovi')).toBeVisible();
  });

  test('should open quick actions dropdown in list view', async ({ userPage }) => {
    await setupLibraryFilterMocks(userPage);

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Switch to list view
    const listViewButton = userPage.locator('button[aria-label="Vista a lista"]');
    await listViewButton.click();

    // Find and click the more actions button (3 dots)
    const firstCard = userPage.locator('[data-testid="game-card"]').first();
    const moreButton = firstCard.locator('button').filter({ has: userPage.locator('svg.lucide-more-vertical') });
    await expect(moreButton).toBeVisible();
    await moreButton.click();

    // Verify dropdown menu is visible
    await expect(userPage.locator('text=Modifica Note')).toBeVisible();
    await expect(userPage.locator('text=Rimuovi')).toBeVisible();
  });
});

test.describe('Library Visual Regression (Issue #2870)', () => {
  test('should match grid view snapshot', async ({ userPage }) => {
    await setupLibraryFilterMocks(userPage);

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Wait for animations to complete
    await userPage.waitForTimeout(500);

    // Take screenshot of library in grid view
    await expect(userPage.locator('.container')).toHaveScreenshot('library-grid-view.png', {
      maxDiffPixels: 100,
    });
  });

  test('should match list view snapshot', async ({ userPage }) => {
    await setupLibraryFilterMocks(userPage);

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Switch to list view
    const listViewButton = userPage.locator('button[aria-label="Vista a lista"]');
    await listViewButton.click();

    // Wait for animations to complete
    await userPage.waitForTimeout(500);

    // Take screenshot of library in list view
    await expect(userPage.locator('.container')).toHaveScreenshot('library-list-view.png', {
      maxDiffPixels: 100,
    });
  });

  test('should match filter chips snapshot', async ({ userPage }) => {
    await setupLibraryFilterMocks(userPage);

    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Take screenshot of filter chips area
    const filterChipsContainer = userPage.locator('.flex.flex-wrap.gap-2');
    await expect(filterChipsContainer).toHaveScreenshot('library-filter-chips.png', {
      maxDiffPixels: 50,
    });
  });
});
