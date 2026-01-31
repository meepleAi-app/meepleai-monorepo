/**
 * Logout All Devices E2E Tests (Issue #2056)
 *
 * Tests for the "Logout from All Devices" feature in Settings > Advanced
 * Covers: UI interaction, API calls, and redirect behavior
 */

import { test, expect, Page } from '@playwright/test';

import { setupMockAuth } from './fixtures/auth';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Setup mock routes for settings page with sessions
 */
async function setupSettingsRoutes(page: Page, sessionsCount: number = 3) {
  // Setup base auth
  await setupMockAuth(page, 'User', 'user@meepleai.dev');

  // Mock user profile for settings page
  await page.route(`${API_BASE}/api/v1/users/profile`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        Id: 'user-test-id',
        Email: 'user@meepleai.dev',
        DisplayName: 'Test User',
        Role: 'User',
        CreatedAt: new Date().toISOString(),
        IsTwoFactorEnabled: false,
        TwoFactorEnabledAt: null,
        Language: 'en',
        Theme: 'system',
        EmailNotifications: true,
        DataRetentionDays: 90,
      }),
    });
  });

  // Mock 2FA status
  await page.route(`${API_BASE}/api/v1/users/me/2fa/status`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        IsEnabled: false,
        EnabledAt: null,
        UnusedBackupCodesCount: 0,
      }),
    });
  });

  // Mock OAuth accounts
  await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    });
  });

  // Mock session status
  await page.route(`${API_BASE}/api/v1/auth/session/status`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        ExpiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        LastSeenAt: new Date().toISOString(),
        RemainingMinutes: 60,
      }),
    });
  });

  // Generate mock sessions
  const sessions = Array.from({ length: sessionsCount }, (_, i) => ({
    Id: `session-${i + 1}`,
    UserId: 'user-test-id',
    UserEmail: 'user@meepleai.dev',
    CreatedAt: new Date(Date.now() - (i + 1) * 24 * 60 * 60 * 1000).toISOString(),
    ExpiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
    LastSeenAt:
      i === 0 ? new Date().toISOString() : new Date(Date.now() - i * 60 * 60 * 1000).toISOString(),
    RevokedAt: null,
    IpAddress: `192.168.1.${100 + i}`,
    UserAgent:
      i === 0
        ? 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0'
        : `Mozilla/5.0 Device ${i + 1}`,
  }));

  // Mock user sessions endpoint
  await page.route(`${API_BASE}/api/v1/users/me/sessions`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(sessions),
    });
  });

  return { sessions };
}

test.describe('Logout All Devices Feature (Issue #2056)', () => {
  test('should show "Logout All Devices" button when multiple sessions exist', async ({ page }) => {
    await setupSettingsRoutes(page, 3);

    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Navigate to Advanced tab
    await page.getByRole('tab', { name: 'Advanced' }).click();

    // Wait for sessions to load
    await expect(page.getByText('Active Sessions')).toBeVisible();

    // Should show the Logout All Devices button
    await expect(page.getByRole('button', { name: 'Logout All Devices' })).toBeVisible();
  });

  test('should NOT show "Logout All Devices" button with only one session', async ({ page }) => {
    await setupSettingsRoutes(page, 1);

    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Navigate to Advanced tab
    await page.getByRole('tab', { name: 'Advanced' }).click();

    // Wait for sessions to load
    await expect(page.getByText('Active Sessions')).toBeVisible();

    // Should NOT show the Logout All Devices button
    await expect(page.getByRole('button', { name: 'Logout All Devices' })).not.toBeVisible();
  });

  test('should open confirmation dialog when clicking "Logout All Devices"', async ({ page }) => {
    await setupSettingsRoutes(page, 3);

    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Navigate to Advanced tab
    await page.getByRole('tab', { name: 'Advanced' }).click();

    // Click the button
    await page.getByRole('button', { name: 'Logout All Devices' }).click();

    // Dialog should be visible
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByText('Logout from All Devices')).toBeVisible();
    await expect(page.getByText('Include current session')).toBeVisible();
    await expect(page.getByLabel('Password (optional)')).toBeVisible();
  });

  test('should close dialog when clicking Cancel', async ({ page }) => {
    await setupSettingsRoutes(page, 3);

    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Navigate to Advanced tab
    await page.getByRole('tab', { name: 'Advanced' }).click();

    // Open dialog
    await page.getByRole('button', { name: 'Logout All Devices' }).click();
    await expect(page.getByRole('dialog')).toBeVisible();

    // Click Cancel
    await page.getByRole('button', { name: 'Cancel' }).click();

    // Dialog should close
    await expect(page.getByRole('dialog')).not.toBeVisible();
  });

  test('should call API and show success message when revoking other sessions', async ({
    page,
  }) => {
    await setupSettingsRoutes(page, 3);

    // Mock the revoke-all endpoint
    let apiCalled = false;
    let requestBody: unknown;
    await page.route(`${API_BASE}/api/v1/auth/sessions/revoke-all`, async route => {
      apiCalled = true;
      requestBody = route.request().postDataJSON();
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ok: true,
          revokedCount: 2,
          currentSessionRevoked: false,
          message: 'Successfully revoked 2 sessions',
        }),
      });
    });

    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Navigate to Advanced tab
    await page.getByRole('tab', { name: 'Advanced' }).click();

    // Open dialog and submit (without checking "include current session")
    await page.getByRole('button', { name: 'Logout All Devices' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Logout All Devices' }).click();

    // Wait for success message
    await expect(page.getByText(/Successfully logged out of 2 devices/)).toBeVisible({
      timeout: 10000,
    });

    // Verify API was called with correct parameters
    expect(apiCalled).toBe(true);
    expect(requestBody).toMatchObject({
      includeCurrentSession: false,
    });
  });

  test('should call API with password when provided', async ({ page }) => {
    await setupSettingsRoutes(page, 3);

    // Mock the revoke-all endpoint
    let requestBody: unknown;
    await page.route(`${API_BASE}/api/v1/auth/sessions/revoke-all`, async route => {
      requestBody = route.request().postDataJSON();
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ok: true,
          revokedCount: 2,
          currentSessionRevoked: false,
          message: 'Successfully revoked 2 sessions',
        }),
      });
    });

    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Navigate to Advanced tab
    await page.getByRole('tab', { name: 'Advanced' }).click();

    // Open dialog, enter password, and submit
    await page.getByRole('button', { name: 'Logout All Devices' }).click();
    await page.getByLabel('Password (optional)').fill('myPassword123');
    await page.getByRole('dialog').getByRole('button', { name: 'Logout All Devices' }).click();

    // Wait for success message
    await expect(page.getByText(/Successfully logged out/)).toBeVisible({ timeout: 10000 });

    // Verify password was sent
    expect(requestBody).toMatchObject({
      includeCurrentSession: false,
      password: 'myPassword123',
    });
  });

  test('should include current session when checkbox is checked', async ({ page }) => {
    await setupSettingsRoutes(page, 3);

    // Mock the revoke-all endpoint
    let requestBody: unknown;
    await page.route(`${API_BASE}/api/v1/auth/sessions/revoke-all`, async route => {
      requestBody = route.request().postDataJSON();
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ok: true,
          revokedCount: 3,
          currentSessionRevoked: true,
          message: 'Successfully revoked 3 sessions',
        }),
      });
    });

    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Navigate to Advanced tab
    await page.getByRole('tab', { name: 'Advanced' }).click();

    // Open dialog, check the checkbox, and submit
    await page.getByRole('button', { name: 'Logout All Devices' }).click();
    await page.getByRole('checkbox', { name: /Include current session/i }).check();
    await page.getByRole('dialog').getByRole('button', { name: 'Logout All Devices' }).click();

    // Wait for success message
    await expect(page.getByText(/Successfully logged out of 3 devices/)).toBeVisible({
      timeout: 10000,
    });

    // Verify includeCurrentSession was true
    expect(requestBody).toMatchObject({
      includeCurrentSession: true,
    });
  });

  test('should redirect to login when current session is revoked', async ({ page }) => {
    await setupSettingsRoutes(page, 3);

    // Mock the revoke-all endpoint to return currentSessionRevoked: true
    await page.route(`${API_BASE}/api/v1/auth/sessions/revoke-all`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ok: true,
          revokedCount: 3,
          currentSessionRevoked: true,
          message: 'Successfully revoked 3 sessions',
        }),
      });
    });

    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Navigate to Advanced tab
    await page.getByRole('tab', { name: 'Advanced' }).click();

    // Open dialog, include current session, and submit
    await page.getByRole('button', { name: 'Logout All Devices' }).click();
    await page.getByRole('checkbox', { name: /Include current session/i }).check();
    await page.getByRole('dialog').getByRole('button', { name: 'Logout All Devices' }).click();

    // Should redirect to login page after success
    await page.waitForURL('/login', { timeout: 10000 });
    await expect(page).toHaveURL('/login');
  });

  test('should show error message on API failure', async ({ page }) => {
    await setupSettingsRoutes(page, 3);

    // Mock the revoke-all endpoint to return an error
    await page.route(`${API_BASE}/api/v1/auth/sessions/revoke-all`, async route => {
      await route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({
          ok: false,
          revokedCount: 0,
          currentSessionRevoked: false,
          message: 'Invalid password',
        }),
      });
    });

    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Navigate to Advanced tab
    await page.getByRole('tab', { name: 'Advanced' }).click();

    // Open dialog and submit
    await page.getByRole('button', { name: 'Logout All Devices' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Logout All Devices' }).click();

    // Should show error message
    await expect(page.getByText(/Failed to logout from all devices/i)).toBeVisible({
      timeout: 10000,
    });
  });

  test('should show loading state during API call', async ({ page }) => {
    await setupSettingsRoutes(page, 3);

    // Mock the revoke-all endpoint with a delay
    await page.route(`${API_BASE}/api/v1/auth/sessions/revoke-all`, async route => {
      // Add a small delay to see loading state
      await new Promise(resolve => setTimeout(resolve, 500));
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ok: true,
          revokedCount: 2,
          currentSessionRevoked: false,
          message: 'Successfully revoked 2 sessions',
        }),
      });
    });

    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Navigate to Advanced tab
    await page.getByRole('tab', { name: 'Advanced' }).click();

    // Open dialog and submit
    await page.getByRole('button', { name: 'Logout All Devices' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Logout All Devices' }).click();

    // Should show loading state
    await expect(page.getByRole('button', { name: 'Logging out...' })).toBeVisible();

    // Wait for completion
    await expect(page.getByText(/Successfully logged out/)).toBeVisible({ timeout: 10000 });
  });
});
