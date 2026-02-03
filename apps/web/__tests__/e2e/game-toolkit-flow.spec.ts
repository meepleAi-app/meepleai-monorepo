/**
 * Game-Specific Toolkit E2E Tests (Issue #3164 - GST-005)
 *
 * End-to-end tests for game-specific toolkit integration
 */

import { test, expect } from '@playwright/test';

test.describe('Game-Specific Toolkit', () => {
  const testGameId = 'test-game-id'; // Replace with actual game ID in test env

  test.beforeEach(async ({ page }) => {
    // Navigate to library
    await page.goto('/library');
  });

  test('should show Toolkit button on game cards', async ({ page }) => {
    // Find a game card
    const gameCard = page.locator('[data-testid="game-card"]').first();
    await expect(gameCard).toBeVisible();

    // Verify Toolkit button exists
    await expect(page.getByRole('link', { name: /toolkit/i }).first()).toBeVisible();
  });

  test('should navigate to game toolkit landing from card', async ({ page }) => {
    // Click Toolkit button on first game card
    await page.getByRole('link', { name: /toolkit/i }).first().click();

    // Verify navigation to game toolkit landing
    await page.waitForURL(/\/library\/games\/[a-f0-9-]+\/toolkit$/);

    // Verify template preview or start session UI
    await expect(page.getByRole('heading', { name: /toolkit/i })).toBeVisible();
  });

  test('should display game template preview', async ({ page }) => {
    // Assume we navigate to 7 Wonders toolkit (mock or seed data)
    await page.goto('/library/games/7-wonders-id/toolkit');

    // Verify template elements
    await expect(page.getByText(/scoring categories/i)).toBeVisible();
    await expect(page.getByText(/military|science|commerce/i)).toBeVisible();

    // Verify scoring rules
    await expect(page.getByText(/scoring rules/i)).toBeVisible();
  });

  test('should start game session with template', async ({ page }) => {
    // Navigate to game toolkit
    await page.getByRole('link', { name: /toolkit/i }).first().click();
    await page.waitForURL(/\/library\/games\/[a-f0-9-]+\/toolkit$/);

    // Fill participants
    await page.getByPlaceholder('Player 1').fill('Alice');
    await page.getByRole('button', { name: /add player/i }).click();
    await page.getByPlaceholder('Player 2').fill('Bob');

    // Start session
    await page.getByRole('button', { name: /start.*session/i }).click();

    // Wait for redirect to active session
    await page.waitForURL(/\/library\/games\/[a-f0-9-]+\/toolkit\/[a-f0-9-]+$/);

    // Verify participants visible
    await expect(page.getByText('Alice')).toBeVisible();
    await expect(page.getByText('Bob')).toBeVisible();
  });

  test('should pre-fill categories from template', async ({ page }) => {
    // Start a session (following previous test flow)
    await page.getByRole('link', { name: /toolkit/i }).first().click();
    await page.waitForURL(/\/library\/games\/[a-f0-9-]+\/toolkit$/);

    await page.getByPlaceholder('Player 1').fill('Player1');
    await page.getByRole('button', { name: /start.*session/i }).click();

    await page.waitForURL(/\/library\/games\/[a-f0-9-]+\/toolkit\/[a-f0-9-]+$/);

    // Check if ScoreInput has category dropdown
    const categorySelect = page.getByRole('combobox', { name: /category/i });
    if (await categorySelect.isVisible()) {
      await categorySelect.click();

      // Verify template categories are available
      // This depends on the specific game template loaded
      await expect(
        page.getByRole('option').first()
      ).toBeVisible();
    }
  });

  test('should validate player count against game rules', async ({ page }) => {
    // Navigate to a game with specific player count (e.g., Splendor: 2-4)
    await page.goto('/library/games/splendor-id/toolkit');

    // Try to start with 1 player (should fail)
    await page.getByPlaceholder('Player 1').fill('Solo');
    await page.getByRole('button', { name: /start.*session/i }).click();

    // Verify error toast about player count
    await expect(page.getByText(/requires at least.*players/i)).toBeVisible();
  });

  test('should display scoring rules in sidebar', async ({ page }) => {
    // Start session
    await page.getByRole('link', { name: /toolkit/i }).first().click();
    await page.waitForURL(/\/library\/games\/[a-f0-9-]+\/toolkit$/);

    await page.getByPlaceholder('Player 1').fill('Test');
    await page.getByRole('button', { name: /start.*session/i }).click();
    await page.waitForURL(/\/library\/games\/[a-f0-9-]+\/toolkit\/[a-f0-9-]+$/);

    // Verify scoring rules card exists
    await expect(page.getByText(/scoring rules/i)).toBeVisible();
  });

  test('should finalize and return to game detail page', async ({ page }) => {
    // Start session
    await page.getByRole('link', { name: /toolkit/i }).first().click();
    await page.waitForURL(/\/library\/games\/[a-f0-9-]+\/toolkit$/);

    await page.getByPlaceholder('Player 1').fill('Winner');
    await page.getByRole('button', { name: /start.*session/i }).click();
    await page.waitForURL(/\/library\/games\/[a-f0-9-]+\/toolkit\/[a-f0-9-]+$/);

    // Finalize session
    await page.getByRole('button', { name: /finalize|end/i }).click();

    // Verify redirect to game detail (not generic toolkit)
    await page.waitForURL(/\/library\/games\/[a-f0-9-]+$/, { timeout: 5000 });
    expect(page.url()).toMatch(/\/library\/games\/[a-f0-9-]+$/);
    expect(page.url()).not.toContain('/toolkit');
  });

  test('should be mobile responsive', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });

    // Navigate to game toolkit
    await page.goto('/library');
    await page.getByRole('link', { name: /toolkit/i }).first().click();

    // Verify layout adapts
    await expect(page.getByRole('heading', { name: /toolkit/i })).toBeVisible();
  });

  test('should work in dark mode', async ({ page }) => {
    // Enable dark mode
    await page.emulateMedia({ colorScheme: 'dark' });

    // Navigate to game toolkit
    await page.goto('/library');
    await page.getByRole('link', { name: /toolkit/i }).first().click();

    // Verify renders correctly
    await expect(page.getByRole('heading', { name: /toolkit/i })).toBeVisible();
  });
});
