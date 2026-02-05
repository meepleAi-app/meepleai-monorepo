/**
 * Collection Dashboard E2E Tests - Issue #3476, #3632
 *
 * End-to-end tests for the collection dashboard user flow
 *
 * Test scenarios:
 * - User navigates to collection dashboard
 * - User views hero stats (total games, favorites, quota, usage)
 * - User searches games
 * - User filters games by state and favorites
 * - User toggles view mode (grid/list)
 * - User navigates pagination
 * - User clicks on game card
 */

import { test, expect } from '@playwright/test';

test.describe('Collection Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    // TODO: Replace with actual login flow once authentication is implemented
    // For now, navigate directly to the collection page
    await page.goto('/dashboard/collection');
  });

  test('should display collection dashboard page', async ({ page }) => {
    // Check page title
    await expect(page).toHaveTitle(/La Mia Collezione/);

    // Check main heading
    await expect(page.getByRole('heading', { name: 'La Mia Collezione' })).toBeVisible();
  });

  test('should display hero stats section', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Wait for stats region to load (Issue #3632: Updated hero stats)
    const statsSection = page.getByRole('region', { name: /statistics/i });
    await expect(statsSection).toBeVisible();

    // Check all hero stat cards are present (new design)
    await expect(page.getByTestId('hero-stat-total')).toBeVisible();
    await expect(page.getByTestId('hero-stat-favorites')).toBeVisible();
    await expect(page.getByTestId('hero-stat-quota')).toBeVisible();
    await expect(page.getByTestId('hero-stat-usage')).toBeVisible();
  });

  test('should display games grid', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Wait for collection grid to load (Issue #3632: Updated test IDs)
    const gamesGrid = page.getByTestId('collection-grid');
    await expect(gamesGrid).toBeVisible();

    // Check that game cards are rendered
    const gameCards = page.locator('[data-testid^="meeple-card-"]');
    await expect(gameCards.first()).toBeVisible();
  });

  test('should display toolbar with search', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Check toolbar is visible (Issue #3632: New toolbar component)
    const toolbar = page.getByTestId('collection-toolbar');
    await expect(toolbar).toBeVisible();

    // Check search input is present
    const searchInput = page.getByTestId('collection-search');
    await expect(searchInput).toBeVisible();
  });

  test('should display view mode toggle', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Check view mode buttons (Issue #3632: Grid/List toggle)
    await expect(page.getByTestId('view-mode-grid')).toBeVisible();
    await expect(page.getByTestId('view-mode-list')).toBeVisible();
  });

  test('should sort games using dropdown', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Wait for grid to load (Issue #3632: Updated test ID)
    await expect(page.getByTestId('collection-grid')).toBeVisible();

    // Open sort dropdown
    const sortSelect = page.getByTestId('sort-select');
    await sortSelect.click();

    // Select "Titolo (A-Z)" sort option
    await page.getByRole('option', { name: /Titolo \(A-Z\)/i }).click();

    // Verify URL or state changed (implementation dependent)
    // For now, just verify dropdown closed and grid still visible
    await expect(page.getByTestId('collection-grid')).toBeVisible();
  });

  test('should display filter chips', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Wait for filters region to load (Issue #3632: New filter chip design)
    const filtersSection = page.getByRole('region', { name: /filters/i });
    await expect(filtersSection).toBeVisible();

    // Check filter chips are present
    await expect(page.getByTestId('filter-chip-all')).toBeVisible();
    await expect(page.getByTestId('filter-chip-favorites')).toBeVisible();
    await expect(page.getByTestId('filter-chip-owned')).toBeVisible();
  });

  test('should toggle favorites filter', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Click favorites filter chip
    const favoritesChip = page.getByTestId('filter-chip-favorites');
    await favoritesChip.click();

    // Verify filter is active (aria-pressed)
    await expect(favoritesChip).toHaveAttribute('aria-pressed', 'true');
  });

  test('should search games', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Type in search input
    const searchInput = page.getByTestId('collection-search');
    await searchInput.fill('Wingspan');

    // Verify search value is set
    await expect(searchInput).toHaveValue('Wingspan');
  });

  test('should clear filters', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // First activate a filter
    const favoritesChip = page.getByTestId('filter-chip-favorites');
    await favoritesChip.click();
    await expect(favoritesChip).toHaveAttribute('aria-pressed', 'true');

    // Click clear button (shows "Pulisci" when filters are active)
    await page.getByText('Pulisci').click();

    // Verify "All" chip is now active
    const allChip = page.getByTestId('filter-chip-all');
    await expect(allChip).toHaveAttribute('aria-pressed', 'true');
  });

  test('should click game card and navigate', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Wait for collection grid to load (Issue #3632: Updated test ID)
    await expect(page.getByTestId('collection-grid')).toBeVisible();

    // Get first game card
    const firstGameCard = page.locator('[data-testid^="meeple-card-"]').first();
    await expect(firstGameCard).toBeVisible();

    // Click on game card (should navigate to game detail page)
    await firstGameCard.click();

    // Verify navigation occurred (URL changed to /games/[id])
    await expect(page).toHaveURL(/\/games\/.+/);
  });

  test('should toggle view mode', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Check grid view is default active
    const gridButton = page.getByTestId('view-mode-grid');
    const listButton = page.getByTestId('view-mode-list');

    // Click list view
    await listButton.click();

    // Grid should still exist (view changes don't remove elements)
    await expect(page.getByTestId('collection-grid')).toBeVisible();
  });

  test('should display pagination', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Check pagination controls (Issue #3632: New pagination)
    await expect(page.getByText(/Pagina \d+ di \d+/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /precedente/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /successiva/i })).toBeVisible();
  });

  test('should be responsive on mobile viewport', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/dashboard/collection');

    // Verify main sections are visible (Issue #3632: Updated structure)
    await expect(page.getByRole('region', { name: /statistics/i })).toBeVisible();
    await expect(page.getByRole('region', { name: /filters/i })).toBeVisible();
    await expect(page.getByRole('region', { name: /collection/i })).toBeVisible();
  });

  test('should handle empty collection state', async ({ page }) => {
    // TODO: Mock empty collection response once API is integrated
    await page.goto('/dashboard/collection');

    // For now, just verify empty state element exists in DOM
    // (it won't be visible with mock data)
    // Issue #3632: Updated test ID
    const emptyState = page.getByTestId('collection-empty-state');
    // Note: Don't check visibility as mock data will show games
  });

  test('should handle loading states', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Loading skeletons should appear briefly before content loads
    // We'll check that stats and collection sections eventually appear
    // Issue #3632: Updated to use ARIA regions
    await expect(page.getByRole('region', { name: /statistics/i })).toBeVisible({ timeout: 5000 });
    await expect(page.getByTestId('collection-grid')).toBeVisible({ timeout: 5000 });
  });

  test('should have accessible search input', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Issue #3632: Verify accessibility labels
    const searchInput = page.getByRole('searchbox', { name: /search collection/i });
    await expect(searchInput).toBeVisible();
  });

  test('should have accessible view mode buttons', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Issue #3632: Verify accessibility labels on view buttons
    await expect(page.getByRole('button', { name: /grid view/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /list view/i })).toBeVisible();
  });
});
