/**
 * E2E Test: PlayRecord Creation with BGG Integration
 * Issue #4276: Complete flow testing for BGG search → Add to library → Create PlayRecord
 *
 * Dependencies:
 * - Issue #4275: Backend rate limiting ✅
 * - Issue #4274: Frontend BGG dialog ✅
 *
 * Test Scenarios:
 * 1. Happy path: Search BGG → Add game → Complete wizard
 * 2. Rate limit: Exhaust quota → See error → Wait for reset
 * 3. Duplicate: BGG game already in library
 * 4. Network error: BGG API failure → Fallback to manual
 * 5. Cancel flow: Open dialog → Cancel
 * 6. Multiple sessions: Create 3 PlayRecords
 */

import { test, expect } from '../fixtures';

test.describe('PlayRecord BGG Integration', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to PlayRecord creation
    await page.goto('/play-records/new');
    await expect(page).toHaveURL('/play-records/new');
  });

  test('Happy Path: Creates PlayRecord with BGG game search', async ({ page }) => {
    // Step 1: Search for game not in library
    const searchInput = page.getByPlaceholder('Search your library...');
    await searchInput.fill('Wingspan');
    await page.waitForTimeout(500); // Wait for autocomplete

    // No results → Click "Search on BGG"
    const searchBggButton = page.getByRole('button', { name: /search.*bgg/i });
    await searchBggButton.click();

    // Step 2: BGG dialog opens
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByText(/search boardgamegeek/i)).toBeVisible();

    // Search for Wingspan
    const bggSearchInput = page.getByPlaceholder(/cerca.*boardgamegeek/i);
    await bggSearchInput.fill('Wingspan');
    await page.waitForTimeout(500); // Debounce

    // Select first result (mock: BGG ID 266192)
    const firstResult = page.locator('[data-bgg-result]').first();
    await expect(firstResult).toBeVisible();
    await firstResult.click();

    // Step 3: Add to library
    const addButton = page.getByRole('button', { name: /add to library|continua/i });
    await addButton.click();

    // Dialog closes, game auto-selected
    await expect(page.getByRole('dialog')).not.toBeVisible();
    await expect(searchInput).toHaveValue('Wingspan');

    // Step 4: Complete wizard
    await page.getByRole('button', { name: /next|avanti/i }).click();

    // Step 2: Session details
    await expect(page.getByText(/session details|dettagli/i)).toBeVisible();
    await page.getByRole('button', { name: /next|avanti/i }).click();

    // Step 3: Scoring (skip)
    await page.getByRole('button', { name: /create session|crea/i }).click();

    // Success: Redirected to play record detail
    await expect(page).toHaveURL(/\/play-records\/[a-f0-9-]+$/);
  });

  test('Rate Limit: Handles quota exhaustion gracefully', async ({ page }) => {
    // Mock: User has exhausted Free tier quota (5/5)
    await page.route('**/api/v1/bgg/search*', async (route) => {
      await route.fulfill({
        status: 429,
        headers: {
          'X-RateLimit-Limit': '5',
          'X-RateLimit-Remaining': '0',
          'X-RateLimit-Reset': String(Math.floor(Date.now() / 1000) + 60),
          'Retry-After': '60',
        },
        json: {
          error: 'Rate limit exceeded',
          message: 'BGG API limit exceeded. Maximum 5 requests per minute for Free tier.',
          retryAfter: 60,
        },
      });
    });

    // Open BGG dialog
    const searchInput = page.getByPlaceholder('Search your library...');
    await searchInput.fill('Catan');
    await page.waitForTimeout(500);

    const searchBggButton = page.getByRole('button', { name: /search.*bgg/i });
    await searchBggButton.click();

    // Search triggers 429
    const bggSearchInput = page.getByPlaceholder(/cerca.*boardgamegeek/i);
    await bggSearchInput.fill('Catan');

    // Expect rate limit error
    await expect(page.getByText(/rate limit/i)).toBeVisible();
    await expect(page.getByText(/60.*second|1.*min/i)).toBeVisible();

    // Search input should be disabled
    await expect(bggSearchInput).toBeDisabled();
  });

  test('Duplicate: Uses existing game if already in library', async ({ page }) => {
    // Mock: BGG returns game that exists in user library
    await page.route('**/api/v1/bgg/search*', async (route) => {
      await route.fulfill({
        status: 200,
        json: {
          results: [
            { bggId: 266192, name: 'Wingspan', yearPublished: 2019 },
          ],
        },
      });
    });

    await page.route('**/api/v1/user-library/private-games', async (route) => {
      // Conflict: Game already exists
      await route.fulfill({
        status: 409,
        json: { error: 'Game already in library' },
      });
    });

    // Open BGG dialog and search
    const searchInput = page.getByPlaceholder('Search your library...');
    await searchInput.fill('Wingspan');
    await page.waitForTimeout(500);

    await page.getByRole('button', { name: /search.*bgg/i }).click();
    const bggSearchInput = page.getByPlaceholder(/cerca.*boardgamegeek/i);
    await bggSearchInput.fill('Wingspan');
    await page.waitForTimeout(500);

    // Select result
    await page.locator('[data-bgg-result]').first().click();
    await page.getByRole('button', { name: /add to library|continua/i }).click();

    // Expect error toast about duplicate
    await expect(page.getByText(/already.*library|già presente/i)).toBeVisible();
  });

  test('Network Error: Fallback to manual entry', async ({ page }) => {
    // Mock: BGG API unavailable
    await page.route('**/api/v1/bgg/search*', async (route) => {
      await route.abort('failed');
    });

    // Open BGG dialog
    const searchInput = page.getByPlaceholder('Search your library...');
    await searchInput.fill('Scythe');
    await page.waitForTimeout(500);

    await page.getByRole('button', { name: /search.*bgg/i }).click();

    // BGG search fails
    const bggSearchInput = page.getByPlaceholder(/cerca.*boardgamegeek/i);
    await bggSearchInput.fill('Scythe');
    await page.waitForTimeout(500);

    // Expect error message
    await expect(page.getByText(/error|errore/i)).toBeVisible();

    // Fallback: Add manually button
    const manualButton = page.getByRole('button', { name: /manual|manuale/i });
    await expect(manualButton).toBeVisible();
    await manualButton.click();

    // Should show manual entry form
    await expect(page.getByLabel(/title|titolo/i)).toBeVisible();
  });

  test('Cancel Flow: User cancels BGG search', async ({ page }) => {
    // Open BGG dialog
    const searchInput = page.getByPlaceholder('Search your library...');
    await searchInput.fill('Pandemic');
    await page.waitForTimeout(500);

    await page.getByRole('button', { name: /search.*bgg/i }).click();

    // Dialog visible
    await expect(page.getByRole('dialog')).toBeVisible();

    // Cancel
    const cancelButton = page.getByRole('button', { name: /cancel|annulla/i });
    await cancelButton.click();

    // Dialog closed
    await expect(page.getByRole('dialog')).not.toBeVisible();

    // Can continue with freeform
    await page.getByLabel(/free.*form|testo libero/i).click();
    await page.getByPlaceholder(/enter game name/i).fill('Pandemic');

    // Wizard continues
    await expect(page.getByRole('button', { name: /next/i })).toBeEnabled();
  });

  test('Multiple Sessions: Create 3 PlayRecords with different games', async ({ page }) => {
    const games = ['Wingspan', 'Terraforming Mars', 'Scythe'];

    for (const game of games) {
      // Navigate to create page
      await page.goto('/play-records/new');

      // Use freeform entry for speed
      await page.getByLabel(/free.*form/i).click();
      await page.getByPlaceholder(/enter game name/i).fill(game);

      // Next
      await page.getByRole('button', { name: /next/i }).click();

      // Skip details
      await page.getByRole('button', { name: /next/i }).click();

      // Create
      await page.getByRole('button', { name: /create/i }).click();

      // Verify created
      await expect(page).toHaveURL(/\/play-records\/[a-f0-9-]+$/);
    }

    // Verify all 3 in history
    await page.goto('/play-records');
    for (const game of games) {
      await expect(page.getByText(game)).toBeVisible();
    }
  });

  test('Performance: Full flow completes in <10s', async ({ page }) => {
    const startTime = Date.now();

    // Quick flow: Freeform → Create
    await page.getByLabel(/free.*form/i).click();
    await page.getByPlaceholder(/enter game name/i).fill('7 Wonders');
    await page.getByRole('button', { name: /next/i }).click();
    await page.getByRole('button', { name: /next/i }).click();
    await page.getByRole('button', { name: /create/i }).click();

    await expect(page).toHaveURL(/\/play-records\/[a-f0-9-]+$/);

    const duration = Date.now() - startTime;
    expect(duration).toBeLessThan(10000); // <10s
  });
});
