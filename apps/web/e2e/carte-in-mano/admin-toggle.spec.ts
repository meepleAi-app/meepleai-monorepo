/**
 * E2E Tests for Carte in Mano — Admin Toggle & Tab Sidebar
 *
 * Verifies that the AdminToggle is conditionally rendered based on user role,
 * and that clicking it reveals the AdminTabSidebar with 6 dashboard sections.
 *
 * Uses page.context().route() for API mocking (NOT page.route()).
 * Auth bypass via PLAYWRIGHT_AUTH_BYPASS=true env var.
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function mockAuth(page: Page, role: 'Admin' | 'User') {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: role === 'Admin' ? 'admin-1' : 'test-user-1',
          email: role === 'Admin' ? 'admin@meepleai.dev' : 'test@test.com',
          displayName: role === 'Admin' ? 'Admin User' : 'Test User',
          role,
        },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );

  await page.context().route(`${API_BASE}/api/v1/auth/session`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: role === 'Admin' ? 'admin-1' : 'test-user-1',
          email: role === 'Admin' ? 'admin@meepleai.dev' : 'test@test.com',
          displayName: role === 'Admin' ? 'Admin User' : 'Test User',
          role,
        },
        isAuthenticated: true,
      }),
    })
  );

  // Catch-all for other API calls
  await page.context().route(`${API_BASE}/api/v1/**`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: [], items: [], totalCount: 0 }),
    })
  );
}

// ---------------------------------------------------------------------------
// Tests — Admin Toggle visibility
// ---------------------------------------------------------------------------

test.describe('Carte in Mano — Admin Toggle', () => {
  test('admin toggle is visible for Admin role on admin routes', async ({ page }) => {
    await mockAuth(page, 'Admin');
    // Admin layout at /admin/overview passes isAdmin=true to UnifiedShell
    await page.goto('/admin/overview', { waitUntil: 'domcontentloaded' });

    const adminToggle = page.getByTestId('admin-toggle');
    await expect(adminToggle).toBeVisible({ timeout: 10000 });
  });

  test('admin toggle is hidden for User role', async ({ page }) => {
    await mockAuth(page, 'User');
    // Regular authenticated route does NOT pass isAdmin to UnifiedShell
    await page.goto('/library', { waitUntil: 'domcontentloaded' });

    const adminToggle = page.getByTestId('admin-toggle');
    // The toggle should not be rendered for non-admin users
    await expect(adminToggle).not.toBeVisible({ timeout: 5000 });
  });
});

// ---------------------------------------------------------------------------
// Tests — Admin Tab Sidebar
// ---------------------------------------------------------------------------

test.describe('Carte in Mano — Admin Tab Sidebar', () => {
  test.beforeEach(async ({ page }) => {
    await mockAuth(page, 'Admin');
  });

  test('admin tab sidebar is visible when in admin context', async ({ page }) => {
    await page.goto('/admin/overview', { waitUntil: 'domcontentloaded' });

    // On admin routes, UnifiedShell passes isAdmin=true which auto-sets admin context
    // This means AdminTabSidebar renders instead of CardStack
    const adminSidebar = page.getByTestId('admin-tab-sidebar');
    await expect(adminSidebar).toBeVisible({ timeout: 10000 });
  });

  test('admin tab sidebar has 6 sections', async ({ page }) => {
    await page.goto('/admin/overview', { waitUntil: 'domcontentloaded' });

    const adminSidebar = page.getByTestId('admin-tab-sidebar');
    await expect(adminSidebar).toBeVisible({ timeout: 10000 });

    // 6 sections: overview, content, ai, users, system, analytics
    await expect(page.getByTestId('admin-tab-overview')).toBeVisible();
    await expect(page.getByTestId('admin-tab-content')).toBeVisible();
    await expect(page.getByTestId('admin-tab-ai')).toBeVisible();
    await expect(page.getByTestId('admin-tab-users')).toBeVisible();
    await expect(page.getByTestId('admin-tab-system')).toBeVisible();
    await expect(page.getByTestId('admin-tab-analytics')).toBeVisible();

    // Verify exactly 6 tab links
    const tabs = adminSidebar.locator('[data-testid^="admin-tab-"]');
    const count = await tabs.count();
    expect(count).toBe(6);
  });

  test('clicking admin toggle switches context between admin and user', async ({ page }) => {
    await page.goto('/admin/overview', { waitUntil: 'domcontentloaded' });

    // Initially in admin context — AdminTabSidebar visible
    const adminSidebar = page.getByTestId('admin-tab-sidebar');
    await expect(adminSidebar).toBeVisible({ timeout: 10000 });

    const adminToggle = page.getByTestId('admin-toggle');
    await expect(adminToggle).toBeVisible();

    // Toggle has role="switch" with aria-checked
    await expect(adminToggle).toHaveAttribute('aria-checked', 'true');

    // Click to switch to user mode
    await adminToggle.click();

    // Now CardStack should appear instead of AdminTabSidebar
    const cardStack = page.getByTestId('card-stack');
    await expect(cardStack).toBeVisible({ timeout: 5000 });

    // AdminTabSidebar should be gone
    await expect(adminSidebar).not.toBeVisible();

    // Toggle should show unchecked
    await expect(adminToggle).toHaveAttribute('aria-checked', 'false');
  });
});
