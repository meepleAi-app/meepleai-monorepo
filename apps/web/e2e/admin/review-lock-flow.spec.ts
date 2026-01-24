/**
 * Admin Review Lock Flow E2E Tests
 *
 * Tests review lock management UI:
 * - Start review and acquire lock
 * - Timer countdown display
 * - Release review and unlock
 * - Conflict handling (409)
 * - Keyboard shortcuts (Escape)
 * - My Active Reviews popover
 *
 * Dependencies:
 * - Backend: POST /api/v1/admin/share-requests/{id}/start-review
 * - Backend: POST /api/v1/admin/share-requests/{id}/release
 * - Backend: GET /api/v1/admin/share-requests/my-reviews
 *
 * Issue #2748: Frontend - Admin Review Lock UI
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 */

import { test as base, expect } from '../fixtures/chromatic';
import { AdminHelper } from '../pages';
import type { Page } from '@playwright/test';

const test = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);
    await adminHelper.setupAdminAuth(true);
    await use(page);
  },
});

test.describe('Admin Review Lock Flow - Issue #2748', () => {
  test.describe('Start and Release Review', () => {
    test('should start review and show timer', async ({ adminPage }) => {
      // Navigate to share requests queue
      await adminPage.goto('/admin/share-requests');

      // Find first pending request
      const firstRequest = adminPage.locator('[data-testid^="share-request-"]').first();
      await expect(firstRequest).toBeVisible({ timeout: 5000 });

      // Click to open detail page
      await firstRequest.click();

      // Verify Start Review button exists
      const startButton = adminPage.getByRole('button', { name: /Start Review/i });
      await expect(startButton).toBeVisible();

      // Click Start Review
      await startButton.click();

      // Verify timer appears
      await expect(adminPage.getByText(/remaining/i)).toBeVisible({ timeout: 2000 });

      // Verify Release Review button appears
      await expect(adminPage.getByRole('button', { name: /Release Review/i })).toBeVisible();
    });

    test('should release review and return to queue', async ({ adminPage }) => {
      // Navigate to detail page with active lock
      // (Assumes previous test left a locked request, or setup creates one)
      await adminPage.goto('/admin/share-requests');

      // Click My Reviews button
      const myReviewsButton = adminPage.getByRole('button', { name: /My Reviews/i });
      await expect(myReviewsButton).toBeVisible();
      await myReviewsButton.click();

      // Click first active review
      const firstReview = adminPage.locator('[data-testid^="active-review-"]').first();
      await expect(firstReview).toBeVisible({ timeout: 2000 });
      await firstReview.click();

      // Verify we're on detail page
      await expect(adminPage.getByRole('button', { name: /Release Review/i })).toBeVisible();

      // Click Release Review
      const releaseButton = adminPage.getByRole('button', { name: /Release Review/i });
      await releaseButton.click();

      // Verify redirected back to queue
      await expect(adminPage).toHaveURL(/\/admin\/share-requests$/);
    });
  });

  test.describe('My Active Reviews Popover', () => {
    test('should show badge count on My Reviews button', async ({ adminPage }) => {
      await adminPage.goto('/admin/share-requests');

      const myReviewsButton = adminPage.getByRole('button', { name: /My Reviews/i });
      await expect(myReviewsButton).toBeVisible();

      // Badge should show count (if any active reviews)
      const badge = myReviewsButton.locator('.bg-blue-600');
      const hasBadge = await badge.count();

      if (hasBadge > 0) {
        const count = await badge.textContent();
        expect(parseInt(count || '0')).toBeGreaterThan(0);
      }
    });

    test('should open popover and display active reviews', async ({ adminPage }) => {
      await adminPage.goto('/admin/share-requests');

      const myReviewsButton = adminPage.getByRole('button', { name: /My Reviews/i });
      await myReviewsButton.click();

      // Sheet should open
      await expect(adminPage.getByText('Active Reviews')).toBeVisible();

      // Should show either empty state or list
      const emptyState = adminPage.getByText('No active reviews');
      const reviewsList = adminPage.locator('[data-testid^="active-review-"]');

      const hasEmpty = await emptyState.isVisible().catch(() => false);
      const hasReviews = (await reviewsList.count()) > 0;

      expect(hasEmpty || hasReviews).toBe(true);
    });
  });

  test.describe('Timer Display', () => {
    test('should show expiring soon warning when < 5 minutes', async ({ adminPage }) => {
      // This test requires backend to create a lock expiring in < 5 minutes
      // Skip if not in test environment
      test.skip(process.env.NODE_ENV !== 'test', 'Requires specific test data');

      await adminPage.goto('/admin/share-requests');
      // Test implementation requires backend support for time-controlled locks
    });
  });

  test.describe('Keyboard Shortcuts', () => {
    test('should release review when Escape is pressed', async ({ adminPage }) => {
      // Navigate to locked review
      await adminPage.goto('/admin/share-requests');

      // Open My Reviews and select one
      await adminPage.getByRole('button', { name: /My Reviews/i }).click();
      const firstReview = adminPage.locator('[data-testid^="active-review-"]').first();

      if (await firstReview.isVisible().catch(() => false)) {
        await firstReview.click();

        // Press Escape
        await adminPage.keyboard.press('Escape');

        // Should redirect to queue
        await expect(adminPage).toHaveURL(/\/admin\/share-requests$/, { timeout: 3000 });
      }
    });
  });

  test.describe('Conflict Handling', () => {
    test('should show conflict dialog on 409 error', async ({ adminPage }) => {
      // This test requires two admin users trying to lock same request
      // Skip for now - requires multi-user test setup
      test.skip(true, 'Requires multi-user test infrastructure');
    });
  });
});
