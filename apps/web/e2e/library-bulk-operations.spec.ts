/**
 * E2E Tests for Library Bulk Operations (Issue #2613)
 *
 * Tests bulk selection workflows:
 * - Select 3 games → bulk favorite
 * - Select all → bulk remove
 * - Keyboard range selection (Shift+Click)
 */

import { test, expect } from './test';
import { Page } from '@playwright/test';

// Issue #841: Make API_BASE configurable via environment variables
const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Sample library games for testing bulk operations
const createMockLibraryGames = (count: number) => {
  return Array.from({ length: count }, (_, i) => ({
    id: `lib-entry-${i + 1}`,
    gameId: `game-${i + 1}`,
    gameTitle: `Test Game ${i + 1}`,
    gameImageUrl: null,
    gamePublisher: `Publisher ${i + 1}`,
    isFavorite: false,
    notes: null,
    addedAt: new Date(Date.now() - i * 86400000).toISOString(),
    updatedAt: new Date().toISOString(),
  }));
};

// Setup mock routes for library bulk operations
async function setupLibraryBulkMocks(page: Page, gameCount: number = 5) {
  const mockGames = createMockLibraryGames(gameCount);

  // Mock library endpoint
  await page.route(`${API_BASE}/api/v1/library**`, async route => {
    const method = route.request().method();

    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: mockGames,
          totalCount: mockGames.length,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        }),
      });
    } else if (method === 'PUT' || method === 'PATCH') {
      // Update library entry (favorite toggle)
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    } else if (method === 'DELETE') {
      // Remove from library
      await route.fulfill({
        status: 204,
      });
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

  // Mock agent config endpoint (for UserGameCard)
  await page.route(`${API_BASE}/api/v1/agents/config**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(null),
    });
  });

  return mockGames;
}

test.describe('Library Bulk Operations (Issue #2613)', () => {
  test('should select 3 games and mark as bulk favorite', async ({ userPage }) => {
    const mockGames = await setupLibraryBulkMocks(userPage, 5);

    // Navigate to library
    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Click "Seleziona" button to enter selection mode
    const selectButton = userPage.locator('button:has-text("Seleziona")');
    await expect(selectButton).toBeVisible();
    await selectButton.click();

    // Verify selection mode is active (button text changes)
    await expect(userPage.locator('button:has-text("Annulla Selezione")')).toBeVisible();

    // Select first 3 game cards by clicking checkboxes
    const gameCards = userPage.locator('[data-testid="game-card"]');
    const cardCount = await gameCards.count();
    expect(cardCount).toBeGreaterThanOrEqual(3);

    // Click on the first 3 cards to select them
    for (let i = 0; i < 3; i++) {
      const card = gameCards.nth(i);
      await card.click();
    }

    // Verify bulk action bar appears with correct count
    const bulkActionBar = userPage.locator('text=3 selezionati');
    await expect(bulkActionBar).toBeVisible({ timeout: 5000 });

    // Click "Preferiti" button (desktop) or heart icon (mobile)
    const favoriteButton = userPage
      .locator('button:has-text("Preferiti")')
      .or(userPage.locator('button[aria-label="Segna come preferiti"]'));
    await expect(favoriteButton.first()).toBeVisible();
    await favoriteButton.first().click();

    // Verify success toast appears
    await expect(
      userPage.locator('text=/\\d+ giochi segnati come preferiti/')
    ).toBeVisible({ timeout: 5000 });

    // Verify selection is cleared after bulk action
    await expect(userPage.locator('text=3 selezionati')).not.toBeVisible({ timeout: 3000 });
  });

  test('should select all games and perform bulk remove', async ({ userPage }) => {
    const mockGames = await setupLibraryBulkMocks(userPage, 4);

    // Navigate to library
    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Enter selection mode
    await userPage.locator('button:has-text("Seleziona")').click();
    await expect(userPage.locator('button:has-text("Annulla Selezione")')).toBeVisible();

    // Select first game to make bulk action bar appear
    await userPage.locator('[data-testid="game-card"]').first().click();

    // Wait for bulk action bar to appear
    await expect(userPage.locator('text=1 selezionati')).toBeVisible({ timeout: 5000 });

    // Click "Seleziona tutti" button
    const selectAllButton = userPage.locator('button:has-text("Seleziona tutti")');
    await expect(selectAllButton).toBeVisible();
    await selectAllButton.click();

    // Verify all games are selected
    await expect(userPage.locator(`text=${mockGames.length} selezionati`)).toBeVisible({
      timeout: 5000,
    });

    // Click "Rimuovi" button to open confirmation dialog
    const removeButton = userPage
      .locator('button:has-text("Rimuovi")')
      .or(userPage.locator('button[aria-label="Rimuovi"]'));
    await expect(removeButton.first()).toBeVisible();
    await removeButton.first().click();

    // Verify confirmation dialog appears
    await expect(userPage.locator(`text=Rimuovi ${mockGames.length} giochi`)).toBeVisible({
      timeout: 5000,
    });
    await expect(userPage.locator('text=Questa azione non può essere annullata')).toBeVisible();

    // Verify game titles are shown in dialog
    for (let i = 0; i < Math.min(5, mockGames.length); i++) {
      await expect(userPage.locator(`text=${mockGames[i].gameTitle}`)).toBeVisible();
    }

    // Confirm removal
    const confirmButton = userPage.locator(
      `button:has-text("Rimuovi ${mockGames.length} giochi")`
    );
    await expect(confirmButton).toBeVisible();
    await confirmButton.click();

    // Verify success toast appears
    await expect(
      userPage.locator('text=/\\d+ giochi rimossi dalla libreria/')
    ).toBeVisible({ timeout: 5000 });

    // Verify dialog is closed and selection is cleared
    await expect(userPage.locator(`text=Rimuovi ${mockGames.length} giochi`)).not.toBeVisible({
      timeout: 3000,
    });
  });

  test('should support keyboard range selection with Shift+Click', async ({ userPage }) => {
    await setupLibraryBulkMocks(userPage, 6);

    // Navigate to library
    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Enter selection mode
    await userPage.locator('button:has-text("Seleziona")').click();
    await expect(userPage.locator('button:has-text("Annulla Selezione")')).toBeVisible();

    const gameCards = userPage.locator('[data-testid="game-card"]');
    const cardCount = await gameCards.count();
    expect(cardCount).toBeGreaterThanOrEqual(5);

    // Select first card (normal click)
    await gameCards.nth(0).click();

    // Verify 1 card selected
    await expect(userPage.locator('text=1 selezionati')).toBeVisible({ timeout: 5000 });

    // Shift+Click on 5th card to select range (cards 1-5)
    await gameCards.nth(4).click({ modifiers: ['Shift'] });

    // Verify 5 cards are now selected (range selection)
    await expect(userPage.locator('text=5 selezionati')).toBeVisible({ timeout: 5000 });

    // Verify visual indication on all selected cards (ring-2 class)
    for (let i = 0; i < 5; i++) {
      const card = gameCards.nth(i);
      await expect(card).toHaveClass(/ring-2/);
    }

    // Verify 6th card is NOT selected
    const sixthCard = gameCards.nth(5);
    await expect(sixthCard).not.toHaveClass(/ring-2/);

    // Click on a different card with Shift to extend range
    await gameCards.nth(5).click({ modifiers: ['Shift'] });

    // Verify all 6 cards are now selected
    await expect(userPage.locator('text=6 selezionati')).toBeVisible({ timeout: 5000 });
  });

  test('should toggle selection mode and clear selection on exit', async ({ userPage }) => {
    await setupLibraryBulkMocks(userPage, 3);

    // Navigate to library
    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Enter selection mode
    await userPage.locator('button:has-text("Seleziona")').click();

    // Select 2 games
    const gameCards = userPage.locator('[data-testid="game-card"]');
    await gameCards.nth(0).click();
    await gameCards.nth(1).click();

    // Verify selection count
    await expect(userPage.locator('text=2 selezionati')).toBeVisible({ timeout: 5000 });

    // Exit selection mode
    await userPage.locator('button:has-text("Annulla Selezione")').click();

    // Verify selection mode exited
    await expect(userPage.locator('button:has-text("Seleziona")')).toBeVisible();

    // Verify bulk action bar is hidden
    await expect(userPage.locator('text=2 selezionati')).not.toBeVisible({ timeout: 3000 });

    // Re-enter selection mode
    await userPage.locator('button:has-text("Seleziona")').click();

    // Verify no cards are selected (selection was cleared)
    // Cards should not have the selected ring
    const firstCard = gameCards.first();
    await expect(firstCard).not.toHaveClass(/ring-2/);
  });

  test('should deselect all when clicking Deseleziona tutti', async ({ userPage }) => {
    await setupLibraryBulkMocks(userPage, 4);

    // Navigate to library
    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Enter selection mode
    await userPage.locator('button:has-text("Seleziona")').click();

    // Select first game
    await userPage.locator('[data-testid="game-card"]').first().click();

    // Select all games
    await userPage.locator('button:has-text("Seleziona tutti")').click();

    // Verify 4 selected
    await expect(userPage.locator('text=4 selezionati')).toBeVisible({ timeout: 5000 });

    // Now button should say "Deseleziona tutti"
    const deselectAllButton = userPage.locator('button:has-text("Deseleziona tutti")');
    await expect(deselectAllButton).toBeVisible();
    await deselectAllButton.click();

    // Verify selection is cleared (but still in selection mode)
    // The count should disappear since no items are selected
    await expect(userPage.locator('text=4 selezionati')).not.toBeVisible({ timeout: 3000 });

    // Verify we're still in selection mode
    await expect(userPage.locator('button:has-text("Annulla Selezione")')).toBeVisible();
  });

  test('should clear selection via X button in bulk action bar', async ({ userPage }) => {
    await setupLibraryBulkMocks(userPage, 3);

    // Navigate to library
    await userPage.goto('/library');
    await userPage.waitForLoadState('networkidle');

    // Wait for game cards to load
    await expect(userPage.locator('[data-testid="game-card"]').first()).toBeVisible({
      timeout: 10000,
    });

    // Enter selection mode and select games
    await userPage.locator('button:has-text("Seleziona")').click();
    await userPage.locator('[data-testid="game-card"]').first().click();
    await userPage.locator('[data-testid="game-card"]').nth(1).click();

    // Verify bulk action bar
    await expect(userPage.locator('text=2 selezionati')).toBeVisible({ timeout: 5000 });

    // Click X button to clear selection
    const closeButton = userPage.locator('button[aria-label="Esci dalla selezione"]');
    await expect(closeButton).toBeVisible();
    await closeButton.click();

    // Verify selection is cleared and bulk action bar hidden
    await expect(userPage.locator('text=2 selezionati')).not.toBeVisible({ timeout: 3000 });

    // Verify we're back to non-selection mode
    await expect(userPage.locator('button:has-text("Seleziona")')).toBeVisible();
  });
});
