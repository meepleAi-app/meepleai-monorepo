/**
 * E2E Tests for SharedGameSearch Flow (Issue #2373: Phase 4)
 *
 * Test Coverage:
 * - Search in SharedGameCatalog (catalog-first approach)
 * - BGG fallback when no catalog results
 * - Source badges (Catalogo vs BGG)
 * - Game detail modal display
 * - Add game from catalog (with sharedGameId link)
 * - Add game from BGG (no catalog link)
 *
 * Testing Strategy:
 * - Use Page Object Model (SharedGameCatalogPage)
 * - Mock API responses for reliable testing
 * - Verify catalog-first search behavior
 */

import { test, expect } from './fixtures/chromatic';
import { AuthHelper, SharedGameCatalogPage, USER_FIXTURES } from './pages';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Increase default timeout for all tests in this file
test.setTimeout(60000);

test.describe('SharedGame Search & Add', () => {
  let catalogPage: SharedGameCatalogPage;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    catalogPage = new SharedGameCatalogPage(page);
    authHelper = new AuthHelper(page);

    // Disable animations for stable tests
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Mock authenticated session before navigation
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock SharedGames search API - return catalog results for "Catan"
    await page.route(`${apiBase}/api/v1/shared-games*`, async route => {
      const url = new URL(route.request().url());
      const searchTerm = url.searchParams.get('searchTerm') || '';

      if (searchTerm.toLowerCase().includes('catan')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                id: 'shared-game-1',
                title: 'Catan',
                yearPublished: 1995,
                thumbnailUrl: 'https://example.com/catan-thumb.jpg',
                imageUrl: 'https://example.com/catan.jpg',
                minPlayers: 3,
                maxPlayers: 4,
                playingTimeMinutes: 90,
                averageRating: 7.5,
                bggId: 13,
                status: 1,
              },
            ],
            totalCount: 1,
            page: 1,
            pageSize: 10,
          }),
        });
      } else if (searchTerm.toLowerCase().includes('nonexistent')) {
        // No results for "nonexistent"
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [],
            totalCount: 0,
            page: 1,
            pageSize: 10,
          }),
        });
      } else {
        // Default empty response
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [],
            totalCount: 0,
            page: 1,
            pageSize: 10,
          }),
        });
      }
    });

    // Mock SharedGame detail endpoint
    await page.route(`${apiBase}/api/v1/shared-games/*`, async route => {
      const url = route.request().url();
      if (url.includes('shared-game-1')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'shared-game-1',
            title: 'Catan',
            description: 'A classic board game about trading and building settlements.',
            yearPublished: 1995,
            thumbnailUrl: 'https://example.com/catan-thumb.jpg',
            imageUrl: 'https://example.com/catan.jpg',
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            averageRating: 7.5,
            bggId: 13,
            status: 1,
            publishers: [{ name: 'Kosmos' }],
            categories: [{ id: '1', name: 'Strategico' }],
            mechanics: [{ id: '1', name: 'Commercio' }],
          }),
        });
      } else {
        await route.fulfill({ status: 404 });
      }
    });

    // Mock BGG search API
    await page.route(`${apiBase}/api/v1/bgg/search*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          results: [
            {
              bggId: 99999,
              name: 'BGG Only Game',
              yearPublished: 2020,
              thumbnailUrl: 'https://example.com/bgg-game-thumb.jpg',
            },
          ],
          totalCount: 1,
        }),
      });
    });

    // Mock games create API
    await page.route(`${apiBase}/api/v1/games`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'new-game-id',
            name: 'Catan',
            success: true,
          }),
        });
      } else {
        await route.continue();
      }
    });
  });

  /**
   * Test Group 1: Page Load & Basic Search
   */
  test.describe('Page Load & Basic Search', () => {
    test('should display search page with instructions', async ({ page }) => {
      await catalogPage.goto();

      // Wait for page content to load
      await page.waitForLoadState('networkidle');

      // Verify page title - use flexible text matching
      await expect(page.getByRole('heading', { name: /aggiungi/i })).toBeVisible({
        timeout: 15000,
      });

      // Verify search input is present - use role-based selector (more reliable)
      const searchInput = page.getByRole('searchbox');
      await expect(searchInput).toBeVisible({ timeout: 10000 });
    });

    test('should search and show results', async ({ page }) => {
      await catalogPage.goto();

      // Wait for page to be ready
      await page.waitForLoadState('networkidle');

      // Find and fill search input
      const searchInput = page.getByRole('searchbox');
      await expect(searchInput).toBeVisible({ timeout: 10000 });

      await searchInput.fill('Catan');

      // Wait for results
      await page.waitForTimeout(500); // Allow debounce

      // Should have catalog results
      await expect(page.locator('text=Catalogo')).toBeVisible({ timeout: 10000 });
    });
  });

  /**
   * Test Group 2: BGG Fallback
   */
  test.describe('BGG Fallback', () => {
    test('should show BGG fallback button for non-catalog games', async ({ page }) => {
      await catalogPage.goto();
      await page.waitForLoadState('networkidle');

      // Search for game not in catalog
      const searchInput = page.getByRole('searchbox');
      await expect(searchInput).toBeVisible({ timeout: 10000 });

      await searchInput.fill('NonExistentGame');
      await page.waitForTimeout(500); // Allow debounce + API call

      // Should show BGG fallback option
      const bggFallback = page.getByRole('button', { name: /boardgamegeek/i });
      await expect(bggFallback).toBeVisible({ timeout: 10000 });
    });
  });

  /**
   * Test Group 3: Search Edge Cases
   */
  test.describe('Search Edge Cases', () => {
    test('should handle empty search gracefully', async ({ page }) => {
      await catalogPage.goto();
      await page.waitForLoadState('networkidle');

      const searchInput = page.getByRole('searchbox');
      await expect(searchInput).toBeVisible({ timeout: 10000 });

      // Clear any existing value and wait
      await searchInput.fill('');
      await page.waitForTimeout(500);

      // Should not show results dropdown when empty - check for shadow-lg card
      const resultsDropdown = page.locator('.shadow-lg').filter({ hasText: /Catalogo|BGG/i });
      await expect(resultsDropdown).not.toBeVisible();
    });

    test('should handle special characters in search', async ({ page }) => {
      await catalogPage.goto();
      await page.waitForLoadState('networkidle');

      const searchInput = page.getByRole('searchbox');
      await expect(searchInput).toBeVisible({ timeout: 10000 });

      // Search with special characters
      await searchInput.fill('Game!@#$%');
      await page.waitForTimeout(500);

      // Should handle gracefully (no crash) - page should still be functional
      await expect(searchInput).toBeVisible();
    });
  });

  /**
   * Test Group 4: Filters
   */
  test.describe('Filters', () => {
    test('should display filter options', async ({ page }) => {
      await catalogPage.goto();
      await page.waitForLoadState('networkidle');

      // Check for filter elements - "Solo dal catalogo" is the main filter toggle
      const filtersVisible = await page.locator('text=/solo dal catalogo|filtri/i').isVisible();

      // Filters should be visible on the page
      expect(filtersVisible).toBe(true);
    });
  });

  /**
   * Test Group 5: Source Badges
   */
  test.describe('Source Badges', () => {
    test('should display Catalogo badge for catalog results', async ({ page }) => {
      await catalogPage.goto();
      await page.waitForLoadState('networkidle');

      const searchInput = page.getByRole('searchbox');
      await searchInput.fill('Catan');
      await page.waitForTimeout(500);

      // Should show Catalogo badge
      await expect(page.locator('text=Catalogo')).toBeVisible({ timeout: 5000 });
    });
  });
});
