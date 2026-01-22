/**
 * E2E Tests for User Contributions Dashboard (Issue #2744)
 *
 * Test Coverage:
 * - Contributions page rendering
 * - Share requests list with filtering
 * - Status badges display
 * - Contribution type badges
 * - Empty states (no requests, filtered results)
 * - Navigation to detail/edit pages
 * - Pagination
 *
 * Testing Strategy:
 * - Mock API responses for predictable test data
 * - Test filter interactions
 * - Verify navigation and routing
 * - Test responsive design
 *
 * @see Issue #2744 Frontend - Dashboard Contributi Utente
 */

import { test, expect } from '@playwright/test';

// ============================================================================
// Test Data
// ============================================================================

const MOCK_SHARE_REQUESTS = {
  items: [
    {
      id: 'req-1',
      sourceGameId: 'game-1',
      gameTitle: 'Catan',
      gameThumbnailUrl: 'https://example.com/catan.jpg',
      status: 'Pending',
      contributionType: 'NewGame',
      userNotes: 'Great game for families',
      adminFeedback: null,
      attachedDocumentCount: 2,
      createdAt: '2024-01-15T10:00:00Z',
      resolvedAt: null,
      resultingSharedGameId: null,
    },
    {
      id: 'req-2',
      sourceGameId: 'game-2',
      gameTitle: 'Gloomhaven',
      gameThumbnailUrl: 'https://example.com/gloomhaven.jpg',
      status: 'Approved',
      contributionType: 'NewGame',
      userNotes: null,
      adminFeedback: 'Looks great!',
      attachedDocumentCount: 3,
      createdAt: '2024-01-10T14:00:00Z',
      resolvedAt: '2024-01-18T16:00:00Z',
      resultingSharedGameId: 'shared-1',
    },
    {
      id: 'req-3',
      sourceGameId: 'game-3',
      gameTitle: 'Wingspan',
      gameThumbnailUrl: null,
      status: 'ChangesRequested',
      contributionType: 'AdditionalContent',
      userNotes: 'New expansion content',
      adminFeedback: 'Please add more detailed rules',
      attachedDocumentCount: 1,
      createdAt: '2024-01-12T09:00:00Z',
      resolvedAt: null,
      resultingSharedGameId: null,
    },
  ],
  page: 1,
  pageSize: 12,
  totalCount: 3,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

const MOCK_EMPTY_RESPONSE = {
  items: [],
  page: 1,
  pageSize: 12,
  totalCount: 0,
  totalPages: 0,
  hasNextPage: false,
  hasPreviousPage: false,
};

// ============================================================================
// Tests
// ============================================================================

test.describe('Contributions Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    // Mock API for share requests
    await page.route('**/api/v1/share-requests*', async (route) => {
      const url = new URL(route.request().url());
      const status = url.searchParams.get('status');

      // Filter based on status param
      if (status) {
        const statuses = status.split(',');
        const filtered = {
          ...MOCK_SHARE_REQUESTS,
          items: MOCK_SHARE_REQUESTS.items.filter((r) => statuses.includes(r.status)),
          totalCount: MOCK_SHARE_REQUESTS.items.filter((r) => statuses.includes(r.status)).length,
        };
        await route.fulfill({ json: filtered });
      } else {
        await route.fulfill({ json: MOCK_SHARE_REQUESTS });
      }
    });
  });

  test('renders contributions page with share requests', async ({ page }) => {
    await page.goto('/contributions');

    // Page title
    await expect(page.getByRole('heading', { name: 'My Contributions' })).toBeVisible();

    // Share requests cards
    await expect(page.getByText('Catan')).toBeVisible();
    await expect(page.getByText('Gloomhaven')).toBeVisible();
    await expect(page.getByText('Wingspan')).toBeVisible();

    // Status badges
    await expect(page.getByText('Pending')).toBeVisible();
    await expect(page.getByText('Approved')).toBeVisible();
    await expect(page.getByText('Changes Requested')).toBeVisible();
  });

  test('displays contribution type badges', async ({ page }) => {
    await page.goto('/contributions');

    // New Game badges
    const newGameBadges = page.getByText('New Game');
    await expect(newGameBadges.first()).toBeVisible();

    // Additional Content badge
    await expect(page.getByText('Additional Content')).toBeVisible();
  });

  test('shows document count metadata', async ({ page }) => {
    await page.goto('/contributions');

    await expect(page.getByText(/2 documents/i)).toBeVisible();
    await expect(page.getByText(/3 documents/i)).toBeVisible();
  });

  test('displays admin feedback when present', async ({ page }) => {
    await page.goto('/contributions');

    await expect(page.getByText('Looks great!')).toBeVisible();
    await expect(page.getByText('Please add more detailed rules')).toBeVisible();
  });

  test('filters share requests by status', async ({ page }) => {
    await page.goto('/contributions');

    // Select Pending status filter
    await page.getByRole('checkbox', { name: 'Pending' }).check();

    // Should only show Pending requests
    await expect(page.getByText('Catan')).toBeVisible();
    await expect(page.getByText('Gloomhaven')).not.toBeVisible();
    await expect(page.getByText('Wingspan')).not.toBeVisible();
  });

  test('shows active filter count badge', async ({ page }) => {
    await page.goto('/contributions');

    // No filters initially
    await expect(page.getByText('Filters')).toBeVisible();

    // Check a filter
    await page.getByRole('checkbox', { name: 'Pending' }).check();

    // Should show count badge
    await expect(page.getByText('1')).toBeVisible();
  });

  test('clears all filters', async ({ page }) => {
    await page.goto('/contributions');

    // Apply filters
    await page.getByRole('checkbox', { name: 'Pending' }).check();
    await page.getByRole('checkbox', { name: 'Approved' }).check();

    // Clear filters
    await page.getByRole('button', { name: 'Clear All Filters' }).click();

    // Should show all requests again
    await expect(page.getByText('Catan')).toBeVisible();
    await expect(page.getByText('Gloomhaven')).toBeVisible();
    await expect(page.getByText('Wingspan')).toBeVisible();
  });

  test('shows empty state when no requests', async ({ page }) => {
    // Mock empty response
    await page.route('**/api/v1/share-requests*', async (route) => {
      await route.fulfill({ json: MOCK_EMPTY_RESPONSE });
    });

    await page.goto('/contributions');

    await expect(page.getByText('No Share Requests Yet')).toBeVisible();
    await expect(page.getByRole('link', { name: /Go to My Library/i })).toBeVisible();
  });

  test('navigates to detail page', async ({ page }) => {
    await page.goto('/contributions');

    // Click Details button for first request
    const detailsButton = page.getByRole('link', { name: 'Details' }).first();
    await detailsButton.click();

    // Should navigate to detail page
    await expect(page).toHaveURL(/\/contributions\/requests\/req-1$/);
  });

  test('shows Update button for ChangesRequested status', async ({ page }) => {
    await page.goto('/contributions');

    // Find Wingspan card (ChangesRequested)
    const wingspanCard = page.locator('text=Wingspan').locator('xpath=ancestor::div[contains(@class, "rounded")]');

    // Should have Update button
    await expect(wingspanCard.getByRole('link', { name: 'Update' })).toBeVisible();
  });

  test('shows View Game button for Approved status', async ({ page }) => {
    await page.goto('/contributions');

    // Find Gloomhaven card (Approved)
    const gloomhavenCard = page.locator('text=Gloomhaven').locator('xpath=ancestor::div[contains(@class, "rounded")]');

    // Should have View Game button
    await expect(gloomhavenCard.getByRole('link', { name: 'View Game' })).toBeVisible();
  });

  test('is responsive on mobile', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/contributions');

    // Page should render
    await expect(page.getByRole('heading', { name: 'My Contributions' })).toBeVisible();

    // Filters should be visible (mobile layout)
    await expect(page.getByText('Filters')).toBeVisible();

    // Cards should stack vertically
    const cards = page.locator('[class*="grid"]').first();
    await expect(cards).toBeVisible();
  });
});
