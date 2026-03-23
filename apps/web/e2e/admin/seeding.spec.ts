/**
 * E2E Tests: Admin Seeding Page
 * Task 11: Seeding & Enrichment page E2E Playwright tests
 *
 * Tests the admin seeding dashboard including:
 * - Page load with heading and game count
 * - Table columns and data rendering
 * - Status filter and search
 * - Select all checkbox
 * - Enrich button with enrichable count
 * - Error message visibility for failed games
 * - Column sorting
 * - Quick action links per game
 * - Pipeline indicator stages
 * - Next-steps banner when all games complete
 *
 * Uses mock auth and API route interception (no real backend needed).
 *
 * Run: pnpm test:e2e apps/web/e2e/admin/seeding.spec.ts
 */

import { loginAsAdmin } from '../fixtures/auth';
import { test, expect } from '../test';

// =============================================================================
// Mock Data
// =============================================================================

const MOCK_SEEDING_GAMES = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    bggId: 13,
    title: 'Catan',
    gameDataStatus: 5,
    gameDataStatusName: 'Complete',
    gameStatus: 2,
    gameStatusName: 'Published',
    hasUploadedPdf: true,
    isRagReady: true,
    errorMessage: null,
    createdAt: '2026-03-23T10:00:00Z',
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    bggId: 266192,
    title: 'Wingspan',
    gameDataStatus: 5,
    gameDataStatusName: 'Complete',
    gameStatus: 2,
    gameStatusName: 'Published',
    hasUploadedPdf: false,
    isRagReady: false,
    errorMessage: null,
    createdAt: '2026-03-23T11:00:00Z',
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    bggId: 230802,
    title: 'Azul',
    gameDataStatus: 0,
    gameDataStatusName: 'Skeleton',
    gameStatus: 0,
    gameStatusName: 'Draft',
    hasUploadedPdf: false,
    isRagReady: false,
    errorMessage: null,
    createdAt: '2026-03-23T12:00:00Z',
  },
  {
    id: '44444444-4444-4444-4444-444444444444',
    bggId: 104162,
    title: 'Descent',
    gameDataStatus: 6,
    gameDataStatusName: 'Failed',
    gameStatus: 0,
    gameStatusName: 'Draft',
    hasUploadedPdf: false,
    isRagReady: false,
    errorMessage: 'BGG API timeout after 30s',
    createdAt: '2026-03-23T13:00:00Z',
  },
];

const MOCK_QUEUE_STATUS = {
  totalQueued: 0,
  totalProcessing: 0,
  items: [],
};

// =============================================================================
// Tests
// =============================================================================

test.describe('Admin Seeding Page', () => {
  test.beforeEach(async ({ page }) => {
    // Setup mock auth (skipNavigation=true so we can add more routes)
    await loginAsAdmin(page, true);

    // Mock seeding status endpoint
    await page.route('**/api/v1/admin/shared-games/seeding-status', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_SEEDING_GAMES),
      })
    );

    // Mock BGG queue status (for QueueStatusPanel)
    await page.route('**/api/v1/admin/bgg-queue/status', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_QUEUE_STATUS),
      })
    );

    // Mock SSE endpoint to avoid connection errors
    await page.route('**/api/v1/admin/bgg-queue/events**', route =>
      route.fulfill({
        status: 200,
        contentType: 'text/event-stream',
        body: '',
      })
    );

    await page.goto('/admin/shared-games/seeding');
    await page.waitForSelector('h1');
  });

  // -------------------------------------------------------------------------
  // 1. Page loads with correct heading and game count
  // -------------------------------------------------------------------------
  test('displays page heading and game count', async ({ page }) => {
    // Verify the page heading
    const heading = page.locator('h1');
    await expect(heading).toContainText('Seeding');

    // Verify game count in the card title
    const cardTitle = page.locator('text=Games (4)');
    await expect(cardTitle).toBeVisible({ timeout: 8000 });
  });

  // -------------------------------------------------------------------------
  // 2. Table shows all expected columns
  // -------------------------------------------------------------------------
  test('shows all table columns', async ({ page }) => {
    const columnHeaders = [
      'Title',
      'BGG ID',
      'Data Status',
      'Has PDF',
      'Game Status',
      'RAG Ready',
      'Pipeline',
      'Created',
      'Actions',
    ];

    for (const header of columnHeaders) {
      const th = page.locator('th', { hasText: header });
      await expect(th).toBeVisible({ timeout: 5000 });
    }
  });

  // -------------------------------------------------------------------------
  // 3. Renders all 4 mock games with correct data
  // -------------------------------------------------------------------------
  test('renders mock games with correct data', async ({ page }) => {
    // All 4 game titles should be visible
    await expect(page.locator('td', { hasText: 'Catan' })).toBeVisible();
    await expect(page.locator('td', { hasText: 'Wingspan' })).toBeVisible();
    await expect(page.locator('td', { hasText: 'Azul' })).toBeVisible();
    await expect(page.locator('td', { hasText: 'Descent' })).toBeVisible();

    // Catan should show Complete badge
    const catanRow = page.locator('tr', { hasText: 'Catan' });
    await expect(catanRow.locator('text=Complete')).toBeVisible();

    // Azul should show Skeleton badge
    const azulRow = page.locator('tr', { hasText: 'Azul' });
    await expect(azulRow.locator('text=Skeleton')).toBeVisible();

    // Descent should show Failed badge
    const descentRow = page.locator('tr', { hasText: 'Descent' });
    await expect(descentRow.locator('text=Failed')).toBeVisible();
  });

  // -------------------------------------------------------------------------
  // 4. Status filter narrows games
  // -------------------------------------------------------------------------
  test('filters games by status', async ({ page }) => {
    // Open the status filter dropdown
    const filterTrigger = page.locator('button[role="combobox"]', {
      hasText: /All Statuses/,
    });
    await filterTrigger.click();

    // Select "Skeleton" option
    const skeletonOption = page.locator('[role="option"]', { hasText: 'Skeleton' });
    await skeletonOption.click();

    // Only Azul should be visible (Skeleton status)
    await expect(page.locator('td', { hasText: 'Azul' })).toBeVisible();
    await expect(page.locator('td', { hasText: 'Catan' })).not.toBeVisible();
    await expect(page.locator('td', { hasText: 'Wingspan' })).not.toBeVisible();
    await expect(page.locator('td', { hasText: 'Descent' })).not.toBeVisible();

    // Game count should update
    await expect(page.locator('text=Games (1)')).toBeVisible();

    // Reset to "All Statuses"
    const currentTrigger = page.locator('button[role="combobox"]', {
      hasText: /Skeleton/,
    });
    await currentTrigger.click();
    const allOption = page.locator('[role="option"]', { hasText: 'All Statuses' });
    await allOption.click();

    // All 4 games should be visible again
    await expect(page.locator('text=Games (4)')).toBeVisible();
  });

  // -------------------------------------------------------------------------
  // 5. Search filters by title
  // -------------------------------------------------------------------------
  test('search filters by title', async ({ page }) => {
    const searchInput = page.locator('input[placeholder*="Search"]');
    await expect(searchInput).toBeVisible();

    // Type "Cat" to filter
    await searchInput.fill('Cat');

    // Only Catan should be visible
    await expect(page.locator('td', { hasText: 'Catan' })).toBeVisible();
    await expect(page.locator('td', { hasText: 'Wingspan' })).not.toBeVisible();
    await expect(page.locator('td', { hasText: 'Azul' })).not.toBeVisible();
    await expect(page.locator('td', { hasText: 'Descent' })).not.toBeVisible();

    // Game count should update
    await expect(page.locator('text=Games (1)')).toBeVisible();

    // Clear the search
    await searchInput.clear();

    // All 4 games should be visible again
    await expect(page.locator('text=Games (4)')).toBeVisible();
  });

  // -------------------------------------------------------------------------
  // 6. Select all checkbox works
  // -------------------------------------------------------------------------
  test('select all checkbox selects visible games', async ({ page }) => {
    // Find the "Select all" checkbox in the table header
    const selectAllCheckbox = page
      .locator('th')
      .locator('button[role="checkbox"][aria-label="Select all"]');
    await expect(selectAllCheckbox).toBeVisible();

    // Click select all
    await selectAllCheckbox.click();

    // All 4 row checkboxes should be checked
    const rowCheckboxes = page.locator('td').locator('button[role="checkbox"]');
    const count = await rowCheckboxes.count();
    expect(count).toBe(4);

    for (let i = 0; i < count; i++) {
      await expect(rowCheckboxes.nth(i)).toHaveAttribute('data-state', 'checked');
    }

    // Click select all again to deselect
    await selectAllCheckbox.click();

    for (let i = 0; i < count; i++) {
      await expect(rowCheckboxes.nth(i)).toHaveAttribute('data-state', 'unchecked');
    }
  });

  // -------------------------------------------------------------------------
  // 7. Enrich button shows count for enrichable games
  // -------------------------------------------------------------------------
  test('enrich button shows enrichable count when skeleton/failed selected', async ({ page }) => {
    // Initially the Enrich button should be disabled with no count
    const enrichButton = page.locator('button', { hasText: /Enrich Selected/ });
    await expect(enrichButton).toBeVisible();
    await expect(enrichButton).toBeDisabled();

    // Select Azul (Skeleton) by clicking its checkbox
    const azulCheckbox = page.locator('button[role="checkbox"][aria-label="Select Azul"]');
    await azulCheckbox.click();

    // Select Descent (Failed) by clicking its checkbox
    const descentCheckbox = page.locator('button[role="checkbox"][aria-label="Select Descent"]');
    await descentCheckbox.click();

    // Enrich button should now show count and be enabled
    await expect(page.locator('button', { hasText: 'Enrich Selected (2)' })).toBeVisible();
    await expect(page.locator('button', { hasText: 'Enrich Selected (2)' })).toBeEnabled();

    // Deselect both
    await azulCheckbox.click();
    await descentCheckbox.click();

    // Enrich button should be disabled again
    await expect(enrichButton).toBeDisabled();
  });

  // -------------------------------------------------------------------------
  // 8. Failed game shows error message
  // -------------------------------------------------------------------------
  test('failed game shows error message inline', async ({ page }) => {
    // Descent row should contain the error message
    const descentRow = page.locator('tr', { hasText: 'Descent' });
    await expect(descentRow).toBeVisible();

    // Error message should be visible in the row
    await expect(descentRow.locator('text=BGG API timeout')).toBeVisible();
  });

  // -------------------------------------------------------------------------
  // 9. Column sorting works
  // -------------------------------------------------------------------------
  test('column sorting toggles direction', async ({ page }) => {
    // Default sort is by Title ascending, so first game should be Azul (alphabetically)
    const firstRow = page.locator('tbody tr').first();
    await expect(firstRow).toContainText('Azul');

    // Click Title header to toggle to descending
    const titleHeader = page.locator('th', { hasText: 'Title' });
    await titleHeader.click();

    // Now first game should be Wingspan (reverse alphabetical)
    const firstRowAfter = page.locator('tbody tr').first();
    await expect(firstRowAfter).toContainText('Wingspan');

    // Click again to go back to ascending
    await titleHeader.click();
    const firstRowReset = page.locator('tbody tr').first();
    await expect(firstRowReset).toContainText('Azul');
  });

  // -------------------------------------------------------------------------
  // 10. Quick action links present
  // -------------------------------------------------------------------------
  test('shows quick action links per game', async ({ page }) => {
    // Wingspan is Complete with no PDF, so it should have the upload PDF link
    const wingspanRow = page.locator('tr', { hasText: 'Wingspan' });
    await expect(wingspanRow).toBeVisible();

    // Upload PDF link for Wingspan (Complete + no PDF)
    const uploadLink = wingspanRow.locator(
      'a[href*="/admin/knowledge-base/upload?gameId=22222222"]'
    );
    await expect(uploadLink).toBeVisible();

    // All games should have the "View details" external link
    const catanRow = page.locator('tr', { hasText: 'Catan' });
    const catanDetailsLink = catanRow.locator('a[href*="/admin/shared-games/11111111"]');
    await expect(catanDetailsLink).toBeVisible();

    // Azul (Skeleton) should NOT have upload PDF link (not Complete)
    const azulRow = page.locator('tr', { hasText: 'Azul' });
    const azulUploadLink = azulRow.locator('a[href*="/admin/knowledge-base/upload"]');
    await expect(azulUploadLink).not.toBeVisible();
  });

  // -------------------------------------------------------------------------
  // 11. Pipeline indicator shows correct stages
  // -------------------------------------------------------------------------
  test('pipeline indicator reflects game state', async ({ page }) => {
    // Catan (Complete + PDF + RAG) — all 3 dots should be green (emerald)
    const catanRow = page.locator('tr', { hasText: 'Catan' });
    const catanPipeline = catanRow.locator('div[title*="Enriched: Yes"]');
    await expect(catanPipeline).toBeVisible();
    // Verify the title indicates all stages complete
    await expect(catanPipeline).toHaveAttribute('title', 'Enriched: Yes | PDF: Yes | RAG: Yes');

    // Wingspan (Complete + no PDF + no RAG) — only enriched dot green
    const wingspanRow = page.locator('tr', { hasText: 'Wingspan' });
    const wingspanPipeline = wingspanRow.locator('div[title*="Enriched: Yes"]');
    await expect(wingspanPipeline).toBeVisible();
    await expect(wingspanPipeline).toHaveAttribute('title', 'Enriched: Yes | PDF: No | RAG: No');

    // Azul (Skeleton + no PDF + no RAG) — all gray
    const azulRow = page.locator('tr', { hasText: 'Azul' });
    const azulPipeline = azulRow.locator('div[title*="Enriched: No"]');
    await expect(azulPipeline).toBeVisible();
    await expect(azulPipeline).toHaveAttribute('title', 'Enriched: No | PDF: No | RAG: No');
  });

  // -------------------------------------------------------------------------
  // 12. Next-steps banner appears when all Complete
  // -------------------------------------------------------------------------
  test('shows next-steps banner when all games complete', async ({ page }) => {
    // Re-mock the seeding endpoint with all games as Complete
    const allCompleteGames = MOCK_SEEDING_GAMES.map(g => ({
      ...g,
      gameDataStatus: 5,
      gameDataStatusName: 'Complete',
      errorMessage: null,
    }));

    await page.route('**/api/v1/admin/shared-games/seeding-status', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(allCompleteGames),
      })
    );

    // Navigate again to pick up the new mock
    await page.goto('/admin/shared-games/seeding');
    await page.waitForSelector('h1');

    // The next-steps banner should be visible
    const banner = page.locator('text=All games are enriched');
    await expect(banner).toBeVisible({ timeout: 8000 });

    // Should contain link to Upload & Process
    const uploadLink = page.locator('a[href="/admin/knowledge-base/upload"]', {
      hasText: /Upload.*Process/,
    });
    await expect(uploadLink).toBeVisible();
  });
});
