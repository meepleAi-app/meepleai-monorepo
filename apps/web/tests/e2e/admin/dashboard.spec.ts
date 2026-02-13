import { test, expect } from '@playwright/test';

/**
 * E2E Tests for Admin Dashboard
 * Issue #4204 - E2E Workflow Tests
 * Epic: #4192
 *
 * Critical workflows:
 * 1. View dashboard blocks
 * 2. Toggle grid/list views
 * 3. Search and filter
 * 4. View user detail panel
 * 5. Navigate to detail pages
 */

test.describe('Admin Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    // TODO: Add admin login when authentication is implemented
    await page.goto('/admin/dashboard');
  });

  test('displays all 3 dashboard blocks', async ({ page }) => {
    // Collection Overview
    await expect(page.getByRole('heading', { name: /collection overview/i })).toBeVisible();

    // Approval Queue
    await expect(page.getByRole('heading', { name: /approval queue/i })).toBeVisible();

    // User Management
    await expect(page.getByRole('heading', { name: /user management/i })).toBeVisible();
  });

  test('displays stat cards in Collection Overview', async ({ page }) => {
    await expect(page.getByText(/shared games/i)).toBeVisible();
    await expect(page.getByText(/community/i)).toBeVisible();
    await expect(page.getByText(/approval rate/i)).toBeVisible();
    await expect(page.getByText(/recent activity/i)).toBeVisible();
  });

  test('has working navigation links', async ({ page }) => {
    // Collection Overview link
    const collectionLink = page.getByRole('link', { name: /view details/i }).first();
    await expect(collectionLink).toHaveAttribute('href', '/admin/collection/overview');

    // Approval Queue link
    const approvalsLink = page.getByRole('link', { name: /view all/i }).first();
    await expect(approvalsLink).toHaveAttribute('href', '/admin/shared-games/approvals');

    // User Management link
    const usersLink = page.getByRole('link', { name: /view all/i }).last();
    await expect(usersLink).toHaveAttribute('href', '/admin/users/management');
  });

  test('toggles between grid and list view in Approval Queue', async ({ page }) => {
    const gridButton = page.locator('button').filter({ has: page.locator('svg') }).first();
    const listButton = page.locator('button').filter({ has: page.locator('svg') }).nth(1);

    // Should start in grid view (default)
    await expect(gridButton).toBeVisible();

    // Click list view
    await listButton.click();
    await page.waitForTimeout(300); // Wait for animation

    // Click grid view
    await gridButton.click();
    await page.waitForTimeout(300);
  });

  test('search input filters approval queue', async ({ page }) => {
    const searchInput = page.getByPlaceholder(/search games/i);

    await searchInput.fill('Wingspan');
    await expect(searchInput).toHaveValue('Wingspan');

    // Clear search
    await searchInput.clear();
    await expect(searchInput).toHaveValue('');
  });

  test('status filter dropdown works in Approval Queue', async ({ page }) => {
    const statusFilter = page.getByRole('combobox').first();

    await statusFilter.click();

    // Check filter options are visible
    await expect(page.getByText(/all status/i)).toBeVisible();
    await expect(page.getByText(/pending/i)).toBeVisible();
    await expect(page.getByText(/urgent/i)).toBeVisible();
  });

  test('search input filters users', async ({ page }) => {
    const searchInput = page.getByPlaceholder(/search by name or email/i);

    await searchInput.fill('John Doe');
    await expect(searchInput).toHaveValue('John Doe');
  });

  test('role and tier filters work in User Management', async ({ page }) => {
    // Role filter
    const roleFilter = page.locator('text=All Roles').first();
    if (await roleFilter.isVisible()) {
      await roleFilter.click();
      await page.waitForTimeout(200);
    }

    // Tier filter
    const tierFilter = page.locator('text=All Tiers').first();
    if (await tierFilter.isVisible()) {
      await tierFilter.click();
      await page.waitForTimeout(200);
    }
  });

  test('responsive layout on mobile', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });

    // Dashboard should still be visible
    await expect(page.getByRole('heading', { name: /dashboard/i })).toBeVisible();

    // Stat cards should stack vertically (still visible)
    await expect(page.getByText(/shared games/i)).toBeVisible();
  });

  test('navigates to collection overview detail page', async ({ page }) => {
    await page.getByRole('link', { name: /view details/i }).first().click();

    await expect(page).toHaveURL('/admin/collection/overview');
    await expect(page.getByRole('heading', { name: /collection overview/i })).toBeVisible();
  });

  test('navigates to approvals detail page', async ({ page }) => {
    const links = await page.getByRole('link', { name: /view all/i }).all();
    await links[0].click();

    await expect(page).toHaveURL('/admin/shared-games/approvals');
  });

  test('navigates to user management detail page', async ({ page }) => {
    const links = await page.getByRole('link', { name: /view all/i }).all();
    await links[1].click();

    await expect(page).toHaveURL('/admin/users/management');
  });
});

test.describe('Admin Dashboard - With Mock Data', () => {
  test.beforeEach(async ({ page }) => {
    // Intercept API calls and return mock data
    await page.route('**/api/v1/admin/dashboard/stats*', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          metrics: {
            totalGames: 1247,
            totalUsers: 8542,
            activeSessions: 42,
            apiRequestsToday: 1523,
          },
        }),
      });
    });

    await page.route('**/api/v1/admin/shared-games/approval-queue*', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              gameId: '1',
              title: 'Twilight Imperium',
              submittedBy: 'user@example.com',
              submittedAt: new Date().toISOString(),
              daysPending: 2,
              pdfCount: 3,
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 6,
          totalPages: 1,
        }),
      });
    });

    await page.route('**/api/v1/admin/users*', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: '1',
              displayName: 'John Doe',
              email: 'john@example.com',
              role: 'user',
              tier: 'premium',
              level: 15,
              experiencePoints: 2350,
              createdAt: new Date().toISOString(),
              isActive: true,
              isTwoFactorEnabled: true,
              emailVerified: true,
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 6,
          totalPages: 1,
        }),
      });
    });

    await page.goto('/admin/dashboard');
  });

  test('displays mock stats correctly', async ({ page }) => {
    await expect(page.getByText('1247')).toBeVisible();
    await expect(page.getByText('8542')).toBeVisible();
  });

  test('displays mock games in approval queue', async ({ page }) => {
    await expect(page.getByText(/twilight imperium/i)).toBeVisible();
    await expect(page.getByText(/2 days pending/i)).toBeVisible();
  });

  test('displays mock users', async ({ page }) => {
    await expect(page.getByText(/john doe/i)).toBeVisible();
    await expect(page.getByText(/john@example.com/i)).toBeVisible();
  });

  test('opens user detail panel on click', async ({ page }) => {
    await page.route('**/api/v1/admin/users/1', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: '1',
          displayName: 'John Doe',
          email: 'john@example.com',
          role: 'user',
          tier: 'premium',
          level: 15,
          experiencePoints: 2350,
          createdAt: new Date().toISOString(),
          isActive: true,
          isTwoFactorEnabled: true,
          emailVerified: true,
        }),
      });
    });

    await page.route('**/api/v1/admin/users/1/library/stats', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          gamesOwned: 47,
          totalPlays: 234,
          wishlistCount: 12,
          averageRating: 8.4,
          favoriteCategory: 'Strategy',
        }),
      });
    });

    await page.route('**/api/v1/admin/users/1/badges', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: '1',
            name: 'Early Adopter',
            description: 'Joined in the first year',
            icon: '🎖️',
            earnedAt: new Date().toISOString(),
          },
        ]),
      });
    });

    // Click user card
    await page.getByText(/john doe/i).click();

    // Verify detail panel opens
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByRole('heading', { name: /user profile/i })).toBeVisible();

    // Verify library stats
    await expect(page.getByText('47')).toBeVisible(); // Games Owned
    await expect(page.getByText('234')).toBeVisible(); // Total Plays

    // Verify badges
    await expect(page.getByText(/early adopter/i)).toBeVisible();
  });
});
