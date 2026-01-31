/**
 * Issue #2897: Editor Dashboard E2E Tests
 *
 * Tests for the Editor Dashboard workflow:
 * - Login as editor → Dashboard loads
 * - View own games list with stats
 * - Filter by status (Draft, Pending, Published, Archived)
 * - Submit single game for approval
 * - View rejection feedback from Admin
 * - Bulk select multiple games
 * - Bulk submit for approval
 * - Verify editor CANNOT approve/reject (negative test)
 *
 * Run: pnpm test:e2e apps/web/e2e/editor/dashboard.spec.ts
 */

import { test, expect, Page } from '@playwright/test';

import { setupMockAuth } from '../fixtures/auth';

// API base URL
const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock data for editor's games
const MOCK_EDITOR_GAMES = [
  {
    id: 'game-draft-1',
    bggId: 12345,
    title: 'Wingspan',
    yearPublished: 2019,
    description: 'A competitive bird-collection game',
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 70,
    minAge: 10,
    complexityRating: 2.45,
    averageRating: 8.1,
    imageUrl: 'https://example.com/wingspan.jpg',
    thumbnailUrl: 'https://example.com/wingspan-thumb.jpg',
    status: 'Draft',
    createdAt: '2026-01-15T10:00:00Z',
    modifiedAt: '2026-01-20T15:30:00Z',
  },
  {
    id: 'game-draft-2',
    bggId: 12346,
    title: 'Azul',
    yearPublished: 2017,
    description: 'A tile-laying game',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 45,
    minAge: 8,
    complexityRating: 1.78,
    averageRating: 7.8,
    imageUrl: 'https://example.com/azul.jpg',
    thumbnailUrl: 'https://example.com/azul-thumb.jpg',
    status: 'Draft',
    createdAt: '2026-01-10T09:00:00Z',
    modifiedAt: '2026-01-18T12:00:00Z',
  },
  {
    id: 'game-pending-1',
    bggId: 12347,
    title: 'Scythe',
    yearPublished: 2016,
    description: 'A competitive game of engine-building',
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 115,
    minAge: 14,
    complexityRating: 3.42,
    averageRating: 8.2,
    imageUrl: 'https://example.com/scythe.jpg',
    thumbnailUrl: 'https://example.com/scythe-thumb.jpg',
    status: 'PendingApproval',
    createdAt: '2026-01-05T08:00:00Z',
    modifiedAt: '2026-01-22T16:00:00Z',
  },
  {
    id: 'game-published-1',
    bggId: 12348,
    title: 'Ticket to Ride',
    yearPublished: 2004,
    description: 'A cross-country train adventure',
    minPlayers: 2,
    maxPlayers: 5,
    playingTimeMinutes: 60,
    minAge: 8,
    complexityRating: 1.85,
    averageRating: 7.4,
    imageUrl: 'https://example.com/ttr.jpg',
    thumbnailUrl: 'https://example.com/ttr-thumb.jpg',
    status: 'Published',
    createdAt: '2025-12-01T10:00:00Z',
    modifiedAt: '2026-01-10T14:00:00Z',
  },
  {
    id: 'game-archived-1',
    bggId: 12349,
    title: 'Rejected Game',
    yearPublished: 2020,
    description: 'A game that was rejected',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 30,
    minAge: 10,
    complexityRating: 2.0,
    averageRating: 6.5,
    imageUrl: 'https://example.com/rejected.jpg',
    thumbnailUrl: 'https://example.com/rejected-thumb.jpg',
    status: 'Archived',
    createdAt: '2025-11-01T10:00:00Z',
    modifiedAt: '2026-01-05T09:00:00Z',
  },
];

/**
 * Setup mock routes for Editor Dashboard tests
 */
async function setupEditorDashboardMocks(page: Page) {
  // Setup base auth mocks for Editor role
  await setupMockAuth(page, 'Editor', 'editor@meepleai.dev');

  // Mock shared-games endpoint for editor's games
  await page.route(`${API_BASE}/api/v1/admin/shared-games`, async route => {
    const url = new URL(route.request().url());
    const statusParam = url.searchParams.get('status');

    let filteredGames = [...MOCK_EDITOR_GAMES];

    // Apply status filter if provided
    if (statusParam !== null) {
      const statusNumeric = parseInt(statusParam, 10);
      const statusMap: Record<number, string> = {
        0: 'Draft',
        1: 'PendingApproval',
        2: 'Published',
        3: 'Archived',
      };
      const statusString = statusMap[statusNumeric];
      if (statusString) {
        filteredGames = filteredGames.filter(g => g.status === statusString);
      }
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: filteredGames,
        total: filteredGames.length,
        page: 1,
        pageSize: 20,
      }),
    });
  });

  // Mock submit for approval endpoint
  await page.route(`${API_BASE}/api/v1/admin/shared-games/*/submit-for-approval`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true }),
    });
  });

  // Mock approve-publication endpoint (should NOT be called by Editor)
  await page.route(`${API_BASE}/api/v1/admin/shared-games/*/approve-publication`, async route => {
    await route.fulfill({
      status: 403,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Forbidden: Admin only' }),
    });
  });

  // Mock reject-publication endpoint (should NOT be called by Editor)
  await page.route(`${API_BASE}/api/v1/admin/shared-games/*/reject-publication`, async route => {
    await route.fulfill({
      status: 403,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Forbidden: Admin only' }),
    });
  });
}

test.describe('Editor Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    await setupEditorDashboardMocks(page);
  });

  test('should load dashboard when logged in as editor', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Verify dashboard title is visible
    await expect(page.getByTestId('dashboard-title')).toBeVisible();
    await expect(page.getByTestId('dashboard-title')).toContainText('I Miei Giochi');

    // Verify stats section is visible
    await expect(page.getByTestId('stats-section')).toBeVisible();
  });

  test('should display stats cards with correct counts', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Wait for data to load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Verify stats cards show correct counts
    // Draft: 2 (Wingspan, Azul)
    await expect(page.getByTestId('stats-card-bozze')).toContainText('2');

    // Pending: 1 (Scythe)
    await expect(page.getByTestId('stats-card-in-attesa')).toContainText('1');

    // Published: 1 (Ticket to Ride)
    await expect(page.getByTestId('stats-card-pubblicati')).toContainText('1');

    // Archived: 1 (Rejected Game)
    await expect(page.getByTestId('stats-card-archiviati')).toContainText('1');
  });

  test('should display games table with all games', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Wait for table to load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Verify all 5 games are displayed
    await expect(page.getByTestId('game-row-game-draft-1')).toBeVisible();
    await expect(page.getByTestId('game-row-game-draft-2')).toBeVisible();
    await expect(page.getByTestId('game-row-game-pending-1')).toBeVisible();
    await expect(page.getByTestId('game-row-game-published-1')).toBeVisible();
    await expect(page.getByTestId('game-row-game-archived-1')).toBeVisible();
  });

  test('should filter games by status using dropdown', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Wait for table to load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Click on status filter
    await page.getByTestId('status-filter').click();

    // Select "Bozza" (Draft)
    await page.getByRole('option', { name: 'Bozza' }).click();

    // Wait for filtered results
    await expect(page.getByTestId('game-row-game-draft-1')).toBeVisible();
    await expect(page.getByTestId('game-row-game-draft-2')).toBeVisible();

    // Other games should not be visible
    await expect(page.getByTestId('game-row-game-pending-1')).not.toBeVisible();
    await expect(page.getByTestId('game-row-game-published-1')).not.toBeVisible();
  });

  test('should filter games by clicking stats card', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Wait for table to load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Click on "In Attesa" stats card
    await page.getByTestId('stats-card-in-attesa').click();

    // Wait for filtered results - only pending game should be visible
    await expect(page.getByTestId('game-row-game-pending-1')).toBeVisible();

    // Draft games should not be visible
    await expect(page.getByTestId('game-row-game-draft-1')).not.toBeVisible();
  });

  test('should submit single game for approval', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Wait for table to load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Click submit button on first draft game
    await page.getByTestId('submit-button-game-draft-1').click();

    // Verify success toast appears
    await expect(page.getByTestId('toast-success')).toBeVisible();
    await expect(page.getByTestId('toast-success')).toContainText('inviato per approvazione');
  });

  test('should show submit button only for draft games', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Wait for table to load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Draft games should have submit button
    await expect(page.getByTestId('submit-button-game-draft-1')).toBeVisible();
    await expect(page.getByTestId('submit-button-game-draft-2')).toBeVisible();

    // Pending game should NOT have submit button
    await expect(page.getByTestId('submit-button-game-pending-1')).not.toBeVisible();

    // Published game should NOT have submit button
    await expect(page.getByTestId('submit-button-game-published-1')).not.toBeVisible();
  });

  test('should view rejection feedback for archived game', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Wait for table to load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Click feedback button on archived game
    await page.getByTestId('rejection-button-game-archived-1').click();

    // Verify rejection modal opens
    await expect(page.getByTestId('rejection-modal')).toBeVisible();

    // Verify rejection reason is displayed
    await expect(page.getByTestId('rejection-reason')).toBeVisible();
  });

  test('should bulk select draft games', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Wait for table to load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Click select all checkbox
    await page.getByTestId('select-all-checkbox').click();

    // Verify bulk submit button appears with correct count (2 draft games)
    await expect(page.getByTestId('bulk-submit-button')).toBeVisible();
    await expect(page.getByTestId('bulk-submit-button')).toContainText('2');
  });

  test('should select individual games using checkboxes', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Wait for table to load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Select first draft game
    await page.getByTestId('select-checkbox-game-draft-1').click();

    // Verify bulk submit button appears with count 1
    await expect(page.getByTestId('bulk-submit-button')).toBeVisible();
    await expect(page.getByTestId('bulk-submit-button')).toContainText('1');

    // Select second draft game
    await page.getByTestId('select-checkbox-game-draft-2').click();

    // Verify count updates to 2
    await expect(page.getByTestId('bulk-submit-button')).toContainText('2');
  });

  test('should bulk submit selected games for approval', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Wait for table to load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Select all draft games
    await page.getByTestId('select-all-checkbox').click();

    // Click bulk submit button
    await page.getByTestId('bulk-submit-button').click();

    // Verify success toast appears
    await expect(page.getByTestId('toast-success')).toBeVisible();
    await expect(page.getByTestId('toast-success')).toContainText('giochi inviati per approvazione');
  });

  test('should NOT have approve/reject buttons (editor cannot approve)', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Wait for table to load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Verify there are no approve buttons anywhere on the page
    const approveButtons = page.locator('[data-testid^="approve-button-"]');
    await expect(approveButtons).toHaveCount(0);

    // Verify there are no reject buttons anywhere on the page
    const rejectButtons = page.locator('[data-testid^="reject-button-"]');
    await expect(rejectButtons).toHaveCount(0);
  });

  test('should show correct status badges for each game', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Wait for table to load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Verify status badges are displayed correctly
    // Draft game should have draft badge
    const draftRow = page.getByTestId('game-row-game-draft-1');
    await expect(draftRow.getByTestId('status-badge-draft')).toBeVisible();

    // Pending game should have pending badge
    const pendingRow = page.getByTestId('game-row-game-pending-1');
    await expect(pendingRow.getByTestId('status-badge-pendingapproval')).toBeVisible();

    // Published game should have published badge
    const publishedRow = page.getByTestId('game-row-game-published-1');
    await expect(publishedRow.getByTestId('status-badge-published')).toBeVisible();
  });

  test('should navigate to game detail when clicking view button', async ({ page }) => {
    await page.goto('/editor/dashboard');

    // Wait for table to load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Click view button on first game
    await page.getByTestId('view-button-game-draft-1').click();

    // Verify navigation to game detail page
    await expect(page).toHaveURL(/\/admin\/shared-games\/game-draft-1/);
  });

  test('should show empty state when no games match filter', async ({ page }) => {
    // Override mock to return empty for Archived status
    await page.route(`${API_BASE}/api/v1/admin/shared-games*status=3*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [],
          total: 0,
          page: 1,
          pageSize: 20,
        }),
      });
    });

    await page.goto('/editor/dashboard');

    // Wait for initial load
    await expect(page.getByTestId('games-table')).toBeVisible();

    // Click on Archived filter
    await page.getByTestId('stats-card-archiviati').click();

    // Verify empty state is shown
    await expect(page.getByTestId('empty-state')).toBeVisible();
  });
});

test.describe('Editor Dashboard - Access Control', () => {
  test('should deny access to regular User role', async ({ page }) => {
    // Setup auth as regular User (not Editor)
    await setupMockAuth(page, 'User', 'user@meepleai.dev');

    await page.goto('/editor/dashboard');

    // Should show access denied or redirect
    // The RequireRole component should handle this
    await expect(page.getByText(/accesso negato|access denied|non autorizzato/i)).toBeVisible({
      timeout: 10000,
    });
  });

  test('should allow access to Admin role', async ({ page }) => {
    // Setup auth as Admin
    await setupMockAuth(page, 'Admin', 'admin@meepleai.dev');

    // Also need to mock the shared-games endpoint for Admin
    await page.route(`${API_BASE}/api/v1/admin/shared-games`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: MOCK_EDITOR_GAMES,
          total: MOCK_EDITOR_GAMES.length,
          page: 1,
          pageSize: 20,
        }),
      });
    });

    await page.goto('/editor/dashboard');

    // Admin should see the dashboard
    await expect(page.getByTestId('dashboard-title')).toBeVisible();
    await expect(page.getByTestId('games-table')).toBeVisible();
  });
});
