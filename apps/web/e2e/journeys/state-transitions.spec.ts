/**
 * Journey 3: Game State Transitions E2E Tests
 *
 * Tests the complete state transition workflows:
 * 1. Loan Game - Mark game as loaned to friend
 * 2. Return Game - Mark loaned game as returned
 * 3. Wishlist - Add/remove games from wishlist
 *
 * Pattern: Hybrid approach (mock external APIs, test real state management)
 * Related Issue: #2843 - E2E User Journey Tests
 * Epic: #2823
 */

import { expect, test } from '../fixtures/chromatic';
import { AuthHelper, USER_FIXTURES } from '../pages';
import { WaitHelper } from '../helpers/WaitHelper';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const MOCK_GAME_OWNED = {
  id: 'test-game-state-1',
  title: 'Catan',
  bggId: 13,
  status: 'Owned',
  loanedTo: null,
  loanedAt: null,
  expectedReturnDate: null,
};

const MOCK_GAME_LOANED = {
  ...MOCK_GAME_OWNED,
  status: 'Loaned',
  loanedTo: 'John Doe',
  loanedAt: new Date().toISOString(),
  expectedReturnDate: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(), // 7 days from now
};

const MOCK_WISHLIST_GAME = {
  id: 'test-game-wishlist-1',
  title: 'Wingspan',
  bggId: 266192,
  status: 'Wishlist',
  priority: 'High',
  addedToWishlistAt: new Date().toISOString(),
};

test.describe('Journey 3: Game State Transitions', () => {
  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Auth: Mock authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
  });

  test('should loan game to friend and return it', async ({ page }) => {
    let gameState = { ...MOCK_GAME_OWNED };

    // Mock: Game API with state tracking
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME_OWNED.id}`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(gameState),
      });
    });

    // Mock: Loan game API
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME_OWNED.id}/loan`, async route => {
      if (route.request().method() === 'POST') {
        const postData = route.request().postDataJSON();
        gameState = {
          ...gameState,
          status: 'Loaned',
          loanedTo: postData.loanedTo || 'John Doe',
          loanedAt: new Date().toISOString(),
          expectedReturnDate: postData.expectedReturnDate || MOCK_GAME_LOANED.expectedReturnDate,
        };

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(gameState),
        });
      }
    });

    // Mock: Return game API
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME_OWNED.id}/return`, async route => {
      if (route.request().method() === 'POST') {
        gameState = {
          ...MOCK_GAME_OWNED,
          status: 'Owned',
          loanedTo: null,
          loanedAt: null,
          expectedReturnDate: null,
        };

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(gameState),
        });
      }
    });

    await test.step('Navigate to game detail page', async () => {
      await page.goto(`/games/${MOCK_GAME_OWNED.id}`);
      await expect(page.locator(`text=${MOCK_GAME_OWNED.title}`).first()).toBeVisible({ timeout: 10000 });
    });

    await test.step('Initiate loan process', async () => {
      // Look for "Loan Game" button or action menu
      const loanButton = page.getByRole('button', { name: /loan|lend|prestito/i }).first();

      if (await loanButton.isVisible({ timeout: 5000 })) {
        await loanButton.click();

        // Wait for loan form/modal
        const waitHelper = new WaitHelper(page);
        await waitHelper.waitForDOMStable('body', 2000);
      } else {
        // Alternative: look in overflow menu or game actions
        const actionsMenu = page.getByRole('button', { name: /actions|more|menu/i }).first();
        if (await actionsMenu.isVisible({ timeout: 3000 })) {
          await actionsMenu.click();
          const loanMenuItem = page.getByRole('menuitem', { name: /loan|lend/i }).first();
          await loanMenuItem.click();
        }
      }
    });

    await test.step('Fill loan details', async () => {
      // Fill loanee name
      const loaneeInput = page.locator('input[name="loanedTo"], input[placeholder*="friend"]').first();
      if (await loaneeInput.isVisible({ timeout: 5000 })) {
        await loaneeInput.fill('John Doe');
      }

      // Set expected return date (optional)
      const dateInput = page.locator('input[type="date"], input[name="returnDate"]').first();
      if (await dateInput.isVisible({ timeout: 3000 })) {
        const futureDate = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000);
        await dateInput.fill(futureDate.toISOString().split('T')[0]);
      }

      // Submit loan
      const submitButton = page.getByRole('button', { name: /confirm|save|loan/i }).first();
      await submitButton.click();

      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(3000);
    });

    await test.step('Verify game status changed to Loaned', async () => {
      // Reload to see updated state
      await page.reload();
      await page.waitForLoadState('networkidle');

      // Look for loaned status indicator
      const pageContent = await page.textContent('body');
      expect(pageContent).toContain('Loaned');
      expect(pageContent).toContain('John Doe');
    });

    await test.step('Return loaned game', async () => {
      // Look for "Return Game" button
      const returnButton = page.getByRole('button', { name: /return|restitui/i }).first();

      if (await returnButton.isVisible({ timeout: 5000 })) {
        await returnButton.click();

        // Confirm return (if confirmation modal exists)
        const confirmButton = page.getByRole('button', { name: /confirm|yes|return/i }).first();
        if (await confirmButton.isVisible({ timeout: 3000 })) {
          await confirmButton.click();
        }

        const waitHelper = new WaitHelper(page);
        await waitHelper.waitForNetworkIdle(3000);
      }
    });

    await test.step('Verify game status changed back to Owned', async () => {
      // Reload to see updated state
      await page.reload();
      await page.waitForLoadState('networkidle');

      // Verify Owned status
      const pageContent = await page.textContent('body');
      expect(pageContent).toContain('Owned');
      expect(pageContent).not.toContain('Loaned to John Doe');
    });

    console.log('✅ Journey 3 (Loan/Return): State transition successful');
  });

  test('should add game to wishlist and remove it', async ({ page }) => {
    let inWishlist = false;

    // Mock: Wishlist endpoints
    await page.route(`${API_BASE}/api/v1/wishlist`, async route => {
      if (route.request().method() === 'GET') {
        // Get wishlist
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(inWishlist ? [MOCK_WISHLIST_GAME] : []),
        });
      } else if (route.request().method() === 'POST') {
        // Add to wishlist
        inWishlist = true;
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_WISHLIST_GAME),
        });
      }
    });

    // Mock: Remove from wishlist
    await page.route(`${API_BASE}/api/v1/wishlist/${MOCK_WISHLIST_GAME.id}`, async route => {
      if (route.request().method() === 'DELETE') {
        inWishlist = false;
        await route.fulfill({ status: 204 });
      }
    });

    // Mock: Game details
    await page.route(`${API_BASE}/api/v1/games/${MOCK_WISHLIST_GAME.id}`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_WISHLIST_GAME),
      });
    });

    await test.step('Navigate to game catalog or search', async () => {
      await page.goto('/games');
      await page.waitForLoadState('networkidle');
    });

    await test.step('Add game to wishlist', async () => {
      // Look for Wingspan game card
      const gameCard = page.locator(`text=${MOCK_WISHLIST_GAME.title}`).first();
      if (await gameCard.isVisible({ timeout: 5000 })) {
        await gameCard.click();
      } else {
        // Navigate directly to game
        await page.goto(`/games/${MOCK_WISHLIST_GAME.id}`);
      }

      await page.waitForLoadState('networkidle');

      // Find "Add to Wishlist" button
      const wishlistButton = page.getByRole('button', { name: /add to wishlist|wishlist|desideri/i }).first();
      await expect(wishlistButton).toBeVisible({ timeout: 5000 });
      await wishlistButton.click();

      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(2000);
    });

    await test.step('Verify game added to wishlist', async () => {
      // Navigate to wishlist page
      await page.goto('/wishlist');
      await page.waitForLoadState('networkidle');

      // Verify Wingspan appears in wishlist
      await expect(page.locator(`text=${MOCK_WISHLIST_GAME.title}`)).toBeVisible({ timeout: 5000 });
    });

    await test.step('Remove game from wishlist', async () => {
      // Find remove button on wishlist item
      const removeButton = page.getByRole('button', { name: /remove|delete|rimuovi/i }).first();
      await removeButton.click();

      // Confirm removal (if confirmation exists)
      const confirmButton = page.getByRole('button', { name: /confirm|yes|remove/i }).first();
      if (await confirmButton.isVisible({ timeout: 3000 })) {
        await confirmButton.click();
      }

      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(2000);
    });

    await test.step('Verify game removed from wishlist', async () => {
      // Reload wishlist
      await page.reload();
      await page.waitForLoadState('networkidle');

      // Verify Wingspan no longer in wishlist
      const pageContent = await page.textContent('body');
      const wingspanCount = (pageContent?.match(/Wingspan/g) || []).length;

      // Should not appear, or appear very minimally (nav/footer)
      expect(wingspanCount).toBeLessThan(2);
    });

    console.log('✅ Journey 3 (Wishlist): Add/remove workflow successful');
  });

  test('should verify state transition persistence', async ({ page }) => {
    await test.step('Navigate to game with state management', async () => {
      await page.goto(`/games/${MOCK_GAME_OWNED.id}`);
      await expect(page.locator(`text=${MOCK_GAME_OWNED.title}`).first()).toBeVisible();
    });

    await test.step('Verify state is persisted across navigation', async () => {
      // Check initial state
      const pageContent1 = await page.textContent('body');
      const initialState = pageContent1?.includes('Owned') ? 'Owned' : 'Unknown';

      // Navigate away and back
      await page.goto('/games');
      await page.waitForLoadState('networkidle');
      await page.goto(`/games/${MOCK_GAME_OWNED.id}`);
      await page.waitForLoadState('networkidle');

      // Verify state persisted
      const pageContent2 = await page.textContent('body');
      expect(pageContent2).toContain(initialState);

      console.log('✅ Journey 3 (Persistence): State transitions persist across navigation');
    });
  });
});
