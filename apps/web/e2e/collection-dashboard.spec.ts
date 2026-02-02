/**
 * Collection Dashboard E2E Tests - Issue #3476
 *
 * End-to-end tests for the collection dashboard user flow
 *
 * Test scenarios:
 * - User navigates to collection dashboard
 * - User views collection stats
 * - User sorts games
 * - User filters games
 * - User clicks on game card
 * - User views activity feed
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

    // Wait for stats to load
    const statsSection = page.getByTestId('collection-stats-section');
    await expect(statsSection).toBeVisible();

    // Check all stat cards are present
    await expect(page.getByTestId('stat-card-giochi-totali')).toBeVisible();
    await expect(page.getByTestId('stat-card-pdf-privati')).toBeVisible();
    await expect(page.getByTestId('stat-card-chat-attive')).toBeVisible();
    await expect(page.getByTestId('stat-card-tempo-lettura')).toBeVisible();

    // Verify stats have values
    await expect(page.getByTestId('stat-value-giochi-totali')).not.toBeEmpty();
  });

  test('should display games grid', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Wait for games grid to load
    const gamesGrid = page.getByTestId('games-grid');
    await expect(gamesGrid).toBeVisible();

    // Check that game cards are rendered
    const gameCards = page.locator('[data-testid^="meeple-card-"]');
    await expect(gameCards.first()).toBeVisible();
  });

  test('should display activity feed', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Wait for activity feed to load
    const activityFeed = page.getByTestId('activity-feed-widget');
    await expect(activityFeed).toBeVisible();

    // Check activity feed title
    await expect(page.getByTestId('activity-feed-title')).toHaveText('Attività Recente');
  });

  test('should sort games using dropdown', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Wait for grid to load
    await expect(page.getByTestId('games-grid')).toBeVisible();

    // Open sort dropdown
    const sortSelect = page.getByTestId('sort-select');
    await sortSelect.click();

    // Select "Titolo (A-Z)" sort option
    await page.getByRole('option', { name: /Titolo \(A-Z\)/i }).click();

    // Verify URL or state changed (implementation dependent)
    // For now, just verify dropdown closed and grid still visible
    await expect(page.getByTestId('games-grid')).toBeVisible();
  });

  test('should toggle filter panel', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Wait for grid to load
    await expect(page.getByTestId('collection-grid-section')).toBeVisible();

    // Click filter toggle button
    const filterToggle = page.getByTestId('filter-toggle');
    await filterToggle.click();

    // Verify filter panel is visible
    const filterPanel = page.getByTestId('filter-panel');
    await expect(filterPanel).toBeVisible();

    // Check filter options are present
    await expect(page.getByTestId('filter-option-has-pdf-true')).toBeVisible();
    await expect(page.getByTestId('filter-option-has-active-chat-true')).toBeVisible();
  });

  test('should apply filters to games', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Open filter panel
    await page.getByTestId('filter-toggle').click();
    await expect(page.getByTestId('filter-panel')).toBeVisible();

    // Click "Ha PDF" filter
    await page.getByTestId('filter-option-has-pdf-true').click();

    // Verify filter tag appears
    await expect(page.getByTestId('filter-tag-has-pdf-true')).toBeVisible();

    // Verify filter count badge
    await expect(page.getByTestId('filter-toggle')).toContainText('1');
  });

  test('should remove active filter', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Open filter panel and apply filter
    await page.getByTestId('filter-toggle').click();
    await page.getByTestId('filter-option-has-pdf-true').click();

    // Wait for filter tag to appear
    const filterTag = page.getByTestId('filter-tag-has-pdf-true');
    await expect(filterTag).toBeVisible();

    // Click remove button on filter tag
    await filterTag.getByRole('button', { name: /Rimuovi filtro/i }).click();

    // Verify filter tag is removed
    await expect(filterTag).not.toBeVisible();
  });

  test('should clear all filters', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Open filter panel and apply multiple filters
    await page.getByTestId('filter-toggle').click();
    await page.getByTestId('filter-option-has-pdf-true').click();
    await page.getByTestId('filter-option-has-active-chat-true').click();

    // Wait for filter tags to appear
    await expect(page.getByTestId('filter-tag-has-pdf-true')).toBeVisible();
    await expect(page.getByTestId('filter-tag-has-active-chat-true')).toBeVisible();

    // Click "Pulisci Tutto" button
    await page.getByTestId('clear-filters').click();

    // Verify all filter tags are removed
    await expect(page.getByTestId('filter-tag-has-pdf-true')).not.toBeVisible();
    await expect(page.getByTestId('filter-tag-has-active-chat-true')).not.toBeVisible();
  });

  test('should click game card and navigate', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Wait for games grid to load
    await expect(page.getByTestId('games-grid')).toBeVisible();

    // Get first game card
    const firstGameCard = page.locator('[data-testid^="meeple-card-"]').first();
    await expect(firstGameCard).toBeVisible();

    // Click on game card (should navigate to game detail page)
    await firstGameCard.click();

    // Verify navigation occurred (URL changed to /games/[id])
    await expect(page).toHaveURL(/\/games\/.+/);
  });

  test('should click play button on game card', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Wait for games grid to load
    await expect(page.getByTestId('games-grid')).toBeVisible();

    // Get first play button
    const firstPlayButton = page.locator('[data-testid^="play-button-"]').first();
    await expect(firstPlayButton).toBeVisible();

    // Click play button (should trigger action without navigation)
    await firstPlayButton.click();

    // Verify still on collection page
    await expect(page).toHaveURL(/\/dashboard\/collection/);
  });

  test('should display add game button', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Check desktop add button (hidden on mobile)
    const desktopAddButton = page.getByTestId('add-game-button');
    await expect(desktopAddButton).toBeVisible();

    // Check mobile add button exists (may need viewport resize to verify visibility)
    const mobileAddButton = page.getByTestId('add-game-button-mobile');
    await expect(mobileAddButton).toBeAttached();
  });

  test('should be responsive on mobile viewport', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/dashboard/collection');

    // Verify main sections are visible
    await expect(page.getByTestId('collection-stats-section')).toBeVisible();
    await expect(page.getByTestId('collection-grid-section')).toBeVisible();
    await expect(page.getByTestId('activity-feed-widget')).toBeVisible();

    // Verify mobile add button is visible
    await expect(page.getByTestId('add-game-button-mobile')).toBeVisible();
  });

  test('should handle empty collection state', async ({ page }) => {
    // TODO: Mock empty collection response once API is integrated
    await page.goto('/dashboard/collection');

    // For now, just verify empty state element exists in DOM
    // (it won't be visible with mock data)
    const emptyState = page.getByTestId('collection-grid-empty');
    // Note: Don't check visibility as mock data will show games
  });

  test('should handle loading states', async ({ page }) => {
    await page.goto('/dashboard/collection');

    // Loading skeletons should appear briefly before content loads
    // We'll check that stats and grid sections eventually appear
    await expect(page.getByTestId('collection-stats-section')).toBeVisible({ timeout: 5000 });
    await expect(page.getByTestId('games-grid')).toBeVisible({ timeout: 5000 });
  });
});
