/**
 * E2E Tests - Share Game User Flows
 *
 * Tests the complete user-facing share game wizard flow.
 * Covers:
 * - Complete 3-step share wizard (game preview, documents, notes & confirm)
 * - Rate limit warning display
 *
 * #5080: the admin approval part was written for the review screen deleted on 2026-02-23 (a request id that is not a
 * GUID, kept in localStorage). The review on the screen #5076 restored is in `admin-share-requests.spec.ts`.
 *
 * Issue #2954: E2E Tests - User Flows (Playwright)
 * Parent: #2751 (Integration Tests - Full Share Flow)
 * Epic: #2718 (Milestone 7)
 */

import { test, expect, Page } from '@playwright/test';

import { authenticateViaAPI } from './fixtures/auth';

/**
 * Helper: Seed a game in user library via API
 * Returns gameId for use in tests
 */
async function seedGameInLibrary(page: Page, userEmail: string): Promise<string> {
  // Mocked approach: Use localStorage to simulate game in library
  // In real scenario, this would call POST /api/v1/library/games
  await page.evaluate(() => {
    const mockGame = {
      gameId: 'test-game-id-123',
      gameTitle: 'Test Board Game',
      gamePublisher: 'Test Publisher',
      bggId: 123456,
    };
    localStorage.setItem('test-library-game', JSON.stringify(mockGame));
  });

  return 'test-game-id-123';
}

/**
 * Helper: Seed user at rate limit (3/3 used)
 */
async function seedUserAtRateLimit(page: Page): Promise<void> {
  await page.evaluate(() => {
    const rateLimitStatus = {
      canCreateRequest: false,
      pendingRequestsCount: 3,
      maxPendingRequests: 3,
      requestsThisMonth: 3,
      maxRequestsPerMonth: 3,
      cooldownEndsAt: null,
      blockReason: 'You have reached your monthly share request limit (3/3)',
    };
    localStorage.setItem('test-rate-limit', JSON.stringify(rateLimitStatus));
  });
}

test.describe('Share Game Flow - User Wizard', () => {
  test('should complete full share game wizard flow (3 steps)', async ({ page }) => {
    // Step 0: Setup - Authenticate as regular user
    await authenticateViaAPI(page, 'user@test.com', 'Test123!');
    await seedGameInLibrary(page, 'user@test.com');

    // Navigate to library page (assuming /library route exists)
    await page.goto('/library');

    // Wait for library to load
    await page.waitForLoadState('networkidle');

    // Step 1: Open share wizard (assuming share button exists on game card)
    // NOTE: This assumes library has a share button - adjust selector as needed
    await page.click('[data-testid="share-game-button"]', { timeout: 5000 });

    // Wizard should open
    await expect(page.locator('[data-testid="share-game-wizard"]')).toBeVisible();
    await expect(page.locator('[data-testid="wizard-title"]')).toContainText('Share');
    await expect(page.locator('[data-testid="wizard-step-indicator"]')).toContainText(
      'Step 1 of 3'
    );

    // Step 1: Game Preview - should be visible
    await expect(page.locator('[data-testid="wizard-step-1"]')).toBeVisible();

    // Continue to step 2
    await page.click('[data-testid="wizard-next"]');
    await expect(page.locator('[data-testid="wizard-step-indicator"]')).toContainText(
      'Step 2 of 3'
    );

    // Step 2: Document Selection - should be visible
    await expect(page.locator('[data-testid="wizard-step-2"]')).toBeVisible();

    // Continue to step 3 (no documents selected - optional)
    await page.click('[data-testid="wizard-next"]');
    await expect(page.locator('[data-testid="wizard-step-indicator"]')).toContainText(
      'Step 3 of 3'
    );

    // Step 3: Notes and Confirmation - should be visible
    await expect(page.locator('[data-testid="wizard-step-3"]')).toBeVisible();
    await expect(page.locator('[data-testid="wizard-summary"]')).toBeVisible();

    // Fill in optional notes
    await page.fill(
      '[data-testid="wizard-notes-textarea"]',
      'Test notes for admin review - E2E automated test'
    );

    // Verify notes character count updates
    await expect(page.locator('[data-testid="wizard-notes-remaining"]')).toContainText('remaining');

    // Submit button should be visible
    await expect(page.locator('[data-testid="wizard-submit"]')).toBeVisible();

    // Submit the request
    await page.click('[data-testid="wizard-submit"]');

    // Verify success (wizard should close or show success message)
    // NOTE: This assumes wizard closes on success - adjust based on actual behavior
    await page.waitForTimeout(2000); // Wait for submission
    await expect(page.locator('[data-testid="share-game-wizard"]')).not.toBeVisible({
      timeout: 5000,
    });

    // Verify navigation to share requests page or success message
    // NOTE: Adjust based on actual post-submission behavior
  });

  test('should allow navigation between wizard steps', async ({ page }) => {
    // Setup
    await authenticateViaAPI(page, 'user@test.com', 'Test123!');
    await seedGameInLibrary(page, 'user@test.com');
    await page.goto('/library');
    await page.click('[data-testid="share-game-button"]', { timeout: 5000 });

    // Start at step 1
    await expect(page.locator('[data-testid="wizard-step-1"]')).toBeVisible();

    // Go to step 2
    await page.click('[data-testid="wizard-next"]');
    await expect(page.locator('[data-testid="wizard-step-2"]')).toBeVisible();

    // Go back to step 1
    await page.click('[data-testid="wizard-back"]');
    await expect(page.locator('[data-testid="wizard-step-1"]')).toBeVisible();

    // Back button should not be visible on step 1
    await expect(page.locator('[data-testid="wizard-back"]')).not.toBeVisible();

    // Go to step 3 via step 2
    await page.click('[data-testid="wizard-next"]');
    await page.click('[data-testid="wizard-next"]');
    await expect(page.locator('[data-testid="wizard-step-3"]')).toBeVisible();

    // Back button should be visible on step 3
    await expect(page.locator('[data-testid="wizard-back"]')).toBeVisible();

    // Next button should not be visible on step 3 (replaced by Submit)
    await expect(page.locator('[data-testid="wizard-next"]')).not.toBeVisible();
    await expect(page.locator('[data-testid="wizard-submit"]')).toBeVisible();
  });

  test('should allow canceling wizard', async ({ page }) => {
    // Setup
    await authenticateViaAPI(page, 'user@test.com', 'Test123!');
    await seedGameInLibrary(page, 'user@test.com');
    await page.goto('/library');
    await page.click('[data-testid="share-game-button"]', { timeout: 5000 });

    // Wizard should be open
    await expect(page.locator('[data-testid="share-game-wizard"]')).toBeVisible();

    // Click cancel
    await page.click('[data-testid="wizard-cancel"]');

    // Wizard should close
    await expect(page.locator('[data-testid="share-game-wizard"]')).not.toBeVisible({
      timeout: 3000,
    });
  });
});

test.describe('Share Game Flow - Rate Limit Warning', () => {
  test('should display rate limit warning and disable submit', async ({ page }) => {
    // Step 0: Setup - Seed user at rate limit
    await authenticateViaAPI(page, 'limited@test.com', 'Test123!');
    await seedUserAtRateLimit(page);
    await seedGameInLibrary(page, 'limited@test.com');

    // Navigate to library
    await page.goto('/library');
    await page.waitForLoadState('networkidle');

    // Try to open share wizard
    const shareButton = page.locator('[data-testid="share-game-button"]');

    // Share button should either:
    // 1. Be disabled with tooltip showing rate limit
    // 2. Open wizard but show rate limit warning

    // Attempt to click (may be disabled)
    if (await shareButton.isEnabled()) {
      await shareButton.click();

      // If wizard opens, should show rate limit warning
      await expect(page.locator('[data-testid="share-game-wizard"]')).toBeVisible();

      // Rate limit warning should be visible
      // NOTE: Adjust selector based on actual implementation
      const rateLimitWarning = page.locator('text=/rate limit|limit reached|monthly limit|3\\/3/i');
      await expect(rateLimitWarning).toBeVisible();

      // Submit button should be disabled
      // Navigate to final step to check submit button
      await page.click('[data-testid="wizard-next"]');
      await page.click('[data-testid="wizard-next"]');

      await expect(page.locator('[data-testid="wizard-submit"]')).toBeDisabled();
    } else {
      // Share button is disabled - verify tooltip or disabled state
      await expect(shareButton).toBeDisabled();

      // Hover to see tooltip (if implemented)
      await shareButton.hover();

      // NOTE: Adjust based on actual tooltip implementation
      const tooltip = page.locator('text=/rate limit|limit reached|monthly limit/i');
      await expect(tooltip).toBeVisible({ timeout: 3000 });
    }
  });

  test('should show rate limit info in wizard', async ({ page }) => {
    // Setup user close to limit (2/3)
    await authenticateViaAPI(page, 'nearLimit@test.com', 'Test123!');

    await page.evaluate(() => {
      const rateLimitStatus = {
        canCreateRequest: true,
        pendingRequestsCount: 2,
        maxPendingRequests: 3,
        requestsThisMonth: 2,
        maxRequestsPerMonth: 3,
        cooldownEndsAt: null,
        blockReason: null,
      };
      localStorage.setItem('test-rate-limit', JSON.stringify(rateLimitStatus));
    });

    await seedGameInLibrary(page, 'nearLimit@test.com');

    // Navigate and open wizard
    await page.goto('/library');
    await page.click('[data-testid="share-game-button"]', { timeout: 5000 });

    // Wizard should open
    await expect(page.locator('[data-testid="share-game-wizard"]')).toBeVisible();

    // Should show rate limit info somewhere in wizard (e.g., "2/3 requests used")
    // NOTE: Adjust based on actual implementation
    const rateLimitInfo = page.locator('text=/2\\/3|requests.*remaining/i');
    await expect(rateLimitInfo).toBeVisible({ timeout: 3000 });

    // Submit should still be enabled since under limit
    await page.click('[data-testid="wizard-next"]');
    await page.click('[data-testid="wizard-next"]');
    await expect(page.locator('[data-testid="wizard-submit"]')).toBeEnabled();
  });
});
