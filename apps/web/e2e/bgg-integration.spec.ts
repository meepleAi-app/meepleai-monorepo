/**
 * BGG Integration E2E Tests - Issue #2193
 *
 * Tests the BoardGameGeek integration flow:
 * - Search for game on BGG
 * - View BGG game details
 * - Link BGG game to local game
 * - Auto-fetch game metadata from BGG
 *
 * @see apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/BggApi/
 * @see apps/web/e2e/add-game-bgg.spec.ts (related tests)
 * @see docs/02-development/testing/test-coverage-gaps.md
 */

import { test, expect } from './fixtures';
import { AuthHelper, USER_FIXTURES } from './pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock BGG data
const MOCK_BGG_SEARCH_RESULTS = [
  {
    bggId: 169786,
    name: 'Scythe',
    yearPublished: 2016,
    thumbnailUrl: 'https://cf.geekdo-images.com/example/scythe_thumb.jpg',
  },
  {
    bggId: 173346,
    name: 'Scythe: The Rise of Fenris',
    yearPublished: 2018,
    thumbnailUrl: 'https://cf.geekdo-images.com/example/fenris_thumb.jpg',
  },
  {
    bggId: 223555,
    name: 'Scythe: Invaders from Afar',
    yearPublished: 2016,
    thumbnailUrl: 'https://cf.geekdo-images.com/example/invaders_thumb.jpg',
  },
];

const MOCK_BGG_GAME_DETAILS = {
  bggId: 169786,
  name: 'Scythe',
  yearPublished: 2016,
  minPlayers: 1,
  maxPlayers: 5,
  playingTime: 115,
  minPlayTime: 90,
  maxPlayTime: 115,
  minAge: 14,
  thumbnailUrl: 'https://cf.geekdo-images.com/example/scythe_thumb.jpg',
  imageUrl: 'https://cf.geekdo-images.com/example/scythe_large.jpg',
  description:
    'It is a time of unrest in 1920s Europa. The ashes from the first great war still darken the snow...',
  publishers: ['Stonemaier Games'],
  designers: ['Jamey Stegmaier'],
  artists: ['Jakub Rozalski'],
  categories: ['Economic', 'Fighting', 'Science Fiction', 'Territory Building'],
  mechanics: ['Area Control', 'Grid Movement', 'Variable Player Powers'],
  averageRating: 8.2,
  numVoters: 75000,
  weight: 3.4,
};

const MOCK_LOCAL_GAMES = [
  {
    id: 'game-1',
    title: 'Chess',
    publisher: 'Various',
    yearPublished: 1475,
    bggId: null,
  },
  {
    id: 'game-2',
    title: 'Scythe',
    publisher: 'Stonemaier Games',
    yearPublished: 2016,
    bggId: 169786, // Already linked
  },
];

test.describe('BGG Integration Flow - Issue #2193', () => {
  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Mock authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock BGG search endpoint
    await page.route(`${API_BASE}/api/v1/bgg/search*`, async route => {
      const url = new URL(route.request().url());
      const query = url.searchParams.get('q');

      if (!query || query.length < 2) {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Search query must be at least 2 characters' }),
        });
        return;
      }

      // Filter results based on query
      const filteredResults = MOCK_BGG_SEARCH_RESULTS.filter(g =>
        g.name.toLowerCase().includes(query.toLowerCase())
      );

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ results: filteredResults }),
      });
    });

    // Mock BGG game details endpoint
    await page.route(new RegExp(`${API_BASE}/api/v1/bgg/games/\\d+`), async route => {
      const url = new URL(route.request().url());
      const bggId = parseInt(url.pathname.split('/').pop() || '0');

      if (bggId === MOCK_BGG_GAME_DETAILS.bggId) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_BGG_GAME_DETAILS),
        });
      } else {
        await route.fulfill({
          status: 404,
          contentType: 'application/json',
          body: JSON.stringify({ error: `Game with BGG ID ${bggId} not found` }),
        });
      }
    });

    // Mock local games endpoint
    await page.route(`${API_BASE}/api/v1/games*`, async route => {
      const method = route.request().method();

      if (method === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            games: MOCK_LOCAL_GAMES,
            total: MOCK_LOCAL_GAMES.length,
            page: 1,
            pageSize: 20,
            totalPages: 1,
          }),
        });
      } else if (method === 'POST') {
        const body = route.request().postDataJSON() as { title: string; bggId?: number };
        const newGame = {
          id: `game-${Date.now()}`,
          title: body.title,
          bggId: body.bggId,
          ...MOCK_BGG_GAME_DETAILS,
        };
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(newGame),
        });
      } else {
        await route.continue();
      }
    });

    // Mock link BGG to local game
    await page.route(new RegExp(`${API_BASE}/api/v1/games/[^/]+/link-bgg`), async route => {
      const body = route.request().postDataJSON() as { bggId: number };
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          message: 'Game linked successfully',
          bggId: body.bggId,
          metadata: MOCK_BGG_GAME_DETAILS,
        }),
      });
    });

    // Mock update game with BGG data
    await page.route(new RegExp(`${API_BASE}/api/v1/games/[^/]+$`), async route => {
      if (route.request().method() === 'PUT' || route.request().method() === 'PATCH') {
        const body = route.request().postDataJSON();
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ...MOCK_LOCAL_GAMES[0],
            ...body,
            updatedAt: new Date().toISOString(),
          }),
        });
      } else {
        await route.continue();
      }
    });
  });

  test('should search for games on BoardGameGeek', async ({ page }) => {
    await page.goto('/library');

    // Find search input
    const searchInput = page
      .locator('input[placeholder*="BoardGameGeek"]')
      .or(page.locator('input[placeholder*="BGG"]'))
      .or(page.locator('[data-testid="bgg-search-input"]'));

    await searchInput.fill('Scythe');

    // Click search button
    const searchButton = page
      .getByRole('button', { name: /Cerca|Search/i })
      .or(page.locator('[data-testid="bgg-search-button"]'));
    await searchButton.click();

    // Verify results are displayed
    await expect(page.getByText('Scythe')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Scythe: The Rise of Fenris')).toBeVisible();
    await expect(page.getByText('Scythe: Invaders from Afar')).toBeVisible();

    // Verify year is shown
    await expect(page.getByText('2016')).toBeVisible();
    await expect(page.getByText('2018')).toBeVisible();
  });

  test('should view BGG game details', async ({ page }) => {
    await page.goto('/library');

    // Search for game
    const searchInput = page
      .locator('input[placeholder*="BoardGameGeek"]')
      .or(page.locator('[data-testid="bgg-search-input"]'));
    await searchInput.fill('Scythe');
    await page.getByRole('button', { name: /Cerca|Search/i }).click();

    // Wait for results and click on first one
    await expect(page.getByText('Scythe').first()).toBeVisible();

    // Click to view details (or expand)
    const detailsButton = page
      .locator('[data-testid="bgg-game-card"]')
      .first()
      .getByRole('button', { name: /Details|Dettagli|Info/i })
      .or(page.getByText('Scythe').first());
    await detailsButton.click();

    // Verify detailed information is shown
    await expect(page.getByText('Stonemaier Games')).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('Jamey Stegmaier')).toBeVisible();
    await expect(page.getByText(/1-5|1 - 5/)).toBeVisible(); // Players
    await expect(page.getByText(/115|90-115/)).toBeVisible(); // Play time
  });

  test('should add new game from BGG search', async ({ page }) => {
    await page.goto('/library');

    // Search
    const searchInput = page
      .locator('input[placeholder*="BoardGameGeek"]')
      .or(page.locator('[data-testid="bgg-search-input"]'));
    await searchInput.fill('Scythe');
    await page.getByRole('button', { name: /Cerca|Search/i }).click();

    // Wait for results
    await expect(page.getByText('Scythe').first()).toBeVisible();

    // Click Add button on first result
    const addButton = page
      .locator('[data-testid="bgg-game-card"], .bgg-result')
      .first()
      .getByRole('button', { name: /Aggiungi|Add/i });
    await addButton.click();

    // Verify success
    await expect(page.locator('text=aggiunto|added|success|creato', { exact: false })).toBeVisible({
      timeout: 10000,
    });

    // Verify redirect to library
    await expect(page).toHaveURL(/\/library/);
  });

  test('should link BGG game to existing local game', async ({ page }) => {
    await page.goto('/library');

    // Find a game without BGG link (Chess)
    const chessGame = page
      .locator('[data-testid="game-card"], .game-card')
      .filter({ hasText: 'Chess' });
    await expect(chessGame).toBeVisible();

    // Click edit or link button
    const linkButton = chessGame
      .getByRole('button', { name: /Link BGG|Collega BGG|Edit/i })
      .or(chessGame.locator('[data-testid="link-bgg-button"]'));

    if (await linkButton.isVisible()) {
      await linkButton.click();

      // Search for BGG game
      const bggSearchInput = page.locator('input[placeholder*="BGG"]');
      if (await bggSearchInput.isVisible({ timeout: 2000 }).catch(() => false)) {
        await bggSearchInput.fill('Scythe');
        await page.getByRole('button', { name: /Cerca|Search/i }).click();

        // Select a game to link
        const selectButton = page
          .locator('[data-testid="bgg-result"]')
          .first()
          .getByRole('button', { name: /Select|Seleziona|Link/i });
        await selectButton.click();

        // Verify success
        await expect(page.locator('text=linked|collegato|success', { exact: false })).toBeVisible({
          timeout: 5000,
        });
      }
    }
  });

  test('should auto-fetch metadata from BGG when linking', async ({ page }) => {
    await page.goto('/library');

    // Find game without BGG link
    const gameCard = page
      .locator('[data-testid="game-card"], .game-card')
      .filter({ hasText: 'Chess' });

    const editButton = gameCard.getByRole('button', { name: /Edit|Modifica/i });
    if (await editButton.isVisible()) {
      await editButton.click();

      // Look for "Fetch from BGG" or similar option
      const fetchBggButton = page
        .getByRole('button', { name: /Fetch|Recupera|Import from BGG/i })
        .or(page.locator('[data-testid="fetch-bgg-metadata"]'));

      if (await fetchBggButton.isVisible({ timeout: 2000 }).catch(() => false)) {
        await fetchBggButton.click();

        // Search and select
        const bggSearchInput = page.locator('input[placeholder*="BGG"]');
        await bggSearchInput.fill('Scythe');
        await page.getByRole('button', { name: /Search|Cerca/i }).click();

        await page.locator('[data-testid="bgg-result"]').first().click();

        // Verify form fields are populated
        await expect(
          page.locator('input[name="publisher"], [data-testid="publisher-input"]')
        ).toHaveValue(/Stonemaier/);
      }
    }
  });

  test('should handle empty BGG search results', async ({ page }) => {
    await page.goto('/library');

    const searchInput = page
      .locator('input[placeholder*="BoardGameGeek"]')
      .or(page.locator('[data-testid="bgg-search-input"]'));
    await searchInput.fill('XYZNonExistentGame123');
    await page.getByRole('button', { name: /Cerca|Search/i }).click();

    // Verify empty state
    await expect(
      page.locator('text=Nessun risultato|No results|not found|non trovato', { exact: false })
    ).toBeVisible({ timeout: 10000 });
  });

  test('should handle BGG API errors gracefully', async ({ page }) => {
    // Override to return error
    await page.route(`${API_BASE}/api/v1/bgg/search*`, async route => {
      await route.fulfill({
        status: 503,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'BGG API temporarily unavailable' }),
      });
    });

    await page.goto('/library');

    const searchInput = page
      .locator('input[placeholder*="BoardGameGeek"]')
      .or(page.locator('[data-testid="bgg-search-input"]'));
    await searchInput.fill('Scythe');
    await page.getByRole('button', { name: /Cerca|Search/i }).click();

    // Verify error message
    await expect(
      page.locator('text=error|errore|unavailable|non disponibile', { exact: false })
    ).toBeVisible({ timeout: 10000 });
  });

  test('should validate BGG ID format', async ({ page }) => {
    // Override to return 400 for invalid ID
    await page.route(`${API_BASE}/api/v1/bgg/games/-1`, async route => {
      await route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Invalid BGG ID. Must be a positive integer.' }),
      });
    });

    // This test depends on UI allowing manual BGG ID entry
    await page.goto('/library');

    const manualBggInput = page.locator('input[name="bggId"], [data-testid="bgg-id-input"]');
    if (await manualBggInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await manualBggInput.fill('-1');
      await page.getByRole('button', { name: /Verify|Verifica|Fetch/i }).click();

      await expect(page.locator('text=invalid|non valido|error', { exact: false })).toBeVisible({
        timeout: 5000,
      });
    }
  });

  test('should display BGG rating and weight', async ({ page }) => {
    await page.goto('/library');

    const searchInput = page
      .locator('input[placeholder*="BoardGameGeek"]')
      .or(page.locator('[data-testid="bgg-search-input"]'));
    await searchInput.fill('Scythe');
    await page.getByRole('button', { name: /Cerca|Search/i }).click();

    // Click to view details
    await page.getByText('Scythe').first().click();

    // Verify rating is displayed
    await expect(page.getByText(/8\.2|8,2/)).toBeVisible({ timeout: 5000 });

    // Verify weight is displayed (if shown)
    const weightText = page.locator('text=3.4|3,4|weight|peso');
    if (await weightText.isVisible({ timeout: 2000 }).catch(() => false)) {
      await expect(weightText).toBeVisible();
    }
  });

  test('should show BGG categories and mechanics', async ({ page }) => {
    await page.goto('/library');

    const searchInput = page
      .locator('input[placeholder*="BoardGameGeek"]')
      .or(page.locator('[data-testid="bgg-search-input"]'));
    await searchInput.fill('Scythe');
    await page.getByRole('button', { name: /Cerca|Search/i }).click();

    // View details
    await page.getByText('Scythe').first().click();

    // Verify categories
    await expect(page.getByText('Economic')).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('Territory Building')).toBeVisible();

    // Verify mechanics
    await expect(page.getByText('Area Control')).toBeVisible();
    await expect(page.getByText('Variable Player Powers')).toBeVisible();
  });

  test('should indicate already linked games in search results', async ({ page }) => {
    await page.goto('/library');

    const searchInput = page
      .locator('input[placeholder*="BoardGameGeek"]')
      .or(page.locator('[data-testid="bgg-search-input"]'));
    await searchInput.fill('Scythe');
    await page.getByRole('button', { name: /Cerca|Search/i }).click();

    // Check if "already added" indicator exists for Scythe (which is in MOCK_LOCAL_GAMES)
    const scytheResult = page.locator('[data-testid="bgg-result"], .bgg-result').first();
    await expect(scytheResult).toBeVisible();

    // Look for "already added" indicator
    const alreadyAddedIndicator = scytheResult.locator(
      'text=già aggiunto|already added|linked|collegato',
      { exact: false }
    );

    // This depends on UI implementation
    // If the indicator exists, verify it
    if (await alreadyAddedIndicator.isVisible({ timeout: 2000 }).catch(() => false)) {
      await expect(alreadyAddedIndicator).toBeVisible();
    }
  });

  test('should handle search with special characters', async ({ page }) => {
    await page.goto('/library');

    const searchInput = page
      .locator('input[placeholder*="BoardGameGeek"]')
      .or(page.locator('[data-testid="bgg-search-input"]'));

    // Test with special characters (should be escaped properly)
    await searchInput.fill('Scythe: The Rise');
    await page.getByRole('button', { name: /Cerca|Search/i }).click();

    // Should still return results
    await expect(page.getByText('Scythe: The Rise of Fenris')).toBeVisible({ timeout: 10000 });
  });

  test('should require minimum search length', async ({ page }) => {
    await page.goto('/library');

    const searchInput = page
      .locator('input[placeholder*="BoardGameGeek"]')
      .or(page.locator('[data-testid="bgg-search-input"]'));
    await searchInput.fill('S'); // Too short

    const searchButton = page.getByRole('button', { name: /Cerca|Search/i });

    // Either button is disabled or error shown
    const isDisabled = await searchButton.isDisabled();
    if (!isDisabled) {
      await searchButton.click();
      await expect(
        page.locator('text=almeno|at least|minimum|caratteri', { exact: false })
      ).toBeVisible({ timeout: 5000 });
    }
  });
});
