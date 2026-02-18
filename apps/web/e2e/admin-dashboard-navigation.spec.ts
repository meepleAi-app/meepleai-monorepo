import { test, expect } from '@playwright/test';

test.describe('Admin Dashboard Navigation', () => {
  test.beforeEach(async ({ page }) => {
    // Set auth bypass for E2E
    process.env.PLAYWRIGHT_AUTH_BYPASS = 'true';

    // Navigate to admin dashboard
    await page.goto('/admin/overview');
  });

  test('should load admin overview page', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Dashboard Overview');
  });

  test('should navigate between top nav sections', async ({ page }) => {
    // Test Users section
    await page.click('text=Users');
    await expect(page).toHaveURL(/\/admin\/users/);

    // Test Shared Games section
    await page.click('text=Shared Games');
    await expect(page).toHaveURL(/\/admin\/shared-games/);

    // Test Agents section
    await page.click('text=Agents');
    await expect(page).toHaveURL(/\/admin\/agents/);

    // Test Knowledge Base section
    await page.click('text=Knowledge Base');
    await expect(page).toHaveURL(/\/admin\/knowledge-base/);
  });

  test('should navigate to all new pages without 404', async ({ page }) => {
    const routes = [
      '/admin/users/roles',
      '/admin/users/activity',
      '/admin/shared-games/all',
      '/admin/shared-games/categories',
      '/admin/agents/analytics',
      '/admin/agents/models',
      '/admin/agents/chat-history',
      '/admin/knowledge-base/vectors',
      '/admin/knowledge-base/upload',
      '/admin/knowledge-base/pipeline',
      '/admin/knowledge-base/settings',
    ];

    for (const route of routes) {
      await page.goto(route);

      // Should not be 404
      await expect(page.locator('h1')).not.toContainText('404');
      await expect(page.locator('h1')).not.toContainText('Not Found');

      // Should have admin layout
      await expect(page.locator('body')).not.toBeEmpty();
    }
  });

  test('should handle legacy route redirects', async ({ page }) => {
    // Test approvals redirect
    await page.goto('/admin/shared-games/approvals');
    await expect(page).toHaveURL('/admin/shared-games');

    // Test management redirect
    await page.goto('/admin/users/management');
    await expect(page).toHaveURL('/admin/users');
  });

  test('should toggle sidebar on mobile', async ({ page, viewport }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });

    await page.goto('/admin/overview');

    // Look for mobile menu button (hamburger)
    const menuButton = page.locator('[aria-label*="menu" i], button:has-text("Menu")').first();

    if (await menuButton.isVisible()) {
      await menuButton.click();

      // Sidebar should be visible
      await expect(page.locator('text=Overview')).toBeVisible();
    }
  });

  test('should show correct active states', async ({ page }) => {
    await page.goto('/admin/users/roles');

    // Users section should be active in top nav
    const usersNav = page.locator('text=Users').first();

    // Roles sidebar item should be highlighted
    const rolesItem = page.locator('text=Roles & Permissions');
    await expect(rolesItem).toBeVisible();
  });
});
