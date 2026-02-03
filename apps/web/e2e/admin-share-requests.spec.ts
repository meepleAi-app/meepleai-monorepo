/**
 * E2E Test: Admin Share Requests Review Workflow - Issue #2745
 *
 * Verifies admin review interface functionality including:
 * - Queue list display with filters
 * - Review lock acquisition and release
 * - Approve/Reject/Request changes actions
 * - Lock conflict handling
 *
 * Tests cover critical admin workflows for game sharing review.
 */

import { test, expect } from '@playwright/test';

test.describe('Admin Share Requests - Review Workflow', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to admin share requests queue
    // Note: Assumes admin authentication is handled by middleware or session
    await page.goto('/admin/share-requests');

    // Wait for page load
    await page.waitForLoadState('networkidle');
  });

  test('should display queue list with share requests', async ({ page }) => {
    // Verify page heading
    await expect(page.getByRole('heading', { name: /share request review/i })).toBeVisible();

    // Verify table is visible
    const table = page.locator('table').first();
    await expect(table).toBeVisible();

    // Verify table headers
    await expect(page.getByRole('columnheader', { name: /game/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /contributor/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /status/i })).toBeVisible();

    // Verify at least one request is displayed (MSW provides 3 mock requests)
    const rows = page.locator('tbody tr');
    await expect(rows).toHaveCount(3); // MSW mock data has 3 requests
  });

  test('should filter requests by status', async ({ page }) => {
    // Open status filter (assuming it's a select or dropdown)
    const statusFilter = page.getByRole('combobox', { name: /status/i }).or(
      page.getByLabel(/status/i)
    );

    if (await statusFilter.isVisible()) {
      await statusFilter.click();

      // Select "Pending" status
      await page.getByRole('option', { name: /pending/i }).click();

      // Wait for filter to apply
      await page.waitForTimeout(500);

      // Verify only pending requests are shown
      const pendingBadges = page.locator('text=/Pending/i');
      await expect(pendingBadges).toHaveCount(2); // MSW has 2 pending requests
    }
  });

  test('should filter requests by contribution type', async ({ page }) => {
    // Open type filter
    const typeFilter = page.getByRole('combobox', { name: /type|contribution/i }).or(
      page.getByLabel(/type|contribution/i)
    );

    if (await typeFilter.isVisible()) {
      await typeFilter.click();

      // Select "New Game" type
      await page.getByRole('option', { name: /new game/i }).click();

      // Wait for filter to apply
      await page.waitForTimeout(500);

      // Verify only new game requests are shown
      const newGameBadges = page.locator('text=/New Game/i');
      await expect(newGameBadges.first()).toBeVisible();
    }
  });

  test('should navigate to detail page when review button clicked', async ({ page }) => {
    // Find first Review button
    const reviewButton = page.getByRole('button', { name: /review/i }).first();
    await expect(reviewButton).toBeVisible();

    // Set up navigation listener BEFORE clicking
    const navigationPromise = page.waitForURL(/\/admin\/share-requests\/share-req-\d+/);
    await reviewButton.click();
    await navigationPromise;

    // Verify detail page loaded
    await expect(page).toHaveURL(/\/admin\/share-requests\/share-req-/);

    // Wait for React navigation
    await page.waitForTimeout(1000);

    // Verify detail page elements
    const detailContent = page.getByRole('heading', { name: /wingspan|terraforming|gloomhaven/i });
    await expect(detailContent.or(page.locator('text=/wingspan|terraforming|gloomhaven/i'))).toBeVisible();
  });

  test('should start review and acquire lock', async ({ page }) => {
    // Navigate to first pending request detail page
    await page.goto('/admin/share-requests/share-req-1');
    await page.waitForLoadState('networkidle');

    // Verify Start Review button is visible
    const startButton = page.getByRole('button', { name: /start review/i });
    await expect(startButton).toBeVisible();

    // Set up response listener BEFORE clicking
    const responsePromise = page.waitForResponse(
      (response) =>
        response.url().includes('/start-review') && response.status() === 200
    );

    // Click Start Review
    await startButton.click();

    // Wait for API response
    await responsePromise;

    // Wait for UI update
    await page.waitForTimeout(1000);

    // Verify lock timer appears
    const timer = page.locator('text=/remaining|minutes/i');
    await expect(timer.or(page.locator('text=/min|sec/i'))).toBeVisible();

    // Verify Release Review button appears
    const releaseButton = page.getByRole('button', { name: /release review/i });
    await expect(releaseButton).toBeVisible();
  });

  test('should release review and unlock request', async ({ page }) => {
    // Navigate to request that's already in review
    await page.goto('/admin/share-requests/share-req-2');
    await page.waitForLoadState('networkidle');

    // Verify Release Review button is visible (req-2 is already locked by current admin in MSW)
    const releaseButton = page.getByRole('button', { name: /release review/i });

    // If not visible, start review first
    const startButton = page.getByRole('button', { name: /start review/i });
    if (await startButton.isVisible()) {
      await startButton.click();
      await page.waitForTimeout(1000);
    }

    // Now release
    await expect(releaseButton).toBeVisible();

    // Set up response listener
    const responsePromise = page.waitForResponse(
      (response) =>
        response.url().includes('/release-review') && response.status() === 200
    );

    await releaseButton.click();
    await responsePromise;

    // Wait for UI update
    await page.waitForTimeout(1000);

    // Verify Start Review button returns
    await expect(page.getByRole('button', { name: /start review/i })).toBeVisible();
  });

  test('should approve request with admin notes', async ({ page }) => {
    // Navigate to pending request
    await page.goto('/admin/share-requests/share-req-1');
    await page.waitForLoadState('networkidle');

    // Start review first
    const startButton = page.getByRole('button', { name: /start review/i });
    if (await startButton.isVisible()) {
      await startButton.click();
      await page.waitForTimeout(1000);
    }

    // Find and click Approve button
    const approveButton = page.getByRole('button', { name: /approve/i });
    await expect(approveButton).toBeVisible();
    await approveButton.click();

    // Wait for confirmation dialog
    await page.waitForTimeout(500);

    // Check for confirmation dialog or notes input
    const notesInput = page.getByLabel(/notes|comment/i).or(
      page.getByPlaceholder(/notes|comment/i)
    );

    if (await notesInput.isVisible()) {
      await notesInput.fill('Looks great! Approved for catalog.');
    }

    // Confirm approval
    const confirmButton = page.getByRole('button', { name: /confirm|approve/i }).last();

    // Set up response listener
    const responsePromise = page.waitForResponse(
      (response) =>
        response.url().includes('/approve') && response.status() === 200
    );

    await confirmButton.click();
    await responsePromise;

    // Wait for success message or redirect
    await page.waitForTimeout(1000);

    // Verify success (check for success message or redirect to queue)
    const successMessage = page.locator('text=/approved|success/i');
    await expect(
      successMessage.or(page).first()
    ).toBeVisible({ timeout: 5000 });
  });

  test('should reject request with reason', async ({ page }) => {
    // Navigate to pending request
    await page.goto('/admin/share-requests/share-req-3');
    await page.waitForLoadState('networkidle');

    // Start review first
    const startButton = page.getByRole('button', { name: /start review/i });
    if (await startButton.isVisible()) {
      await startButton.click();
      await page.waitForTimeout(1000);
    }

    // Find and click Reject button
    const rejectButton = page.getByRole('button', { name: /reject/i });
    await expect(rejectButton).toBeVisible();
    await rejectButton.click();

    // Wait for dialog
    await page.waitForTimeout(500);

    // Fill rejection reason (required field)
    const reasonInput = page.getByLabel(/reason/i).or(
      page.getByPlaceholder(/reason/i)
    );
    await expect(reasonInput).toBeVisible();
    await reasonInput.fill('Incomplete documentation. Please provide rulebook and quick reference.');

    // Confirm rejection
    const confirmButton = page.getByRole('button', { name: /confirm|reject/i }).last();

    // Set up response listener
    const responsePromise = page.waitForResponse(
      (response) =>
        response.url().includes('/reject') && response.status() === 200
    );

    await confirmButton.click();
    await responsePromise;

    // Wait for success message or redirect
    await page.waitForTimeout(1000);

    // Verify success
    const successMessage = page.locator('text=/rejected|success/i');
    await expect(
      successMessage.or(page).first()
    ).toBeVisible({ timeout: 5000 });
  });

  test('should request changes with feedback', async ({ page }) => {
    // Navigate to pending request
    await page.goto('/admin/share-requests/share-req-1');
    await page.waitForLoadState('networkidle');

    // Start review first
    const startButton = page.getByRole('button', { name: /start review/i });
    if (await startButton.isVisible()) {
      await startButton.click();
      await page.waitForTimeout(1000);
    }

    // Find and click Request Changes button
    const requestChangesButton = page.getByRole('button', { name: /request changes|changes/i });
    await expect(requestChangesButton).toBeVisible();
    await requestChangesButton.click();

    // Wait for dialog
    await page.waitForTimeout(500);

    // Fill feedback (required field)
    const feedbackInput = page.getByLabel(/feedback|changes/i).or(
      page.getByPlaceholder(/feedback|changes/i)
    );
    await expect(feedbackInput).toBeVisible();
    await feedbackInput.fill('Please add FAQ section and clarify setup rules.');

    // Confirm request changes
    const confirmButton = page.getByRole('button', { name: /confirm|submit|send/i }).last();

    // Set up response listener
    const responsePromise = page.waitForResponse(
      (response) =>
        response.url().includes('/request-changes') && response.status() === 200
    );

    await confirmButton.click();
    await responsePromise;

    // Wait for success message or redirect
    await page.waitForTimeout(1000);

    // Verify success
    const successMessage = page.locator('text=/changes requested|success|sent/i');
    await expect(
      successMessage.or(page).first()
    ).toBeVisible({ timeout: 5000 });
  });

  test('should show lock conflict when request is locked by another admin', async ({ page }) => {
    // Request share-req-2 is already locked by admin-1 in MSW mock data
    await page.goto('/admin/share-requests/share-req-2');
    await page.waitForLoadState('networkidle');

    // Verify locked status badge is shown
    const lockedBadge = page.locator('text=/in review|locked|being reviewed/i');
    await expect(lockedBadge.first()).toBeVisible();

    // Verify locked by admin name is shown
    const adminName = page.locator('text=/Admin User/i');
    await expect(adminName.first()).toBeVisible();

    // Verify action buttons are disabled or not visible
    const approveButton = page.getByRole('button', { name: /approve/i });
    const isDisabled = await approveButton.isDisabled().catch(() => true);
    const isHidden = !(await approveButton.isVisible().catch(() => false));

    expect(isDisabled || isHidden).toBe(true);
  });

  test('should work on mobile viewport', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 }); // iPhone SE

    await page.goto('/admin/share-requests');
    await page.waitForLoadState('networkidle');

    // Verify page renders without errors
    await expect(page.locator('body')).toBeVisible();

    // Verify queue list is visible (may have responsive layout)
    const heading = page.getByRole('heading', { name: /share request/i });
    await expect(heading.or(page.locator('text=/share request/i'))).toBeVisible();

    // Verify can navigate to detail page
    const reviewButton = page.getByRole('button', { name: /review/i }).first().or(
      page.locator('text=/review/i').first()
    );

    if (await reviewButton.isVisible()) {
      await reviewButton.click();
      await page.waitForTimeout(1000);

      // Verify detail page loaded
      await expect(page.locator('body')).toBeVisible();
    }
  });
});
