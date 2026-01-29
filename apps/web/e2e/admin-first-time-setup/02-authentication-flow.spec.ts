/**
 * E2E Test Suite 2: Authentication Flow
 *
 * Tests admin first login scenarios:
 * - Email/password login (no 2FA by default)
 * - Session creation and JWT handling
 * - OAuth login flows (Google, GitHub, Discord)
 * - Dashboard access after login
 *
 * Strategy: Full-stack assertions with response interception
 * Execution: Serial (maintains login state across tests)
 */

import { test, expect } from '@playwright/test';

import { loginAsAdmin } from '../utils/admin-setup-helpers';

test.describe.configure({ mode: 'serial' });
test.describe('Authentication Flow', () => {
  test.use({ storageState: undefined }); // Start with no auth

  test('admin first login with email/password (no 2FA)', async ({ page }) => {
    // Navigate to login page
    await page.goto('/login');

    // Verify login form is visible
    await expect(page.locator('input[name="email"]')).toBeVisible();
    await expect(page.locator('input[name="password"]')).toBeVisible();

    // Fill credentials
    const adminEmail = process.env.ADMIN_EMAIL || 'admin@meepleai.dev';
    const adminPassword = process.env.ADMIN_PASSWORD || 'pVKOMQNK0tFNgGlX';

    await page.fill('input[name="email"]', adminEmail);
    await page.fill('input[name="password"]', adminPassword);

    // Set up response listener BEFORE clicking submit
    const responsePromise = page.waitForResponse(
      (response) => response.url().includes('/api/v1/auth/login') && response.status() === 200
    );

    // Submit login form
    await page.click('button[type="submit"]');

    // Wait for and verify login response
    const response = await responsePromise;
    const data = await response.json();

    // Verify user data (sessionToken is in HTTP-only cookie, not JSON)
    expect(data.user).toBeDefined();
    expect(data.user.email).toBe(adminEmail.toLowerCase());
    expect(data.user.role.toLowerCase()).toBe('admin');
    expect(data.user.isTwoFactorEnabled).toBe(false);
    expect(data.expiresAt).toBeDefined(); // Session expiration

    // Verify redirect to dashboard
    await page.waitForURL(/\/(dashboard|admin)/, { timeout: 10000 });
    expect(page.url()).toMatch(/\/(dashboard|admin)/);

    // Verify dashboard loaded successfully
    await expect(page.locator('h1:has-text("Dashboard"), h1:has-text("Admin")')).toBeVisible({
      timeout: 5000,
    });

    console.log('✅ Admin logged in successfully');
    console.log(`   Redirected to: ${page.url()}`);
  });

  test('should persist session across page reloads', async ({ page, context }) => {
    // Login first
    await loginAsAdmin(page);

    // Get cookies to verify session persistence
    const cookies = await context.cookies();
    // Session cookie may have various names: session, sessionToken, .AspNetCore.Session, etc.
    const sessionCookie = cookies.find(
      (c) =>
        c.name.toLowerCase().includes('session') ||
        c.name.toLowerCase().includes('auth') ||
        c.name.startsWith('.')
    );

    expect(sessionCookie).toBeDefined();
    console.log(`✅ Session cookie found: ${sessionCookie?.name}`);

    // Reload page
    await page.reload();

    // Verify still authenticated (not redirected to login)
    await page.waitForLoadState('networkidle');
    expect(page.url()).toMatch(/\/(dashboard|admin)/);

    // Verify user profile accessible
    const profileResponse = await page.request.get('/api/v1/users/profile');
    expect(profileResponse.status()).toBe(200);

    const profile = await profileResponse.json();
    expect(profile.role.toLowerCase()).toBe('admin');

    console.log('✅ Session persisted across reload');
  });

  test('should validate session token in API requests', async ({ page, request }) => {
    // Login to get session
    await loginAsAdmin(page);

    // Get session cookie (flexible name matching)
    const cookies = await page.context().cookies();
    const sessionCookie = cookies.find(
      (c) =>
        c.name.toLowerCase().includes('session') ||
        c.name.toLowerCase().includes('auth') ||
        c.name.startsWith('.')
    );

    expect(sessionCookie).toBeDefined();

    // Make API request with session token
    const response = await request.get('/api/v1/users/profile', {
      headers: {
        Cookie: `${sessionCookie!.name}=${sessionCookie!.value}`,
      },
    });

    expect(response.status()).toBe(200);

    const profile = await response.json();
    expect(profile.role.toLowerCase()).toBe('admin');

    console.log('✅ Session token validated in API request');
  });

  test('should reject invalid credentials', async ({ page }) => {
    await page.goto('/login');

    // Try login with wrong password
    await page.fill('input[name="email"]', 'admin@meepleai.dev');
    await page.fill('input[name="password"]', 'WrongPassword123');

    const responsePromise = page.waitForResponse(
      (response) => response.url().includes('/api/v1/auth/login')
    );

    await page.click('button[type="submit"]');

    const response = await responsePromise;
    // Backend may return 400 (Bad Request) or 401 (Unauthorized) for invalid credentials
    expect([400, 401]).toContain(response.status());

    // Verify error message displayed
    await expect(
      page.locator('text=/Invalid credentials|Invalid email or password|Login failed/i')
    ).toBeVisible({ timeout: 5000 });

    // Verify still on login page
    expect(page.url()).toContain('/login');

    console.log('✅ Invalid credentials rejected correctly');
  });

  test('should handle logout correctly', async ({ page, context }) => {
    // Login first
    await loginAsAdmin(page);

    // Verify on dashboard
    expect(page.url()).toMatch(/\/(dashboard|admin)/);

    // Logout
    const logoutButton = page.locator('button:has-text("Logout"), button:has-text("Sign out")');

    if (await logoutButton.isVisible()) {
      await logoutButton.click();
    } else {
      // Logout might be in a dropdown menu
      await page.click('[aria-label="User menu"], [aria-label="Account menu"]');
      await page.click('text=Logout, text=Sign out');
    }

    // Verify redirected to login
    await page.waitForURL('/login', { timeout: 10000 });

    // Verify session cleared
    const cookies = await context.cookies();
    const sessionCookie = cookies.find(
      (c) =>
        c.name.toLowerCase().includes('session') ||
        c.name.toLowerCase().includes('auth') ||
        c.name.startsWith('.')
    );

    // Session cookie should be removed or expired
    expect(sessionCookie?.value || '').toBeFalsy();

    console.log('✅ Logout successful, session cleared');
  });

  // OAuth tests - marked as skip for CI (requires real OAuth credentials)
  test.skip('admin first login via Google OAuth', async ({ page, context }) => {
    // NOTE: This test requires real Google OAuth setup and credentials
    // Skip in CI, run manually with E2E_OAUTH_ENABLED=true

    await page.goto('/login');

    // Click Google OAuth button
    await page.click('button:has-text("Sign in with Google"), button:has-text("Google")');

    // Wait for OAuth redirect
    await page.waitForURL(/accounts\.google\.com/, { timeout: 10000 });

    // In a real test, you would:
    // 1. Fill Google email/password
    // 2. Handle consent screen
    // 3. Wait for callback redirect

    // For now, just verify the OAuth flow initiated
    expect(page.url()).toContain('google.com');

    console.log('✅ OAuth flow initiated (manual completion required)');
  });

  test.skip('admin first login via GitHub OAuth', async ({ page }) => {
    // NOTE: Similar to Google OAuth - requires real credentials

    await page.goto('/login');

    await page.click('button:has-text("Sign in with GitHub"), button:has-text("GitHub")');

    await page.waitForURL(/github\.com/, { timeout: 10000 });

    expect(page.url()).toContain('github.com');

    console.log('✅ GitHub OAuth flow initiated (manual completion required)');
  });

  test('should display admin role badge after login', async ({ page }) => {
    await loginAsAdmin(page);

    // Verify admin badge or indicator
    const adminIndicator = page.locator(
      'text=Admin, [data-role="admin"], .badge:has-text("Admin")'
    );

    await expect(adminIndicator.first()).toBeVisible({ timeout: 5000 });

    console.log('✅ Admin role indicator displayed');
  });

  test('should have access to admin-only routes', async ({ page }) => {
    await loginAsAdmin(page);

    // Try accessing admin-only page
    await page.goto('/admin/users');

    // Should NOT be redirected (403 or back to dashboard)
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('/admin');

    // Verify admin content visible
    await expect(
      page.locator('h1:has-text("Users"), h1:has-text("User Management")')
    ).toBeVisible({ timeout: 5000 });

    console.log('✅ Admin routes accessible');
  });
});
