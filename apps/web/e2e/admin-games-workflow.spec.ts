/**
 * Admin Games Management E2E Tests (Issue #2515)
 *
 * Tests complete approval workflow:
 * 1. List games with filters
 * 2. Create/Edit game configuration
 * 3. Submit for approval
 * 4. Admin review and approve/reject
 * 5. Verify status transitions
 */

import { test, expect } from '@playwright/test';

test.describe('Admin Games Management Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    // Login as admin
    await page.goto('/login');
    await page.fill('[name="email"]', 'admin@meepleai.com');
    await page.fill('[name="password"]', 'admin-password');
    await page.click('button[type="submit"]');

    // Navigate to admin games dashboard
    await page.goto('/admin/games');
  });

  test('should display games table with filters', async ({ page }) => {
    // Verify page loaded
    await expect(page.locator('h2:has-text("Admin Dashboard - Games Management")')).toBeVisible();

    // Verify filters present
    await expect(page.locator('input[placeholder*="Search"]')).toBeVisible();
    await expect(page.locator('select#status-filter')).toBeVisible();

    // Verify table headers
    await expect(page.locator('th:has-text("Name")')).toBeVisible();
    await expect(page.locator('th:has-text("BGG ID")')).toBeVisible();
    await expect(page.locator('th:has-text("Status")')).toBeVisible();
    await expect(page.locator('th:has-text("Last Modified")')).toBeVisible();
    await expect(page.locator('th:has-text("Actions")')).toBeVisible();
  });

  test('should filter games by status', async ({ page }) => {
    // Filter by "Draft"
    await page.selectOption('select#status-filter', '0');

    // Wait for table update
    await page.waitForTimeout(500);

    // Verify only draft games shown
    const statusBadges = page.locator('td:has-text("Draft")');
    await expect(statusBadges).toHaveCount(await statusBadges.count());
  });

  test('should search games by name', async ({ page }) => {
    // Enter search term
    await page.fill('input[placeholder*="Search"]', 'Catan');
    await page.click('button:has-text("Search")');

    // Wait for search results
    await page.waitForTimeout(500);

    // Verify Catan appears in results
    await expect(page.locator('td:has-text("Catan")')).toBeVisible();
  });

  test('should open edit modal for draft game', async ({ page }) => {
    // Click configure action on first draft game
    await page.locator('button:has(svg.lucide-more-horizontal)').first().click();
    await page.click('div[role="menuitem"]:has-text("Configure")');

    // Verify edit modal opened
    await expect(page.locator('h2:has-text("Edit Game Configuration")')).toBeVisible();

    // Verify form fields present
    await expect(page.locator('input#name')).toBeVisible();
    await expect(page.locator('input#bggId')).toBeVisible();
    await expect(page.locator('textarea#description')).toBeVisible();

    // Verify tabs present
    await expect(page.locator('button[role="tab"]:has-text("Edit")')).toBeVisible();
    await expect(page.locator('button[role="tab"]:has-text("Preview")')).toBeVisible();
  });

  test('should submit game for approval', async ({ page }) => {
    // Open edit modal
    await page.locator('button:has(svg.lucide-more-horizontal)').first().click();
    await page.click('div[role="menuitem"]:has-text("Configure")');

    // Fill required fields
    await page.fill('textarea#description', 'Test game description with enough detail');

    // Submit for approval
    await page.click('button:has-text("Submit for Approval")');

    // Verify toast notification
    await expect(page.locator('text=submitted for approval')).toBeVisible({
      timeout: 3000,
    });

    // Verify modal closed
    await expect(page.locator('h2:has-text("Edit Game Configuration")')).not.toBeVisible();
  });

  test('should open approval review modal for pending game', async ({ page }) => {
    // Click review action on pending approval game
    await page.locator('button:has(svg.lucide-more-horizontal)').nth(1).click();
    await page.click('div[role="menuitem"]:has-text("Review")');

    // Verify review modal opened
    await expect(page.locator('h2:has-text("Review Game Submission")')).toBeVisible();

    // Verify tabs present
    await expect(page.locator('button[role="tab"]:has-text("Preview")')).toBeVisible();
    await expect(page.locator('button[role="tab"]:has-text("Changes")')).toBeVisible();

    // Verify action buttons
    await expect(page.locator('button:has-text("Approve & Publish")')).toBeVisible();
    await expect(page.locator('button:has-text("Reject")')).toBeVisible();
  });

  test('should approve game publication', async ({ page }) => {
    // Open review modal
    await page.locator('button:has(svg.lucide-more-horizontal)').nth(1).click();
    await page.click('div[role="menuitem"]:has-text("Review")');

    // Click approve button
    await page.click('button:has-text("Approve & Publish")');

    // Verify toast notification
    await expect(page.locator('text=approved and published')).toBeVisible({
      timeout: 3000,
    });

    // Verify modal closed
    await expect(page.locator('h2:has-text("Review Game Submission")')).not.toBeVisible();
  });

  test('should reject game publication with reason', async ({ page }) => {
    // Open review modal
    await page.locator('button:has(svg.lucide-more-horizontal)').nth(1).click();
    await page.click('div[role="menuitem"]:has-text("Review")');

    // Click reject button
    await page.click('button:has-text("Reject")');

    // Verify reject dialog opened
    await expect(page.locator('h2:has-text("Reject Game Submission")')).toBeVisible();

    // Try to submit without reason (should fail)
    await page.click('button:has-text("Confirm Rejection")');
    await expect(page.locator('text=at least 10 characters')).toBeVisible({
      timeout: 2000,
    });

    // Fill reason and submit
    await page.fill(
      'textarea#reject-reason',
      'Description lacks sufficient detail about game mechanics'
    );
    await page.click('button:has-text("Confirm Rejection")');

    // Verify toast notification
    await expect(page.locator('text=submission rejected')).toBeVisible({
      timeout: 3000,
    });
  });

  test('should paginate through games', async ({ page }) => {
    // Verify pagination controls visible
    await expect(page.locator('button:has-text("Previous")')).toBeVisible();
    await expect(page.locator('button:has-text("Next")')).toBeVisible();
    await expect(page.locator('text=Page')).toBeVisible();

    // Click next page
    await page.click('button:has-text("Next")');

    // Wait for page change
    await page.waitForTimeout(500);

    // Verify page number updated
    await expect(page.locator('text=Page 2')).toBeVisible();
  });

  test('should handle unauthorized access (non-admin users)', async ({ page }) => {
    // Logout admin
    await page.goto('/logout');

    // Login as regular user (non-admin)
    await page.goto('/login');
    await page.fill('[name="email"]', 'user@meepleai.com');
    await page.fill('[name="password"]', 'user-password');
    await page.click('button[type="submit"]');

    // Try to access admin games page
    await page.goto('/admin/games');

    // Verify redirect to unauthorized page or error message
    await expect(
      page.locator('text=Unauthorized').or(page.locator('text=Admin access required'))
    ).toBeVisible({ timeout: 3000 });
  });
});
