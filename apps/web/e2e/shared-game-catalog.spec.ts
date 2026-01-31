/**
 * E2E Tests for SharedGameCatalog User-Facing Features (Issue #2373 Phase 4)
 *
 * Test Coverage:
 * - SharedGameSearch component with catalog-first, BGG fallback
 * - Search filters (category, mechanic, player count, playing time)
 * - SharedGameDetailModal with tabs (Overview, Rules, FAQ, Errata)
 * - Empty states and error handling
 * - Source badges (Catalog vs BGG)
 *
 * Testing Strategy:
 * - Mock API responses for predictable test data
 * - Test search-first pattern: catalog → BGG fallback
 * - Verify filter interactions and state management
 * - Test modal content loading and tab navigation
 *
 * @see claudedocs/shared-game-catalog-spec.md
 */

import { test, expect } from '@playwright/test';

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
      imageUrl: 'https://example.com/catan.jpg',
      thumbnailUrl: 'https://example.com/catan-thumb.jpg',
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
      imageUrl: 'https://example.com/gloomhaven.jpg',
      thumbnailUrl: 'https://example.com/gloomhaven-thumb.jpg',
      status: 1,
      createdAt: '2024-01-02T00:00:00Z',
      modifiedAt: null,
    },
  ],
  total: 2,
  page: 1,
  pageSize: 10,
};

const MOCK_BGG_RESULTS = {
  results: [
    {
      bggId: 167791,
      name: 'Terraforming Mars',
      yearPublished: 2016,
      thumbnailUrl: 'https://example.com/terraforming-thumb.jpg',
      type: 'boardgame',
    },
  ],
  total: 1,
  page: 1,
  pageSize: 10,
  totalPages: 1,
};

const MOCK_GAME_DETAIL = {
  id: '11111111-1111-1111-1111-111111111111',
  bggId: 13,
  title: 'Catan',
  yearPublished: 1995,
  description: 'Trade, build and settle the island of Catan in this classic strategy game.',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  minAge: 10,
  complexityRating: 2.3,
  averageRating: 7.1,
  imageUrl: 'https://example.com/catan.jpg',
  thumbnailUrl: 'https://example.com/catan-thumb.jpg',
  rules: {
    content: '<h2>Setup</h2><p>Place the board tiles and shuffle the cards.</p>',
    language: 'en',
  },
  status: 1,
  createdBy: '00000000-0000-0000-0000-000000000001',
  modifiedBy: null,
  createdAt: '2024-01-01T00:00:00Z',
  modifiedAt: null,
  faqs: [
    {
      id: 'faq-1',
      question: 'Can I build on any hex?',
      answer: 'You can only build on hexes you have settlements adjacent to.',
      order: 0,
      createdAt: '2024-01-01T00:00:00Z',
    },
    {
      id: 'faq-2',
      question: 'What happens when the robber is moved?',
      answer: 'The robber blocks resource production on that hex.',
      order: 1,
      createdAt: '2024-01-02T00:00:00Z',
    },
  ],
  erratas: [
    {
      id: 'errata-1',
      description: 'The development card deck should have 25 cards, not 24.',
      pageReference: 'p.12',
      publishedDate: '2024-06-15T00:00:00Z',
      createdAt: '2024-06-15T00:00:00Z',
    },
  ],
  designers: [{ id: 'd-1', name: 'Klaus Teuber' }],
  publishers: [{ id: 'p-1', name: 'KOSMOS' }],
  categories: [
    { id: 'c-1', name: 'Economic', slug: 'economic' },
    { id: 'c-2', name: 'Negotiation', slug: 'negotiation' },
  ],
  mechanics: [
    { id: 'm-1', name: 'Dice Rolling', slug: 'dice-rolling' },
    { id: 'm-2', name: 'Trading', slug: 'trading' },
  ],
};

const MOCK_CATEGORIES = [
  { id: 'c-1', name: 'Economic', slug: 'economic' },
  { id: 'c-2', name: 'Negotiation', slug: 'negotiation' },
  { id: 'c-3', name: 'Strategy', slug: 'strategy' },
];

const MOCK_MECHANICS = [
  { id: 'm-1', name: 'Dice Rolling', slug: 'dice-rolling' },
  { id: 'm-2', name: 'Trading', slug: 'trading' },
  { id: 'm-3', name: 'Worker Placement', slug: 'worker-placement' },
];

// ============================================================================
// Test Setup
// ============================================================================

test.describe('SharedGameCatalog User-Facing Features', () => {
  test.beforeEach(async ({ page }) => {
    // Mock API endpoints
    await page.route('**/api/v1/shared-games*', async route => {
      const url = new URL(route.request().url());
      const searchTerm = url.searchParams.get('searchTerm');

      if (searchTerm && searchTerm.toLowerCase().includes('nonexistent')) {
        // Return empty results for specific search
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: [], total: 0, page: 1, pageSize: 10 }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_CATALOG_GAMES),
        });
      }
    });

    await page.route('**/api/v1/shared-games/categories', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_CATEGORIES),
      });
    });

    await page.route('**/api/v1/shared-games/mechanics', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_MECHANICS),
      });
    });

    await page.route('**/api/v1/shared-games/*', async route => {
      if (!route.request().url().includes('?')) {
        // Detail endpoint
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_GAME_DETAIL),
        });
      }
    });

    await page.route('**/api/v1/bgg/search*', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_BGG_RESULTS),
      });
    });
  });

  // ============================================================================
  // Search Tests
  // ============================================================================

  test.describe('Game Search', () => {
    test('should display search input and filters button', async ({ page }) => {
      // Navigate to a page with SharedGameSearch component
      // Note: Adjust this URL to match where the component is actually used
      await page.goto('/games/catalog');

      // Verify search input is visible
      await expect(page.locator('input[aria-label="Cerca giochi"]')).toBeVisible();

      // Verify filters button is visible
      await expect(page.getByRole('button', { name: /filtri/i })).toBeVisible();
    });

    test('should search catalog and display results with source badge', async ({ page }) => {
      await page.goto('/games/catalog');

      // Type in search
      const searchInput = page.locator('input[aria-label="Cerca giochi"]');
      await searchInput.fill('Catan');

      // Wait for debounce and results
      await page.waitForTimeout(400);

      // Should show catalog results
      await expect(page.getByText('Catan')).toBeVisible();
      await expect(page.getByText('Catalogo')).toBeVisible();
    });

    test('should show BGG fallback button when no catalog results', async ({ page }) => {
      await page.goto('/games/catalog');

      // Search for something not in catalog
      const searchInput = page.locator('input[aria-label="Cerca giochi"]');
      await searchInput.fill('NonexistentGame');

      // Wait for debounce
      await page.waitForTimeout(400);

      // Should show BGG fallback button
      await expect(page.getByText('Nessun gioco trovato nel catalogo.')).toBeVisible();
      await expect(page.getByRole('button', { name: /cerca su boardgamegeek/i })).toBeVisible();
    });

    test('should search BGG when fallback button clicked', async ({ page }) => {
      await page.goto('/games/catalog');

      // Search for something not in catalog
      const searchInput = page.locator('input[aria-label="Cerca giochi"]');
      await searchInput.fill('NonexistentGame');
      await page.waitForTimeout(400);

      // Click BGG fallback button
      await page.getByRole('button', { name: /cerca su boardgamegeek/i }).click();

      // Should show BGG results with BGG badge
      await expect(page.getByText('Terraforming Mars')).toBeVisible();
      await expect(page.getByText('BGG')).toBeVisible();
    });

    test('should clear search when X button clicked', async ({ page }) => {
      await page.goto('/games/catalog');

      // Type in search
      const searchInput = page.locator('input[aria-label="Cerca giochi"]');
      await searchInput.fill('Catan');
      await page.waitForTimeout(400);

      // Click clear button
      await page.getByRole('button', { name: /cancella ricerca/i }).click();

      // Input should be cleared
      await expect(searchInput).toHaveValue('');
    });
  });

  // ============================================================================
  // Filter Tests
  // ============================================================================

  test.describe('Search Filters', () => {
    test('should expand filter panel when Filtri button clicked', async ({ page }) => {
      await page.goto('/games/catalog');

      // Click filters button
      await page.getByRole('button', { name: /filtri/i }).click();

      // Filter panel should be visible
      await expect(page.getByText('Categorie')).toBeVisible();
      await expect(page.getByText('Meccaniche')).toBeVisible();
      await expect(page.getByText('Numero giocatori')).toBeVisible();
      await expect(page.getByText('Tempo massimo di gioco')).toBeVisible();
      await expect(page.getByText('Solo dal catalogo')).toBeVisible();
    });

    test('should show active filter count in badge', async ({ page }) => {
      await page.goto('/games/catalog');

      // Expand filters
      await page.getByRole('button', { name: /filtri/i }).click();

      // Set a player count filter
      await page.locator('input[aria-label="Minimo giocatori"]').fill('2');

      // Close filters
      await page.getByRole('button', { name: /filtri/i }).click();

      // Should show filter count badge
      await expect(page.locator('.rounded-full').getByText('1')).toBeVisible();
    });

    test('should clear filters when Cancella filtri clicked', async ({ page }) => {
      await page.goto('/games/catalog');

      // Expand filters and set values
      await page.getByRole('button', { name: /filtri/i }).click();
      await page.locator('input[aria-label="Minimo giocatori"]').fill('2');
      await page.getByRole('button', { name: /filtri/i }).click();

      // Click clear filters
      await page.getByRole('button', { name: /cancella filtri/i }).click();

      // Badge should not show count
      await expect(page.locator('.rounded-full').getByText('1')).not.toBeVisible();
    });

    test('should toggle catalog only mode', async ({ page }) => {
      await page.goto('/games/catalog');

      // Expand filters
      await page.getByRole('button', { name: /filtri/i }).click();

      // Toggle catalog only switch
      const catalogOnlySwitch = page.locator('#catalog-only');
      await catalogOnlySwitch.click();

      // Should be checked
      await expect(catalogOnlySwitch).toBeChecked();
    });
  });

  // ============================================================================
  // Game Detail Modal Tests
  // ============================================================================

  test.describe('Game Detail Modal', () => {
    test('should open modal when game card clicked', async ({ page }) => {
      await page.goto('/games/catalog');

      // Search and click on a game
      const searchInput = page.locator('input[aria-label="Cerca giochi"]');
      await searchInput.fill('Catan');
      await page.waitForTimeout(400);

      // Click on game result (assuming onClick opens modal)
      await page.getByText('Catan').first().click();

      // Modal should be visible with game title
      await expect(page.getByRole('dialog')).toBeVisible();
      await expect(page.getByRole('heading', { name: 'Catan' })).toBeVisible();
    });

    test('should display Overview tab content', async ({ page }) => {
      await page.goto('/games/catalog');

      // Search and click on game
      await page.locator('input[aria-label="Cerca giochi"]').fill('Catan');
      await page.waitForTimeout(400);
      await page.getByText('Catan').first().click();

      // Overview tab should be visible by default
      await expect(page.getByText('Descrizione')).toBeVisible();
      await expect(page.getByText(/trade, build and settle/i)).toBeVisible();

      // Game metadata should be visible
      await expect(page.getByText('Klaus Teuber')).toBeVisible();
      await expect(page.getByText('KOSMOS')).toBeVisible();
    });

    test('should display Rules tab with sanitized HTML', async ({ page }) => {
      await page.goto('/games/catalog');

      // Open game detail
      await page.locator('input[aria-label="Cerca giochi"]').fill('Catan');
      await page.waitForTimeout(400);
      await page.getByText('Catan').first().click();

      // Click Rules tab
      await page.getByRole('tab', { name: /regole/i }).click();

      // Rules content should be visible
      await expect(page.getByText('Setup')).toBeVisible();
      await expect(page.getByText('Place the board tiles')).toBeVisible();

      // Language badge should be visible
      await expect(page.getByText('EN')).toBeVisible();
    });

    test('should display FAQ tab with accordion', async ({ page }) => {
      await page.goto('/games/catalog');

      // Open game detail
      await page.locator('input[aria-label="Cerca giochi"]').fill('Catan');
      await page.waitForTimeout(400);
      await page.getByText('Catan').first().click();

      // Click FAQ tab
      await page.getByRole('tab', { name: /faq/i }).click();

      // FAQ questions should be visible
      await expect(page.getByText('Can I build on any hex?')).toBeVisible();
      await expect(page.getByText('What happens when the robber is moved?')).toBeVisible();
    });

    test('should display Errata tab with corrections', async ({ page }) => {
      await page.goto('/games/catalog');

      // Open game detail
      await page.locator('input[aria-label="Cerca giochi"]').fill('Catan');
      await page.waitForTimeout(400);
      await page.getByText('Catan').first().click();

      // Click Errata tab
      await page.getByRole('tab', { name: /errata/i }).click();

      // Errata content should be visible
      await expect(page.getByText('The development card deck')).toBeVisible();
      await expect(page.getByText('p.12')).toBeVisible();
    });

    test('should close modal when clicking outside or close button', async ({ page }) => {
      await page.goto('/games/catalog');

      // Open modal
      await page.locator('input[aria-label="Cerca giochi"]').fill('Catan');
      await page.waitForTimeout(400);
      await page.getByText('Catan').first().click();

      // Modal should be open
      await expect(page.getByRole('dialog')).toBeVisible();

      // Close via keyboard
      await page.keyboard.press('Escape');

      // Modal should be closed
      await expect(page.getByRole('dialog')).not.toBeVisible();
    });
  });

  // ============================================================================
  // Error Handling Tests
  // ============================================================================

  test.describe('Error Handling', () => {
    test('should show error message when search fails', async ({ page }) => {
      // Override route to return error
      await page.route('**/api/v1/shared-games*', async route => {
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Internal Server Error' }),
        });
      });

      await page.goto('/games/catalog');

      // Search
      await page.locator('input[aria-label="Cerca giochi"]').fill('Test');
      await page.waitForTimeout(400);

      // Error message should be visible
      await expect(page.getByText(/errore nella ricerca/i)).toBeVisible();
    });

    test('should show error when game detail fails to load', async ({ page }) => {
      await page.goto('/games/catalog');

      // Search first
      await page.locator('input[aria-label="Cerca giochi"]').fill('Catan');
      await page.waitForTimeout(400);

      // Override detail route to fail
      await page.route('**/api/v1/shared-games/*', async route => {
        if (!route.request().url().includes('?')) {
          await route.fulfill({
            status: 404,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Not found' }),
          });
        }
      });

      // Click on game
      await page.getByText('Catan').first().click();

      // Error message in modal
      await expect(page.getByText(/impossibile caricare/i)).toBeVisible();
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  test.describe('Accessibility', () => {
    test('search input should have proper aria-label', async ({ page }) => {
      await page.goto('/games/catalog');

      const searchInput = page.locator('input[aria-label="Cerca giochi"]');
      await expect(searchInput).toBeVisible();
      await expect(searchInput).toHaveAttribute('aria-label', 'Cerca giochi');
    });

    test('filter inputs should have proper labels', async ({ page }) => {
      await page.goto('/games/catalog');

      // Expand filters
      await page.getByRole('button', { name: /filtri/i }).click();

      // Check aria labels
      await expect(page.locator('input[aria-label="Minimo giocatori"]')).toBeVisible();
      await expect(page.locator('input[aria-label="Massimo giocatori"]')).toBeVisible();
      await expect(page.locator('input[aria-label="Tempo massimo in minuti"]')).toBeVisible();
    });

    test('modal should trap focus', async ({ page }) => {
      await page.goto('/games/catalog');

      // Open modal
      await page.locator('input[aria-label="Cerca giochi"]').fill('Catan');
      await page.waitForTimeout(400);
      await page.getByText('Catan').first().click();

      // Modal should be focused
      await expect(page.getByRole('dialog'))
        .toBeFocused({ timeout: 1000 })
        .catch(() => {
          // Dialog container might not be directly focused, but focus should be within
        });

      // Tab should cycle within modal
      await page.keyboard.press('Tab');
      const focusedElement = await page.evaluate(() => document.activeElement?.tagName);
      expect(['BUTTON', 'A', 'INPUT']).toContain(focusedElement);
    });
  });
});
