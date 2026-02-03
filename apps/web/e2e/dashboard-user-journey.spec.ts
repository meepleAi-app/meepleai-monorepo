/**
 * Dashboard User Journey E2E Tests (Issue #2862)
 *
 * End-to-end tests for the complete user dashboard journey including:
 * - Login → Dashboard loads with all sections
 * - Click recent game → Navigate to game detail
 * - Click chat thread → Navigate to chat
 * - Quick action 'Add Game' → Navigate to catalog
 * - Library quota CTA → Navigate appropriately
 * - Visual regression test (screenshot baseline)
 *
 * @see Issue #2862 - [User Dashboard] E2E Tests - User Journey
 */

import { test, expect } from './fixtures';
import { AuthHelper, USER_FIXTURES } from './pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock game data for RecentGamesSection
const MOCK_GAMES = [
  {
    id: 'game-1',
    name: 'Azul',
    description: 'A game about building beautiful mosaic patterns',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 45,
    complexity: 1.8,
    yearPublished: 2017,
    publisher: 'Plan B Games',
    iconUrl: null,
    imageUrl: 'https://example.com/azul.png',
    thumbnailUrl: null,
  },
  {
    id: 'game-2',
    name: 'Wingspan',
    description: 'A competitive bird-collection engine-building game',
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 70,
    complexity: 2.4,
    yearPublished: 2019,
    publisher: 'Stonemaier Games',
    iconUrl: null,
    imageUrl: 'https://example.com/wingspan.png',
    thumbnailUrl: null,
  },
  {
    id: 'game-3',
    name: 'Catan',
    description: 'Trade, build and settle on the island of Catan',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
    complexity: 2.3,
    yearPublished: 1995,
    publisher: 'Kosmos',
    iconUrl: null,
    imageUrl: null,
    thumbnailUrl: null,
  },
  {
    id: 'game-4',
    name: 'Ticket to Ride',
    description: 'Build train routes across the country',
    minPlayers: 2,
    maxPlayers: 5,
    playingTimeMinutes: 60,
    complexity: 1.9,
    yearPublished: 2004,
    publisher: 'Days of Wonder',
    iconUrl: null,
    imageUrl: 'https://example.com/ticket.png',
    thumbnailUrl: null,
  },
  {
    id: 'game-5',
    name: 'Pandemic',
    description: 'Work together to stop global disease outbreaks',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 45,
    complexity: 2.4,
    yearPublished: 2008,
    publisher: 'Z-Man Games',
    iconUrl: null,
    imageUrl: 'https://example.com/pandemic.png',
    thumbnailUrl: null,
  },
  {
    id: 'game-6',
    name: 'Splendor',
    description: 'Collect gems and build a jewelry empire',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 30,
    complexity: 1.8,
    yearPublished: 2014,
    publisher: 'Space Cowboys',
    iconUrl: null,
    imageUrl: 'https://example.com/splendor.png',
    thumbnailUrl: null,
  },
];

// Mock library data
const MOCK_LIBRARY_GAMES = [
  {
    id: '1',
    userId: 'user-test-1',
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
    userId: 'user-test-1',
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
];

// Mock quota data
const MOCK_QUOTA = {
  currentCount: 3,
  maxAllowed: 10,
  userTier: 'free',
  remainingSlots: 7,
  percentageUsed: 30,
};

// Mock sessions data
const MOCK_SESSIONS = [
  {
    id: 'session-1',
    deviceName: 'Chrome on Windows',
    ipAddress: '127.0.0.1',
    lastActiveAt: new Date().toISOString(),
    createdAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
    isCurrent: true,
  },
];

// Mock session quota data
const MOCK_SESSION_QUOTA = {
  currentActiveSessions: 1,
  maxConcurrentSessions: 5,
  remainingSlots: 4,
  percentageUsed: 20,
};

/**
 * Helper to set up all required API mocks for dashboard
 *
 * Note: Specific routes must be registered BEFORE generic wildcard routes
 * to ensure proper matching in Playwright.
 */
async function setupDashboardMocks(page: Parameters<typeof test>[0]['page']): Promise<void> {
  // Mock library quota API (specific route BEFORE generic library route)
  await page.route(`${API_BASE}/api/v1/library/quota`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_QUOTA),
    });
  });

  // Mock library API (generic route AFTER specific quota route)
  await page.route(`${API_BASE}/api/v1/library*`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: MOCK_LIBRARY_GAMES,
        page: 1,
        pageSize: 5,
        totalCount: MOCK_LIBRARY_GAMES.length,
        totalPages: 1,
        hasNextPage: false,
        hasPreviousPage: false,
      }),
    });
  });

  // Mock single game detail API (for navigation to game detail page)
  await page.route(new RegExp(`${API_BASE}/api/v1/games/[^/]+$`), async route => {
    const url = route.request().url();
    const gameId = url.split('/').pop();
    const game = MOCK_GAMES.find(g => g.id === gameId) || MOCK_GAMES[0];
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(game),
    });
  });

  // Mock games list API
  await page.route(`${API_BASE}/api/v1/games*`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        games: MOCK_GAMES,
        totalCount: MOCK_GAMES.length,
      }),
    });
  });

  // Mock session quota API (specific route BEFORE generic sessions route)
  await page.route(`${API_BASE}/api/v1/auth/sessions/quota`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_SESSION_QUOTA),
    });
  });

  // Mock sessions API (generic route AFTER specific quota route)
  await page.route(`${API_BASE}/api/v1/auth/sessions*`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        sessions: MOCK_SESSIONS,
        totalCount: MOCK_SESSIONS.length,
      }),
    });
  });
}

test.describe('Dashboard User Journey', () => {
  test.describe('DoD #1: Login → Dashboard loads with all sections', () => {
    test('dashboard displays all sections after authenticated login', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      // Setup authenticated session
      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
      await setupDashboardMocks(page);

      // Navigate to dashboard
      await page.goto('/dashboard');

      // Verify dashboard loaded (main container)
      await expect(page.locator('.container')).toBeVisible({ timeout: 15000 });

      // Verify Greeting Section
      await expect(page.getByText(/Ciao|Buongiorno|Buonasera/)).toBeVisible({ timeout: 10000 });

      // Verify Recent Games Section
      await expect(page.getByTestId('recent-games-section')).toBeVisible({ timeout: 10000 });
      await expect(page.getByTestId('recent-games-title')).toContainText('Giochi Recenti');

      // Verify Chat History Section
      await expect(page.getByTestId('chat-history-section')).toBeVisible();
      await expect(page.getByTestId('chat-history-title')).toContainText('Cronologia Chat');

      // Verify Library Quota Section
      await expect(page.getByTestId('library-quota-card')).toBeVisible();
      await expect(page.getByTestId('library-quota-title')).toContainText('La Mia Libreria');

      // Verify Quick Actions Section
      await expect(page.getByRole('region', { name: /quick actions/i })).toBeVisible();
      await expect(page.getByText('Azioni Rapide')).toBeVisible();
    });

    test('dashboard shows game cards in recent games section', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
      await setupDashboardMocks(page);

      await page.goto('/dashboard');

      // Wait for games to load
      await expect(page.getByTestId('recent-games-grid')).toBeVisible({ timeout: 15000 });

      // Verify game cards are displayed
      await expect(page.getByText('Azul')).toBeVisible();
      await expect(page.getByText('Wingspan')).toBeVisible();
      await expect(page.getByText('Catan')).toBeVisible();
    });
  });

  test.describe('DoD #2: Click recent game → Navigate to game detail', () => {
    test('clicking a game card navigates to game detail page', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
      await setupDashboardMocks(page);

      await page.goto('/dashboard');

      // Wait for games to load
      await expect(page.getByTestId('recent-games-grid')).toBeVisible({ timeout: 15000 });

      // Click on the first game card (Azul)
      const firstGameCard = page.getByText('Azul').first();
      await expect(firstGameCard).toBeVisible();
      await firstGameCard.click();

      // Verify navigation to game detail
      await page.waitForURL('**/games/game-1', { timeout: 10000 });
      await expect(page).toHaveURL(/\/games\/game-1/);
    });

    test('clicking "Vedi Tutti" navigates to games catalog', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
      await setupDashboardMocks(page);

      await page.goto('/dashboard');

      // Wait for recent games section
      await expect(page.getByTestId('recent-games-section')).toBeVisible({ timeout: 15000 });

      // Click "Vedi Tutti" link
      const viewAllLink = page.getByTestId('recent-games-view-all-button');
      await expect(viewAllLink).toBeVisible();
      await viewAllLink.click();

      // Verify navigation to games catalog
      await page.waitForURL('**/games', { timeout: 10000 });
      await expect(page).toHaveURL(/\/games$/);
    });
  });

  test.describe('DoD #3: Click chat thread → Navigate to chat', () => {
    test('clicking "Apri Chat" button navigates to chat page', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
      await setupDashboardMocks(page);

      await page.goto('/dashboard');

      // Wait for chat history section
      await expect(page.getByTestId('chat-history-section')).toBeVisible({ timeout: 15000 });

      // Click "Apri Chat" button (use getByRole since Button with asChild doesn't pass data-testid to DOM)
      const openChatButton = page.getByRole('link', { name: /Apri Chat/i });
      await expect(openChatButton).toBeVisible();
      await openChatButton.click();

      // Verify navigation to chat page
      await page.waitForURL('**/chat', { timeout: 10000 });
      await expect(page).toHaveURL(/\/chat/);
    });

    test('clicking "Vedi Tutte" link navigates to chat page', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
      await setupDashboardMocks(page);

      await page.goto('/dashboard');

      // Wait for chat history section
      await expect(page.getByTestId('chat-history-section')).toBeVisible({ timeout: 15000 });

      // Click "Vedi Tutte" link (use getByRole since Button with asChild doesn't pass data-testid to DOM)
      const viewAllLink = page
        .getByTestId('chat-history-section')
        .getByRole('link', { name: /Vedi Tutte/i });
      await expect(viewAllLink).toBeVisible();
      await viewAllLink.click();

      // Verify navigation to chat page
      await page.waitForURL('**/chat', { timeout: 10000 });
      await expect(page).toHaveURL(/\/chat/);
    });
  });

  test.describe('DoD #4: Quick action Add Game → Navigate to catalog', () => {
    test('clicking Add Game quick action navigates to add game page', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
      await setupDashboardMocks(page);

      await page.goto('/dashboard');

      // Wait for quick actions section
      await expect(page.getByRole('region', { name: /quick actions/i })).toBeVisible({ timeout: 15000 });

      // Click "Add Game" quick action card
      const addGameAction = page.getByText('Add Game').first();
      await expect(addGameAction).toBeVisible();
      await addGameAction.click();

      // Verify navigation to add game page
      await page.waitForURL('**/games/add', { timeout: 10000 });
      await expect(page).toHaveURL(/\/games\/add/);
    });

    test('clicking New Chat quick action navigates to new chat page', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
      await setupDashboardMocks(page);

      await page.goto('/dashboard');

      // Wait for quick actions section
      await expect(page.getByRole('region', { name: /quick actions/i })).toBeVisible({ timeout: 15000 });

      // Click "New Chat" quick action card
      const newChatAction = page.getByText('New Chat').first();
      await expect(newChatAction).toBeVisible();
      await newChatAction.click();

      // Verify navigation to new chat page
      await page.waitForURL('**/chat/new', { timeout: 10000 });
      await expect(page).toHaveURL(/\/chat\/new/);
    });
  });

  test.describe('DoD #5: Library quota CTA → Navigate appropriately', () => {
    test('clicking library quota card navigates to library page', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
      await setupDashboardMocks(page);

      await page.goto('/dashboard');

      // Wait for library quota card
      await expect(page.getByTestId('library-quota-card')).toBeVisible({ timeout: 15000 });

      // Click library quota card (it's wrapped in a link)
      const quotaLink = page.getByTestId('library-quota-link');
      await expect(quotaLink).toBeVisible();
      await quotaLink.click();

      // Verify navigation to library page
      await page.waitForURL('**/library', { timeout: 10000 });
      await expect(page).toHaveURL(/\/library/);
    });

    test('library quota displays correct count and status', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
      await setupDashboardMocks(page);

      await page.goto('/dashboard');

      // Wait for library quota card
      await expect(page.getByTestId('library-quota-card')).toBeVisible({ timeout: 15000 });

      // Verify quota count is displayed
      await expect(page.getByTestId('library-quota-count-number')).toContainText('3');

      // Verify remaining slots status
      await expect(page.getByTestId('library-quota-status')).toContainText('7 slot disponibili');
    });
  });

  test.describe('DoD #6: Visual regression test (screenshot baseline)', () => {
    test('dashboard visual snapshot - full page', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
      await setupDashboardMocks(page);

      await page.goto('/dashboard');

      // Wait for all sections to load
      await expect(page.getByTestId('recent-games-section')).toBeVisible({ timeout: 15000 });
      await expect(page.getByTestId('chat-history-section')).toBeVisible();
      await expect(page.getByTestId('library-quota-card')).toBeVisible();
      await expect(page.getByRole('region', { name: /quick actions/i })).toBeVisible();

      // Wait for network to settle (ensure all images/data loaded)
      await page.waitForLoadState('networkidle');

      // Chromatic will automatically capture screenshot at end of test
      // The test name enables visual diff detection in Chromatic
    });

    test('dashboard visual snapshot - mobile viewport', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      // Set mobile viewport
      await page.setViewportSize({ width: 375, height: 812 });

      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
      await setupDashboardMocks(page);

      await page.goto('/dashboard');

      // Wait for sections to load
      await expect(page.getByTestId('recent-games-section')).toBeVisible({ timeout: 15000 });
      await expect(page.getByTestId('chat-history-section')).toBeVisible();

      await page.waitForLoadState('networkidle');

      // Chromatic captures screenshot for mobile visual regression
    });
  });
});
