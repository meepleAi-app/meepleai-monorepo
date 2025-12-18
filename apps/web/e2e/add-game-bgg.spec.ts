import { expect, test } from './fixtures/chromatic';
import { AuthHelper, USER_FIXTURES } from './pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Add Game via BGG', () => {
  const MOCK_BGG_SEARCH_RESULTS = [
    {
      bggId: 169786,
      name: 'Scythe',
      yearPublished: 2016,
      thumbnailUrl: 'https://cf.geekdo-images.com/example/scythe.jpg',
    },
    {
      bggId: 123456,
      name: 'Scythe: Invaders from Afar',
      yearPublished: 2016,
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
    thumbnailUrl: 'https://cf.geekdo-images.com/example/scythe_thumb.jpg',
    imageUrl: 'https://cf.geekdo-images.com/example/scythe_large.jpg',
    description: 'It is a time of unrest in 1920s Europa...',
    publishers: ['Stonemaier Games'],
  };

  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock initial games list
    await page.route(`${API_BASE}/api/v1/games*`, async route => {
      const method = route.request().method();
      if (method === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            games: [],
            total: 0,
            page: 1,
            pageSize: 20,
            totalPages: 0,
          }),
        });
      } else {
        await route.continue();
      }
    });

    // Mock BGG Search
    await page.route(`${API_BASE}/api/v1/bgg/search*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          results: MOCK_BGG_SEARCH_RESULTS,
        }),
      });
    });

    // Mock BGG Details
    await page.route(`${API_BASE}/api/v1/bgg/games/169786`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_BGG_GAME_DETAILS),
      });
    });

    // Mock Create Game POST
    await page.route(`${API_BASE}/api/v1/games`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'new-game-id',
            ...MOCK_BGG_GAME_DETAILS,
            title: MOCK_BGG_GAME_DETAILS.name, // Mapper might expect title
            publisher: MOCK_BGG_GAME_DETAILS.publishers[0],
          }),
        });
      } else {
        await route.continue();
      }
    });
  });

  test('should search and add a game via BGG', async ({ page }) => {
    // 1. Navigate to Games page
    await page.goto('/games');

    // 2. Click "Aggiungi Gioco"
    await page.click('text=Aggiungi Gioco');
    await expect(page).toHaveURL(/\/games\/add/);

    // 3. Search for "Scythe"
    await page.fill('input[placeholder="Cerca su BoardGameGeek..."]', 'Scythe');
    await page.click('button:has-text("Cerca")');

    // 4. Verify results
    const results = page.locator('.grid > div'); // Adjust selector based on Card implementation
    await expect(results).toHaveCount(2);
    await expect(results.first()).toContainText('Scythe');
    await expect(results.first()).toContainText('2016');

    // 5. Add Game
    await results.first().getByRole('button', { name: 'Aggiungi' }).click();

    // 6. Verify redirection to games page and toast
    await expect(page).toHaveURL(/\/(games)$/);
    await expect(page.locator('text=Gioco aggiunto con successo!')).toBeVisible();
  });

  test('should show empty state when no results found', async ({ page }) => {
    // Override search mock for empty results
    await page.route(`${API_BASE}/api/v1/bgg/search*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          results: [],
        }),
      });
    });

    await page.goto('/games/add');
    await page.fill('input[placeholder="Cerca su BoardGameGeek..."]', 'NoResultsGame');
    await page.click('button:has-text("Cerca")');

    await expect(page.locator('text=Nessun risultato trovato')).toBeVisible();
  });
});
