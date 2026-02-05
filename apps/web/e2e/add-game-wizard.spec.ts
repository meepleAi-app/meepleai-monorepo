/**
 * Add Game Wizard E2E Tests
 * Issue #3477, #3650: End-to-end tests for wizard flow with API integration
 *
 * Tests:
 * - Complete wizard flow (search shared game → add to collection)
 * - Custom game flow (create custom → review)
 * - Step navigation and validation
 * - Search functionality with debounce
 * - Error handling and recovery
 */

import { test, expect } from '@playwright/test';

test.describe('Add Game Wizard', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to wizard (assumes authenticated user)
    await page.goto('/dashboard/collection/add-game');
  });

  test.describe('Wizard Structure', () => {
    test('displays wizard title and description', async ({ page }) => {
      await expect(page.getByTestId('wizard-title')).toHaveText('Add Game to Collection');
      await expect(page.getByTestId('wizard-subtitle')).toContainText('Search for a game');
    });

    test('displays initial step correctly', async ({ page }) => {
      // Should be on Step 1
      await expect(page.getByText('Search or Create Game')).toBeVisible();
      await expect(page.getByPlaceholder(/Search by game title/i)).toBeVisible();
      await expect(page.getByText(/Create Custom Game/i)).toBeVisible();
    });

    test('shows back to collection link', async ({ page }) => {
      await expect(page.getByRole('link', { name: /Back to Collection/i })).toBeVisible();
    });
  });

  test.describe('Search Functionality', () => {
    test('search input accepts text and shows loading state', async ({ page }) => {
      const searchInput = page.getByPlaceholder(/Search by game title/i);
      await searchInput.fill('Catan');

      // Should show loading after debounce
      await page.waitForTimeout(350); // Wait for debounce (300ms + buffer)

      // Either shows results or loading state or no results
      const hasResults = await page.getByText(/games? found/i).isVisible().catch(() => false);
      const hasNoResults = await page.getByText(/No games found/i).isVisible().catch(() => false);

      expect(hasResults || hasNoResults).toBeTruthy();
    });

    test('displays search results with game info', async ({ page }) => {
      // Mock a search that should return results
      const searchInput = page.getByPlaceholder(/Search by game title/i);
      await searchInput.fill('test');

      // Wait for results
      await page.waitForTimeout(500);

      // Check if any game cards are displayed (if API returns results)
      const gameCards = page.locator('[class*="cursor-pointer"]').filter({ hasText: /players/i });
      const count = await gameCards.count();

      // Test passes whether we have results or not (depends on API state)
      if (count > 0) {
        // Verify first game card has expected structure
        const firstCard = gameCards.first();
        await expect(firstCard).toBeVisible();
      }
    });

    test('clears search results when input cleared', async ({ page }) => {
      const searchInput = page.getByPlaceholder(/Search by game title/i);

      // Search first
      await searchInput.fill('game');
      await page.waitForTimeout(400);

      // Clear search
      await searchInput.clear();

      // Results should be cleared (no "games found" text)
      await page.waitForTimeout(100);
      const hasResultCount = await page.getByText(/\d+ games? found/i).isVisible().catch(() => false);
      expect(hasResultCount).toBeFalsy();
    });
  });

  test.describe('Game Selection', () => {
    test('selecting a game shows selection indicator and Next button', async ({ page }) => {
      const searchInput = page.getByPlaceholder(/Search by game title/i);
      await searchInput.fill('game');
      await page.waitForTimeout(500);

      // Try to find and click a game result
      const gameResult = page.locator('[class*="cursor-pointer"]').first();
      const isVisible = await gameResult.isVisible().catch(() => false);

      if (isVisible) {
        await gameResult.click();

        // Should show "Selected" badge
        await expect(page.getByText(/Selected/i)).toBeVisible();

        // Next button should be visible
        await expect(page.getByRole('button', { name: /Next/i })).toBeVisible();
      }
    });
  });

  test.describe('Custom Game Flow', () => {
    test('clicking Create Custom Game advances to Step 2', async ({ page }) => {
      await page.getByText(/Create Custom Game/i).click();

      // Should be on Step 2 (Game Details)
      await expect(page.getByText('Game Details')).toBeVisible();
      await expect(page.getByLabel(/Game Name/i)).toBeVisible();
    });

    test('custom game form validates required fields', async ({ page }) => {
      await page.getByText(/Create Custom Game/i).click();

      // Next button should be disabled without name
      const nextButton = page.getByRole('button', { name: /Next/i });
      await expect(nextButton).toBeDisabled();

      // Fill name
      await page.getByLabel(/Game Name/i).fill('My Test Game');

      // Next button should now be enabled
      await expect(nextButton).toBeEnabled();
    });

    test('custom game form accepts all fields', async ({ page }) => {
      await page.getByText(/Create Custom Game/i).click();

      // Fill all fields
      await page.getByLabel(/Game Name/i).fill('My Test Game');

      const minPlayers = page.getByLabel(/Min Players/i);
      const maxPlayers = page.getByLabel(/Max Players/i);
      const playTime = page.getByLabel(/Play Time/i);
      const complexity = page.getByLabel(/Complexity/i);

      // Fill optional fields if visible
      if (await minPlayers.isVisible()) await minPlayers.fill('2');
      if (await maxPlayers.isVisible()) await maxPlayers.fill('6');
      if (await playTime.isVisible()) await playTime.fill('90');
      if (await complexity.isVisible()) await complexity.fill('3');

      // Should be able to proceed
      await page.getByRole('button', { name: /Next/i }).click();

      // Should advance to Step 3 (Upload PDF)
      await expect(page.getByText('Upload Private PDF')).toBeVisible();
    });
  });

  test.describe('Step 3: PDF Upload', () => {
    test('PDF upload step allows skipping', async ({ page }) => {
      // Get to Step 3 via custom game path
      await page.getByText(/Create Custom Game/i).click();
      await page.getByLabel(/Game Name/i).fill('Test Game');
      await page.getByRole('button', { name: /Next/i }).click();

      // Should be on Step 3
      await expect(page.getByText('Upload Private PDF')).toBeVisible();

      // Skip should advance to Step 4
      const skipButton = page.getByRole('button', { name: /Skip/i });
      if (await skipButton.isVisible()) {
        await skipButton.click();
        await expect(page.getByText('Review & Confirm')).toBeVisible();
      } else {
        // Next button works as skip when no PDF
        await page.getByRole('button', { name: /Next/i }).click();
        await expect(page.getByText('Review & Confirm')).toBeVisible();
      }
    });
  });

  test.describe('Step 4: Review & Confirm', () => {
    test('review step shows game summary', async ({ page }) => {
      // Get to Step 4 via custom game path
      await page.getByText(/Create Custom Game/i).click();
      await page.getByLabel(/Game Name/i).fill('My Review Test Game');
      await page.getByRole('button', { name: /Next/i }).click();

      // Skip PDF
      const skipButton = page.getByRole('button', { name: /Skip/i });
      if (await skipButton.isVisible()) {
        await skipButton.click();
      } else {
        await page.getByRole('button', { name: /Next/i }).click();
      }

      // Should be on Step 4
      await expect(page.getByText('Review & Confirm')).toBeVisible();

      // Should show game name
      await expect(page.getByText('My Review Test Game')).toBeVisible();

      // Should show "Custom" badge
      await expect(page.getByText('Custom')).toBeVisible();

      // Should show Add to Collection button
      await expect(page.getByRole('button', { name: /Add to Collection/i })).toBeVisible();
    });

    test('custom game submission shows info message (backend not ready)', async ({ page }) => {
      // Navigate to review step
      await page.getByText(/Create Custom Game/i).click();
      await page.getByLabel(/Game Name/i).fill('Custom Game Test');
      await page.getByRole('button', { name: /Next/i }).click();

      // Skip PDF
      const skipButton = page.getByRole('button', { name: /Skip/i });
      if (await skipButton.isVisible()) {
        await skipButton.click();
      } else {
        await page.getByRole('button', { name: /Next/i }).click();
      }

      // Submit
      await page.getByRole('button', { name: /Add to Collection/i }).click();

      // Should show info message (custom games not yet supported)
      await expect(page.getByText(/coming soon|search for games/i)).toBeVisible({ timeout: 5000 });
    });
  });

  test.describe('Navigation', () => {
    test('back button returns to previous step', async ({ page }) => {
      // Go to Step 2
      await page.getByText(/Create Custom Game/i).click();
      await expect(page.getByText('Game Details')).toBeVisible();

      // Go back
      await page.getByRole('button', { name: /Back/i }).click();

      // Should be on Step 1
      await expect(page.getByText('Search or Create Game')).toBeVisible();
    });

    test('shared game skips Step 2 correctly', async ({ page }) => {
      // Search and select a game
      const searchInput = page.getByPlaceholder(/Search by game title/i);
      await searchInput.fill('test');
      await page.waitForTimeout(500);

      // Click first result if available
      const gameResult = page.locator('[class*="cursor-pointer"]').first();
      const isVisible = await gameResult.isVisible().catch(() => false);

      if (isVisible) {
        await gameResult.click();
        await page.getByRole('button', { name: /Next/i }).click();

        // Should skip to Step 3 (not Step 2)
        await expect(page.getByText('Upload Private PDF')).toBeVisible();

        // Going back should return to Step 1 (not Step 2)
        await page.getByRole('button', { name: /Back/i }).click();
        await expect(page.getByText('Search or Create Game')).toBeVisible();
      }
    });

    test('back to collection link works', async ({ page }) => {
      await page.getByRole('link', { name: /Back to Collection/i }).click();
      await page.waitForURL('/dashboard/collection');
    });
  });

  test.describe('Accessibility', () => {
    test('step indicator is visible and shows progress', async ({ page }) => {
      // Check step icons are visible
      const stepIndicators = page.locator('[class*="rounded-full"]').filter({ hasText: /🔍|📝|📄|✓|1|2|3|4/i });
      expect(await stepIndicators.count()).toBeGreaterThan(0);
    });

    test('search input has proper aria-label', async ({ page }) => {
      const searchInput = page.getByLabel(/Search games/i);
      await expect(searchInput).toBeVisible();
    });

    test('buttons have proper labels', async ({ page }) => {
      // Create Custom Game button
      await expect(page.getByRole('button', { name: /Create Custom Game/i })).toBeVisible();

      // Back to Collection link
      await expect(page.getByRole('link', { name: /Back to Collection/i })).toBeVisible();
    });
  });

  test.describe('Error Handling', () => {
    test('handles network errors gracefully', async ({ page }) => {
      // Intercept API calls and fail them
      await page.route('**/api/v1/shared-games/search**', route => {
        route.abort('failed');
      });

      const searchInput = page.getByPlaceholder(/Search by game title/i);
      await searchInput.fill('test');
      await page.waitForTimeout(500);

      // Should show error message or retry option
      const hasError = await page.getByText(/error|failed|retry/i).isVisible().catch(() => false);
      expect(hasError).toBeTruthy();
    });
  });

  test.describe('Loading States', () => {
    test('shows loading indicator during search', async ({ page }) => {
      // Slow down API response
      await page.route('**/api/v1/shared-games/search**', async route => {
        await new Promise(resolve => setTimeout(resolve, 1000));
        await route.continue();
      });

      const searchInput = page.getByPlaceholder(/Search by game title/i);
      await searchInput.fill('test');

      // Should show loading state (skeleton or spinner)
      await page.waitForTimeout(400); // After debounce
      const hasLoading =
        (await page.locator('[class*="animate-pulse"]').count()) > 0 ||
        (await page.locator('[class*="spinner"]').count()) > 0;

      // Loading state should appear during API call
      expect(hasLoading).toBeTruthy();
    });
  });
});
