/**
 * Game Discovery Flow - Visual Documentation
 *
 * Captures visual documentation for game discovery flows:
 * - Browse catalog
 * - Search games
 * - View game details
 * - Filter and sort
 *
 * @see docs/08-user-flows/user-role/02-game-discovery.md
 */

import { test } from '../../fixtures';
import { AuthHelper, USER_FIXTURES } from '../../pages';
import {
  ScreenshotHelper,
  USER_FLOWS,
  disableAnimations,
  waitForStableState,
  ANNOTATION_COLORS,
} from '../fixtures/screenshot-helpers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock game data
const MOCK_GAMES = [
  {
    id: 'game-1',
    title: 'Ticket to Ride',
    publisher: 'Days of Wonder',
    yearPublished: 2004,
    minPlayers: 2,
    maxPlayers: 5,
    minPlayTimeMinutes: 30,
    maxPlayTimeMinutes: 60,
    bggId: 9209,
    thumbnailUrl: 'https://cf.geekdo-images.com/ZWJg0dCdrWHxVnc0eFXK8w__thumb/img/example.jpg',
  },
  {
    id: 'game-2',
    title: 'Catan',
    publisher: 'Kosmos',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    minPlayTimeMinutes: 60,
    maxPlayTimeMinutes: 120,
    bggId: 13,
    thumbnailUrl: 'https://cf.geekdo-images.com/example2.jpg',
  },
  {
    id: 'game-3',
    title: 'Wingspan',
    publisher: 'Stonemaier Games',
    yearPublished: 2019,
    minPlayers: 1,
    maxPlayers: 5,
    minPlayTimeMinutes: 40,
    maxPlayTimeMinutes: 70,
    bggId: 266192,
    thumbnailUrl: 'https://cf.geekdo-images.com/example3.jpg',
  },
];

test.describe('Game Discovery Flow - Visual Documentation', () => {
  let helper: ScreenshotHelper;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    helper = new ScreenshotHelper({
      outputDir: USER_FLOWS.gameDiscovery.outputDir,
      flow: USER_FLOWS.gameDiscovery.name,
      role: USER_FLOWS.gameDiscovery.role,
    });
    authHelper = new AuthHelper(page);
    await disableAnimations(page);

    // Setup authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock games endpoint
    await page.route(`${API_BASE}/api/v1/games*`, async route => {
      const url = route.request().url();
      if (url.includes('/games/game-1')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_GAMES[0]),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_GAMES),
        });
      }
    });

    // Mock shared catalog
    await page.route(`${API_BASE}/api/v1/shared-games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: MOCK_GAMES,
          total: MOCK_GAMES.length,
          page: 1,
          pageSize: 10,
        }),
      });
    });
  });

  test('browse catalog - game list', async ({ page }) => {
    // Step 1: Navigate to games page
    await page.goto('/games');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Game Catalog',
      description: 'Browse the game catalog with search and filter options',
      annotations: [
        { selector: 'input[type="search"], [data-testid="search-input"]', label: 'Search', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Browse or search games',
    });

    // Step 2: View game cards
    const gameCards = page.locator('[data-testid="game-card"], .game-card, article').first();
    if (await gameCards.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Game Cards',
        description: 'Games displayed as cards with key information',
        annotations: [
          { selector: '[data-testid="game-card"], .game-card, article', label: 'Game Card', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View catalog',
        nextAction: 'Click on a game',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Browse catalog captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('search games - BGG integration', async ({ page }) => {
    // Mock BGG search
    await page.route(`${API_BASE}/api/v1/bgg/search*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          results: [
            { bggId: 9209, name: 'Ticket to Ride', yearPublished: 2004, thumbnailUrl: 'https://example.com/thumb.jpg' },
            { bggId: 9216, name: 'Ticket to Ride: Europe', yearPublished: 2005, thumbnailUrl: 'https://example.com/thumb2.jpg' },
          ],
        }),
      });
    });

    // Step 1: Navigate to add game page
    await page.goto('/games/add');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Add Game - BGG Search',
      description: 'Search for games on BoardGameGeek to add to library',
      annotations: [
        { selector: 'input[placeholder*="Cerca"], input[placeholder*="Search"], input[type="search"]', label: 'BGG Search', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Enter game name',
    });

    // Step 2: Enter search term
    const searchInput = page.locator('input[placeholder*="Cerca"], input[placeholder*="Search"], input[type="search"]').first();
    if (await searchInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await searchInput.fill('Ticket to Ride');
      await waitForStableState(page);

      await helper.capture(page, {
        step: 2,
        title: 'Search Term Entered',
        description: 'User types game name to search BGG database',
        annotations: [
          { selector: 'input[placeholder*="Cerca"], input[placeholder*="Search"]', label: 'Search Term', color: ANNOTATION_COLORS.success },
          { selector: 'button:has-text("Cerca"), button:has-text("Search")', label: 'Search Button', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'Enter search term',
        nextAction: 'Click search',
      });

      // Step 3: Click search and wait for results
      const searchButton = page.locator('button:has-text("Cerca"), button:has-text("Search")').first();
      if (await searchButton.isVisible({ timeout: 1000 }).catch(() => false)) {
        await searchButton.click();
        await page.waitForTimeout(500);
        await waitForStableState(page);

        await helper.capture(page, {
          step: 3,
          title: 'Search Results',
          description: 'BGG search results showing matching games',
          annotations: [
            { selector: 'text=Ticket to Ride', label: 'Result', color: ANNOTATION_COLORS.success },
            { selector: 'button:has-text("Aggiungi"), button:has-text("Add")', label: 'Add Button', color: ANNOTATION_COLORS.primary },
          ],
          previousAction: 'Execute search',
          nextAction: 'Select game to add',
        });
      }
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ BGG search captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('game detail page', async ({ page }) => {
    // Mock game details and PDFs
    await page.route(`${API_BASE}/api/v1/games/game-1`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_GAMES[0]),
      });
    });

    await page.route(`${API_BASE}/api/v1/games/game-1/pdfs`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'pdf-1', fileName: 'rulebook.pdf', pageCount: 12, processingStatus: 'Completed' },
        ]),
      });
    });

    await page.route(`${API_BASE}/api/v1/games/game-1/rules`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    // Step 1: Navigate to game detail
    await page.goto('/games/game-1');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Game Detail Page',
      description: 'Detailed view of a game with all information',
      annotations: [
        { selector: 'h1, [data-testid="game-title"]', label: 'Game Title', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Explore game details',
    });

    // Step 2: Check for tabs/sections
    const rulesTab = page.locator('button:has-text("Rules"), [role="tab"]:has-text("Rules")').first();
    if (await rulesTab.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Game Tabs',
        description: 'Navigate between game information sections',
        annotations: [
          { selector: 'button:has-text("Rules"), [role="tab"]:has-text("Rules")', label: 'Rules Tab', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View game page',
        nextAction: 'Click Rules tab',
      });

      // Step 3: Click Rules tab
      await rulesTab.click();
      await waitForStableState(page);

      await helper.capture(page, {
        step: 3,
        title: 'Rules Section',
        description: 'Game rules and PDF documents',
        annotations: [
          { selector: 'text=Rulebook PDFs, text=PDF', label: 'PDFs Section', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'Click Rules tab',
        nextAction: 'View or upload PDF',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Game detail captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('filter and sort games', async ({ page }) => {
    // Step 1: Navigate to games
    await page.goto('/games');
    await waitForStableState(page);

    // Look for filter controls
    const filterSection = page.locator('[data-testid="filters"], .filters, [role="search"]').first();
    if (await filterSection.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 1,
        title: 'Filter Options',
        description: 'Filter games by various criteria',
        annotations: [
          { selector: '[data-testid="filters"], .filters', label: 'Filters', color: ANNOTATION_COLORS.info },
        ],
        nextAction: 'Apply filters',
      });
    }

    // Look for sort dropdown
    const sortSelect = page.locator('select, [data-testid="sort-select"], button:has-text("Sort")').first();
    if (await sortSelect.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Sort Options',
        description: 'Sort games by name, year, or popularity',
        annotations: [
          { selector: 'select, [data-testid="sort-select"]', label: 'Sort By', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'View sort options',
        nextAction: 'Select sort order',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Filter/sort captured: ${helper.getCapturedSteps().length} screenshots`);
  });
});
