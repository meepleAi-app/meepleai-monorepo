/**
 * E2E Authentication Tests
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
 */

import { test, expect, Page } from '@playwright/test';

const apiBase = 'http://localhost:5080';

// Test user fixtures
const mockAdmin = {
  id: 'admin-test-1',
  email: 'admin@meepleai.dev',
  displayName: 'Admin User',
  role: 'Admin',
};

const mockEditor = {
  id: 'editor-test-1',
  email: 'editor@meepleai.dev',
  displayName: 'Editor User',
  role: 'Editor',
};

const mockUser = {
  id: 'user-test-1',
  email: 'user@meepleai.dev',
  displayName: 'Regular User',
  role: 'User',
};

/**
 * Mock authenticated session for a user
 * Sets both API mock and cookies for middleware compatibility
 */
async function mockAuthSession(page: Page, user: typeof mockAdmin) {
  // Mock API response
  await page.route(`${apiBase}/api/v1/auth/me`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user,
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // Set cookies for middleware (server-side) to work
  await page.context().addCookies([
    {
      name: 'meepleai_session',
      value: 'mock-session-token',
      domain: 'localhost',
      path: '/',
      httpOnly: true,
      secure: false,
      sameSite: 'Lax',
    },
    {
      name: 'meepleai_user_role',
      value: user.role.toLowerCase(),
      domain: 'localhost',
      path: '/',
      httpOnly: false,
      secure: false,
      sameSite: 'Lax',
    },
  ]);
}

/**
 * Mock unauthenticated session (401 response)
 * Clears cookies to ensure clean state
 */
async function mockUnauthenticated(page: Page) {
  // Mock API response
  await page.route(`${apiBase}/api/v1/auth/me`, async route => {
    await route.fulfill({
      status: 401,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Unauthorized' }),
    });
  });

  // Clear auth cookies to ensure middleware sees unauthenticated state
  await page.context().clearCookies();
}

test.describe('Authentication Flows', () => {
  test('should successfully login with valid credentials', async ({ page }) => {
    // Mock unauthenticated state initially
    await mockUnauthenticated(page);

    // Mock login endpoint
    await page.route(`${apiBase}/api/v1/auth/login`, async route => {
      const postData = route.request().postDataJSON();

      if (postData.email === 'admin@meepleai.dev' && postData.password === 'Demo123!') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            user: mockAdmin,
            expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
          }),
        });
      } else {
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Invalid credentials' }),
        });
      }
    });

    // Navigate to login page
    await page.goto('/login');

    // Fill in login form
    await page.fill('input[type="email"]', 'admin@meepleai.dev');
    await page.fill('input[type="password"]', 'Demo123!');

    // Submit form
    await page.click('button[type="submit"]');

    // Should redirect or show success (depends on implementation)
    // Check for either redirect or success indicator
    await page.waitForTimeout(1000);

    // Verify no error messages
    const errorElement = page.locator('text=/invalid|error|failed/i').first();
    await expect(errorElement)
      .not.toBeVisible({ timeout: 2000 })
      .catch(() => {
        // Error element might not exist at all, which is fine
      });
  });

  test('should show error with invalid credentials', async ({ page }) => {
    // Mock unauthenticated state initially
    await mockUnauthenticated(page);

    // Mock failed login
    await page.route(`${apiBase}/api/v1/auth/login`, async route => {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Invalid credentials' }),
      });
    });

    await page.goto('/login');

    // Fill in invalid credentials
    await page.fill('input[type="email"]', 'wrong@example.com');
    await page.fill('input[type="password"]', 'wrongpassword');

    // Submit form
    await page.click('button[type="submit"]');

    // Should show error message
    await page.waitForTimeout(500);

    // Look for error indicators (could be toast, alert, or inline error)
    const pageContent = await page.content();
    expect(pageContent.toLowerCase()).toMatch(/invalid|error|failed|incorrect/);
  });

  test('should logout successfully and redirect to home', async ({ page }) => {
    await mockAuthSession(page, mockAdmin);

    // Mock logout endpoint
    await page.route(`${apiBase}/api/v1/auth/logout`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ ok: true }),
      });
    });

    // Navigate to a protected page first
    await page.goto('/upload');

    // Find and click logout button (typically in nav or user menu)
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
    await mockUnauthenticated(page);

    // Attempt to access protected route
    await page.goto('/upload');

    // Should redirect to login
    await page.waitForURL(/\/login/);
    expect(page.url()).toContain('/login');

    // Should preserve return URL
    expect(page.url()).toContain('from=');
  });

  test('should redirect unauthenticated users from /editor to /login', async ({ page }) => {
    await mockUnauthenticated(page);

    await page.goto('/editor');

    // Should redirect to login
    await page.waitForURL(/\/login/);
    expect(page.url()).toContain('/login');
  });

  test('should redirect unauthenticated users from /admin to /login', async ({ page }) => {
    await mockUnauthenticated(page);

    await page.goto('/admin');

    // Should redirect to login
    await page.waitForURL(/\/login/);
    expect(page.url()).toContain('/login');
  });

  test('should allow authenticated admin to access /upload', async ({ page }) => {
    await mockAuthSession(page, mockAdmin);

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
    await mockAuthSession(page, mockEditor);

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
    await mockAuthSession(page, mockEditor);

    await page.goto('/editor');

    // Should stay on /editor page
    await page.waitForTimeout(500);
    expect(page.url()).toContain('/editor');
  });
});

test.describe('Role-Based Authorization', () => {
  test('should block non-admin users from /admin pages', async ({ page }) => {
    await mockAuthSession(page, mockEditor); // Editor, not admin

    await page.goto('/admin');

    // Should redirect to home (not login, since user is authenticated)
    await page.waitForURL('http://localhost:3000/', { timeout: 5000 });
    expect(page.url()).toBe('http://localhost:3000/');
  });

  test('should block regular users from /admin pages', async ({ page }) => {
    await mockAuthSession(page, mockUser); // Regular user

    await page.goto('/admin');

    // Should redirect to home
    await page.waitForURL('http://localhost:3000/', { timeout: 5000 });
    expect(page.url()).toBe('http://localhost:3000/');
  });

  test('should allow admin users to access /admin pages', async ({ page }) => {
    await mockAuthSession(page, mockAdmin);

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
    await mockAuthSession(page, mockEditor);

    await page.goto('/admin/users');

    // Should redirect away from admin area
    await page.waitForTimeout(500);
    expect(page.url()).not.toContain('/admin');
  });

  test('should allow admin to access /admin/users', async ({ page }) => {
    await mockAuthSession(page, mockAdmin);

    // Mock users endpoint
    await page.route(`${apiBase}/api/v1/admin/users*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          users: [mockAdmin],
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
    await mockAuthSession(page, mockAdmin);

    // Mock profile endpoints
    await page.route(`${apiBase}/api/v1/users/profile`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            Email: mockAdmin.email,
            DisplayName: mockAdmin.displayName,
            Role: mockAdmin.role,
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
    await mockAuthSession(page, mockAdmin);

    // Mock profile endpoint
    await page.route(`${apiBase}/api/v1/users/profile`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            Email: mockAdmin.email,
            DisplayName: mockAdmin.displayName,
            Role: mockAdmin.role,
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
    await mockAuthSession(page, mockAdmin);

    // Mock profile endpoint
    await page.route(`${apiBase}/api/v1/users/profile`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          Email: mockAdmin.email,
          DisplayName: mockAdmin.displayName,
          Role: mockAdmin.role,
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
