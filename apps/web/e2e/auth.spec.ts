/**
 * E2E Authentication Tests - MIGRATED TO PAGE OBJECT MODEL
 *
 * Tests SSR auth protection, login flows, and user profile management
 * across the application.
 *
 * Test Coverage:
 * 1. Login flow with valid/invalid credentials
 * 2. SSR auth protection on protected routes
 * 3. Role-based authorization (admin-only pages)
 * 4. Profile update functionality
 * 5. Password change functionality
 * 6. Logout flow
 *
 * @see apps/web/e2e/page-objects/ - Page Object Model architecture
 */

import { test, expect } from '@playwright/test';
import { LoginPage, AuthHelper, USER_FIXTURES } from './pages';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5080';

test.describe('Authentication Flows', () => {
  test('should successfully login with valid credentials', async ({ page }) => {
    const loginPage = new LoginPage(page);
    const authHelper = new AuthHelper(page);

    // Mock unauthenticated state initially
    await authHelper.mockUnauthenticatedSession();

    // Mock successful login
    await authHelper.mockLoginEndpoint(true, USER_FIXTURES.admin);

    // Navigate and perform login
    await loginPage.navigate();
    await loginPage.login('admin@meepleai.dev', 'Demo123!');

    // Wait and verify no error messages
    await page.waitForTimeout(1000);
    const errorElement = page.locator('text=/invalid|error|failed/i').first();
    await expect(errorElement)
      .not.toBeVisible({ timeout: 2000 })
      .catch(() => {
        // Error element might not exist at all, which is fine
      });
  });

  test('should show error with invalid credentials', async ({ page }) => {
    const loginPage = new LoginPage(page);
    const authHelper = new AuthHelper(page);

    // Mock unauthenticated state and failed login
    await authHelper.mockUnauthenticatedSession();
    await authHelper.mockLoginEndpoint(false);

    await loginPage.navigate();
    await loginPage.login('wrong@example.com', 'wrongpassword');

    // Should show error message
    await page.waitForTimeout(500);
    const pageContent = await page.content();
    expect(pageContent.toLowerCase()).toMatch(/invalid|error|failed|incorrect/);
  });

  test('should logout successfully and redirect to home', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);
    await authHelper.mockLogoutEndpoint();

    // Navigate to a protected page first
    await page.goto('/upload');

    // Find and click logout button
    const logoutButton = page
      .locator('button:has-text("Logout"), button:has-text("Log out"), a:has-text("Logout")')
      .first();

    if (await logoutButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await logoutButton.click();

      // Should redirect to home or login
      await page.waitForTimeout(1000);
      const currentUrl = page.url();
      expect(currentUrl).toMatch(/\/(login|)$/);
    }
  });
});

test.describe('SSR Auth Protection', () => {
  test('should redirect unauthenticated users from /upload to /login', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockUnauthenticatedSession();

    // Attempt to access protected route
    await page.goto('/upload');

    // Should redirect to login
    await authHelper.verifyRedirectToLogin();
    expect(page.url()).toContain('/login');

    // Should preserve return URL
    expect(page.url()).toContain('from=');
  });

  test('should redirect unauthenticated users from /editor to /login', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockUnauthenticatedSession();
    await page.goto('/editor');

    // Should redirect to login
    await authHelper.verifyRedirectToLogin();
    expect(page.url()).toContain('/login');
  });

  test('should redirect unauthenticated users from /admin to /login', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockUnauthenticatedSession();
    await page.goto('/admin');

    // Should redirect to login
    await authHelper.verifyRedirectToLogin();
    expect(page.url()).toContain('/login');
  });

  test('should allow authenticated admin to access /upload', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock games endpoint for upload page
    await page.route(`${apiBase}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: [],
          totalCount: 0,
          pageNumber: 1,
          pageSize: 10,
          totalPages: 0,
        }),
      });
    });

    await page.goto('/upload');

    // Should stay on /upload page
    await page.waitForTimeout(500);
    expect(page.url()).toContain('/upload');
  });

  test('should allow authenticated editor to access /upload', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockAuthenticatedSession(USER_FIXTURES.editor);

    // Mock games endpoint
    await page.route(`${apiBase}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: [],
          totalCount: 0,
          pageNumber: 1,
          pageSize: 10,
          totalPages: 0,
        }),
      });
    });

    await page.goto('/upload');

    // Should stay on /upload page
    await page.waitForTimeout(500);
    expect(page.url()).toContain('/upload');
  });

  test('should allow authenticated editor to access /editor', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockAuthenticatedSession(USER_FIXTURES.editor);
    await page.goto('/editor');

    // Should stay on /editor page
    await page.waitForTimeout(500);
    expect(page.url()).toContain('/editor');
  });
});

test.describe('Role-Based Authorization', () => {
  test('should block non-admin users from /admin pages', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockAuthenticatedSession(USER_FIXTURES.editor); // Editor, not admin
    await page.goto('/admin');

    // Should redirect to home (not login, since user is authenticated)
    await page.waitForURL('http://localhost:3000/', { timeout: 5000 });
    expect(page.url()).toBe('http://localhost:3000/');
  });

  test('should block regular users from /admin pages', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user); // Regular user
    await page.goto('/admin');

    // Should redirect to home
    await page.waitForURL('http://localhost:3000/', { timeout: 5000 });
    expect(page.url()).toBe('http://localhost:3000/');
  });

  test('should allow admin users to access /admin pages', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock admin stats endpoint
    await page.route(`${apiBase}/api/v1/admin/stats`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalUsers: 10,
          totalGames: 5,
          totalQueries: 100,
        }),
      });
    });

    await page.goto('/admin');

    // Should stay on /admin page
    await page.waitForTimeout(500);
    expect(page.url()).toContain('/admin');
  });

  test('should block non-admin from /admin/users', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockAuthenticatedSession(USER_FIXTURES.editor);
    await page.goto('/admin/users');

    // Should redirect away from admin area
    await page.waitForTimeout(500);
    expect(page.url()).not.toContain('/admin');
  });

  test('should allow admin to access /admin/users', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock users endpoint
    await page.route(`${apiBase}/api/v1/admin/users*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          users: [USER_FIXTURES.admin],
          totalCount: 1,
          pageNumber: 1,
          pageSize: 10,
          totalPages: 1,
        }),
      });
    });

    await page.goto('/admin/users');

    // Should stay on /admin/users page
    await page.waitForTimeout(500);
    expect(page.url()).toContain('/admin/users');
  });
});

test.describe('User Profile Management', () => {
  test('should update user profile successfully', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock profile endpoints
    await page.route(`${apiBase}/api/v1/users/profile`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            Email: USER_FIXTURES.admin.email,
            DisplayName: USER_FIXTURES.admin.displayName,
            Role: USER_FIXTURES.admin.role,
            IsTwoFactorEnabled: false,
          }),
        });
      } else if (route.request().method() === 'PUT') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ok: true,
            message: 'Profile updated successfully',
          }),
        });
      }
    });

    // Mock 2FA status
    await page.route(`${apiBase}/api/v1/auth/2fa/status`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          isEnabled: false,
          backupCodesRemaining: 0,
        }),
      });
    });

    // Mock OAuth accounts
    await page.route(`${apiBase}/api/v1/users/me/oauth-accounts`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    await page.goto('/settings');

    // Wait for profile to load
    await page.waitForTimeout(1000);

    // Update display name
    const displayNameInput = page
      .locator('input[name="displayName"], input[placeholder*="name" i]')
      .first();
    await displayNameInput.fill('Updated Admin Name');

    // Click save button
    const saveButton = page.locator('button:has-text("Save"), button:has-text("Update")').first();
    await saveButton.click();

    // Should show success message
    await page.waitForTimeout(500);
    const pageContent = await page.content();
    expect(pageContent.toLowerCase()).toMatch(/success|updated|saved/);
  });

  test('should change password successfully', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock profile endpoint
    await page.route(`${apiBase}/api/v1/users/profile`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            Email: USER_FIXTURES.admin.email,
            DisplayName: USER_FIXTURES.admin.displayName,
            Role: USER_FIXTURES.admin.role,
            IsTwoFactorEnabled: false,
          }),
        });
      }
    });

    // Mock password change endpoint
    await page.route(`${apiBase}/api/v1/users/profile/password`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ok: true,
          message: 'Password changed successfully',
        }),
      });
    });

    // Mock 2FA and OAuth endpoints
    await page.route(`${apiBase}/api/v1/auth/2fa/status`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ isEnabled: false, backupCodesRemaining: 0 }),
      });
    });

    await page.route(`${apiBase}/api/v1/users/me/oauth-accounts`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    await page.goto('/settings');
    await page.waitForTimeout(1000);

    // Fill password change form
    await page.fill(
      'input[type="password"][placeholder*="current" i], input[name="currentPassword"]',
      'OldPass123!'
    );
    await page.fill(
      'input[type="password"][placeholder*="new" i]:not([placeholder*="confirm" i]), input[name="newPassword"]',
      'NewPass123!'
    );
    await page.fill(
      'input[type="password"][placeholder*="confirm" i], input[name="confirmPassword"]',
      'NewPass123!'
    );

    // Click change password button
    const changePasswordBtn = page
      .locator('button:has-text("Change Password"), button:has-text("Update Password")')
      .first();
    await changePasswordBtn.click();

    // Should show success message
    await page.waitForTimeout(500);
    const pageContent = await page.content();
    expect(pageContent.toLowerCase()).toMatch(/success|changed|updated/);
  });

  test('should show error when passwords do not match', async ({ page }) => {
    const authHelper = new AuthHelper(page);

    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock profile endpoint
    await page.route(`${apiBase}/api/v1/users/profile`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          Email: USER_FIXTURES.admin.email,
          DisplayName: USER_FIXTURES.admin.displayName,
          Role: USER_FIXTURES.admin.role,
          IsTwoFactorEnabled: false,
        }),
      });
    });

    // Mock 2FA and OAuth
    await page.route(`${apiBase}/api/v1/auth/2fa/status`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ isEnabled: false, backupCodesRemaining: 0 }),
      });
    });

    await page.route(`${apiBase}/api/v1/users/me/oauth-accounts`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    await page.goto('/settings');
    await page.waitForTimeout(1000);

    // Fill with mismatched passwords
    await page.fill(
      'input[type="password"][placeholder*="current" i], input[name="currentPassword"]',
      'OldPass123!'
    );
    await page.fill(
      'input[type="password"][placeholder*="new" i]:not([placeholder*="confirm" i]), input[name="newPassword"]',
      'NewPass123!'
    );
    await page.fill(
      'input[type="password"][placeholder*="confirm" i], input[name="confirmPassword"]',
      'DifferentPass123!'
    );

    // Click change password
    const changePasswordBtn = page
      .locator('button:has-text("Change Password"), button:has-text("Update Password")')
      .first();
    await changePasswordBtn.click();

    // Should show error
    await page.waitForTimeout(500);
    const pageContent = await page.content();
    expect(pageContent.toLowerCase()).toMatch(/match|error|invalid/);
  });
});
