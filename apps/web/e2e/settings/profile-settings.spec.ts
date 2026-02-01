/**
 * Profile/Settings E2E Tests
 * Issue #2883 - Profile Update, Password, 2FA, Preferences
 *
 * Tests the /settings page workflows:
 * - Profile tab: Update display name, change password
 * - Preferences tab: Language, theme, notifications, data retention
 * - Advanced tab: Delete account (disabled state)
 *
 * Note: 2FA tests are covered in auth-2fa-complete.spec.ts
 * Note: Avatar upload is not implemented in current UI
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ============================================================================
// Mock Setup Functions
// ============================================================================

interface MockUserProfile {
  id: string;
  email: string;
  displayName: string;
  role: string;
  createdAt: string;
  language?: string;
  theme?: string;
  emailNotifications?: boolean;
  dataRetentionDays?: number;
}

interface MockState {
  user: MockUserProfile;
  passwordChanged: boolean;
  preferencesUpdated: boolean;
}

/**
 * Setup comprehensive mocks for settings page testing
 */
async function setupSettingsMocks(
  page: Page,
  initialUser: Partial<MockUserProfile> = {}
): Promise<{ getState: () => MockState; updateUser: (updates: Partial<MockUserProfile>) => void }> {
  const state: MockState = {
    user: {
      id: 'test-user-id',
      email: initialUser.email || 'test@meepleai.dev',
      displayName: initialUser.displayName || 'Test User',
      role: initialUser.role || 'User',
      createdAt: initialUser.createdAt || new Date().toISOString(),
      language: initialUser.language || 'en',
      theme: initialUser.theme || 'system',
      emailNotifications: initialUser.emailNotifications ?? true,
      dataRetentionDays: initialUser.dataRetentionDays ?? 90,
    },
    passwordChanged: false,
    preferencesUpdated: false,
  };

  // Mock /auth/me endpoint
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: state.user,
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      }),
    });
  });

  // Mock profile update endpoint
  await page.route(`${API_BASE}/api/v1/auth/profile`, async (route) => {
    if (route.request().method() === 'PUT' || route.request().method() === 'PATCH') {
      const body = await route.request().postDataJSON();
      if (body.displayName) state.user.displayName = body.displayName;
      if (body.email) state.user.email = body.email;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Profile updated successfully' }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(state.user),
      });
    }
  });

  // Mock password change endpoint
  await page.route(`${API_BASE}/api/v1/auth/change-password`, async (route) => {
    if (route.request().method() === 'POST') {
      const body = await route.request().postDataJSON();
      // Validate current password (mock validation)
      if (body.currentPassword === 'WrongPassword123!') {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Current password is incorrect' }),
        });
        return;
      }
      // Validate password requirements
      if (body.newPassword.length < 8) {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Password must be at least 8 characters' }),
        });
        return;
      }
      state.passwordChanged = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Password changed successfully' }),
      });
    }
  });

  // Mock preferences update endpoint
  await page.route(`${API_BASE}/api/v1/auth/preferences`, async (route) => {
    if (route.request().method() === 'PUT' || route.request().method() === 'PATCH') {
      const body = await route.request().postDataJSON();
      if (body.language) state.user.language = body.language;
      if (body.theme) state.user.theme = body.theme;
      if (typeof body.emailNotifications === 'boolean')
        state.user.emailNotifications = body.emailNotifications;
      if (body.dataRetentionDays) state.user.dataRetentionDays = body.dataRetentionDays;
      state.preferencesUpdated = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Preferences saved successfully' }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          language: state.user.language,
          theme: state.user.theme,
          emailNotifications: state.user.emailNotifications,
          dataRetentionDays: state.user.dataRetentionDays,
        }),
      });
    }
  });

  // Mock 2FA status endpoint
  await page.route(`${API_BASE}/api/v1/users/me/2fa/status`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        isTwoFactorEnabled: false,
        backupCodesCount: 0,
      }),
    });
  });

  // Mock OAuth accounts endpoint
  await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    });
  });

  // Mock sessions endpoint
  await page.route(`${API_BASE}/api/v1/auth/sessions`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          id: 'session-1',
          userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)',
          ipAddress: '127.0.0.1',
          createdAt: new Date().toISOString(),
          lastSeenAt: new Date().toISOString(),
          expiresAt: new Date(Date.now() + 86400000).toISOString(),
        },
      ]),
    });
  });

  // Mock games endpoint (commonly needed)
  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    });
  });

  return {
    getState: () => state,
    updateUser: (updates) => {
      Object.assign(state.user, updates);
    },
  };
}

// ============================================================================
// Test Suite: Profile Tab - Update Display Name
// ============================================================================

test.describe('Profile Settings - Update Profile', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page, { displayName: 'Original Name' });
  });

  test('should display current profile information', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Verify settings page loads
    await expect(page.getByTestId('settings-heading').or(page.getByText(/settings/i).first())).toBeVisible();

    // Verify profile tab is active by default
    const profileTab = page.getByRole('tab', { name: /profile/i });
    await expect(profileTab).toBeVisible();

    // Verify display name input exists
    const displayNameInput = page.getByLabel(/display name/i);
    await expect(displayNameInput).toBeVisible();
  });

  test('should update display name successfully', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Clear and fill new display name
    const displayNameInput = page.getByLabel(/display name/i);
    await displayNameInput.clear();
    await displayNameInput.fill('Updated Name');

    // Click update profile button
    const updateButton = page.getByTestId('save-profile-button').or(
      page.getByRole('button', { name: /update profile/i })
    );
    await updateButton.click();

    // Verify success message
    await expect(page.getByText(/updated|saved|success/i)).toBeVisible({ timeout: 5000 });
  });

  test('should show error for empty display name', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Clear display name (make it empty)
    const displayNameInput = page.getByLabel(/display name/i);
    await displayNameInput.clear();

    // Click update profile button
    const updateButton = page.getByTestId('save-profile-button').or(
      page.getByRole('button', { name: /update profile/i })
    );
    await updateButton.click();

    // Verify error message
    await expect(page.getByText(/cannot be empty|required/i)).toBeVisible({ timeout: 5000 });
  });

  test('should show email is disabled for changes', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Verify email input is disabled
    const emailInput = page.getByLabel(/email/i).first();
    await expect(emailInput).toBeDisabled();

    // Verify explanation text
    await expect(page.getByText(/email changes not yet supported/i)).toBeVisible();
  });
});

// ============================================================================
// Test Suite: Profile Tab - Change Password
// ============================================================================

test.describe('Profile Settings - Change Password', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page);
  });

  test('should display password change form', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Verify password change section exists
    await expect(page.getByText(/change password/i)).toBeVisible();

    // Verify all password fields exist
    await expect(page.getByLabel(/current password/i)).toBeVisible();
    await expect(page.getByLabel(/new password/i)).toBeVisible();
    await expect(page.getByLabel(/confirm.*password/i)).toBeVisible();
  });

  test('should change password successfully', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Fill password change form
    await page.getByLabel(/current password/i).fill('CurrentPass123!');
    await page.getByLabel(/new password/i).fill('NewSecurePass456!');
    await page.getByLabel(/confirm.*password/i).fill('NewSecurePass456!');

    // Click change password button
    const changeButton = page.getByTestId('change-password-button').or(
      page.getByRole('button', { name: /change password/i })
    );
    await changeButton.click();

    // Verify success message
    await expect(page.getByText(/password changed|success/i)).toBeVisible({ timeout: 5000 });
  });

  test('should show error when passwords do not match', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Fill with mismatched passwords
    await page.getByLabel(/current password/i).fill('CurrentPass123!');
    await page.getByLabel(/new password/i).fill('NewSecurePass456!');
    await page.getByLabel(/confirm.*password/i).fill('DifferentPass789!');

    // Click change password button
    const changeButton = page.getByTestId('change-password-button').or(
      page.getByRole('button', { name: /change password/i })
    );
    await changeButton.click();

    // Verify error message
    await expect(page.getByText(/do not match|passwords.*match/i)).toBeVisible({ timeout: 5000 });
  });

  test('should show error for short password', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Fill with short password
    await page.getByLabel(/current password/i).fill('CurrentPass123!');
    await page.getByLabel(/new password/i).fill('short');
    await page.getByLabel(/confirm.*password/i).fill('short');

    // Click change password button
    const changeButton = page.getByTestId('change-password-button').or(
      page.getByRole('button', { name: /change password/i })
    );
    await changeButton.click();

    // Verify error message
    await expect(page.getByText(/at least 8 characters|too short/i)).toBeVisible({ timeout: 5000 });
  });

  test('should show error when all fields are empty', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Click change password without filling fields
    const changeButton = page.getByTestId('change-password-button').or(
      page.getByRole('button', { name: /change password/i })
    );
    await changeButton.click();

    // Verify error message
    await expect(page.getByText(/required|all.*fields/i)).toBeVisible({ timeout: 5000 });
  });

  test('should clear password fields after successful change', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Fill password change form
    await page.getByLabel(/current password/i).fill('CurrentPass123!');
    await page.getByLabel(/new password/i).fill('NewSecurePass456!');
    await page.getByLabel(/confirm.*password/i).fill('NewSecurePass456!');

    // Click change password button
    const changeButton = page.getByTestId('change-password-button').or(
      page.getByRole('button', { name: /change password/i })
    );
    await changeButton.click();

    // Wait for success
    await expect(page.getByText(/password changed|success/i)).toBeVisible({ timeout: 5000 });

    // Verify fields are cleared
    await expect(page.getByLabel(/current password/i)).toHaveValue('');
    await expect(page.getByLabel(/new password/i)).toHaveValue('');
    await expect(page.getByLabel(/confirm.*password/i)).toHaveValue('');
  });
});

// ============================================================================
// Test Suite: Preferences Tab - Language, Theme, Notifications
// ============================================================================

test.describe('Profile Settings - Preferences', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page, {
      language: 'en',
      theme: 'system',
      emailNotifications: true,
      dataRetentionDays: 90,
    });
  });

  test('should display preferences tab', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Click preferences tab
    const preferencesTab = page.getByRole('tab', { name: /preferences/i });
    await preferencesTab.click();

    // Verify preferences content is visible
    await expect(page.getByText(/user preferences/i)).toBeVisible();
    await expect(page.getByLabel(/language/i)).toBeVisible();
    await expect(page.getByLabel(/theme/i)).toBeVisible();
  });

  test('should change language preference', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Click preferences tab
    await page.getByRole('tab', { name: /preferences/i }).click();

    // Change language
    const languageSelect = page.getByLabel(/language/i);
    await languageSelect.click();
    await page.getByRole('option', { name: /italiano/i }).click();

    // Save preferences
    const saveButton = page.getByTestId('save-preferences-button').or(
      page.getByRole('button', { name: /save preferences/i })
    );
    await saveButton.click();

    // Verify success message
    await expect(page.getByText(/preferences saved|success/i)).toBeVisible({ timeout: 5000 });
  });

  test('should change theme preference', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Click preferences tab
    await page.getByRole('tab', { name: /preferences/i }).click();

    // Change theme
    const themeSelect = page.getByLabel(/theme/i);
    await themeSelect.click();
    await page.getByRole('option', { name: /dark/i }).click();

    // Save preferences
    const saveButton = page.getByTestId('save-preferences-button').or(
      page.getByRole('button', { name: /save preferences/i })
    );
    await saveButton.click();

    // Verify success message
    await expect(page.getByText(/preferences saved|success/i)).toBeVisible({ timeout: 5000 });
  });

  test('should toggle email notifications', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Click preferences tab
    await page.getByRole('tab', { name: /preferences/i }).click();

    // Toggle email notifications
    const emailToggle = page.getByRole('switch', { name: /email notifications/i }).or(
      page.locator('#emailNotifications')
    );
    await emailToggle.click();

    // Save preferences
    const saveButton = page.getByTestId('save-preferences-button').or(
      page.getByRole('button', { name: /save preferences/i })
    );
    await saveButton.click();

    // Verify success message
    await expect(page.getByText(/preferences saved|success/i)).toBeVisible({ timeout: 5000 });
  });

  test('should change data retention period', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Click preferences tab
    await page.getByRole('tab', { name: /preferences/i }).click();

    // Change data retention
    const retentionSelect = page.getByLabel(/data retention/i);
    await retentionSelect.click();
    await page.getByRole('option', { name: /180 days/i }).click();

    // Save preferences
    const saveButton = page.getByTestId('save-preferences-button').or(
      page.getByRole('button', { name: /save preferences/i })
    );
    await saveButton.click();

    // Verify success message
    await expect(page.getByText(/preferences saved|success/i)).toBeVisible({ timeout: 5000 });
  });

  test('should persist preferences after page reload', async ({ page }) => {
    const mocks = await setupSettingsMocks(page, {
      language: 'en',
      theme: 'system',
    });

    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Click preferences tab
    await page.getByRole('tab', { name: /preferences/i }).click();

    // Change theme to dark
    const themeSelect = page.getByLabel(/theme/i);
    await themeSelect.click();
    await page.getByRole('option', { name: /dark/i }).click();

    // Update mock to reflect the change
    mocks.updateUser({ theme: 'dark' });

    // Save preferences
    const saveButton = page.getByTestId('save-preferences-button').or(
      page.getByRole('button', { name: /save preferences/i })
    );
    await saveButton.click();
    await expect(page.getByText(/preferences saved|success/i)).toBeVisible({ timeout: 5000 });

    // Reload page
    await page.reload();
    await page.waitForLoadState('networkidle');

    // Navigate back to preferences tab
    await page.getByRole('tab', { name: /preferences/i }).click();

    // Verify theme is still set to dark (mocked value)
    await expect(page.getByLabel(/theme/i)).toContainText(/dark/i);
  });
});

// ============================================================================
// Test Suite: Advanced Tab - Delete Account
// ============================================================================

test.describe('Profile Settings - Delete Account', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page);
  });

  test('should display danger zone with delete account option', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Click advanced tab
    const advancedTab = page.getByRole('tab', { name: /advanced/i });
    await advancedTab.click();

    // Verify danger zone section exists
    await expect(page.getByText(/danger zone/i)).toBeVisible();

    // Verify delete account button exists (should be disabled with "Coming Soon")
    const deleteButton = page.getByRole('button', { name: /delete account/i });
    await expect(deleteButton).toBeVisible();
    await expect(deleteButton).toBeDisabled();
  });

  test('should show warning about irreversible action', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Click advanced tab
    await page.getByRole('tab', { name: /advanced/i }).click();

    // Verify warning text
    await expect(page.getByText(/permanent|cannot be undone|irreversible/i)).toBeVisible();
  });
});

// ============================================================================
// Test Suite: Tab Navigation
// ============================================================================

test.describe('Profile Settings - Tab Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page);
  });

  test('should navigate between all tabs', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Verify Profile tab is active by default
    await expect(page.getByText(/profile information/i)).toBeVisible();

    // Navigate to Preferences tab
    await page.getByRole('tab', { name: /preferences/i }).click();
    await expect(page.getByText(/user preferences/i)).toBeVisible();

    // Navigate to Privacy tab
    await page.getByRole('tab', { name: /privacy/i }).click();
    await expect(page.getByText(/two-factor authentication/i)).toBeVisible();

    // Navigate to Advanced tab
    await page.getByRole('tab', { name: /advanced/i }).click();
    await expect(page.getByText(/api key authentication|danger zone/i).first()).toBeVisible();

    // Navigate back to Profile tab
    await page.getByRole('tab', { name: /profile/i }).click();
    await expect(page.getByText(/profile information/i)).toBeVisible();
  });

  test('should show all four tabs', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Verify all tabs are visible
    await expect(page.getByRole('tab', { name: /profile/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /preferences/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /privacy/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /advanced/i })).toBeVisible();
  });
});

// ============================================================================
// Test Suite: Active Sessions (Advanced Tab)
// ============================================================================

test.describe('Profile Settings - Active Sessions', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page);
  });

  test('should display active sessions', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Click advanced tab
    await page.getByRole('tab', { name: /advanced/i }).click();

    // Verify active sessions section
    await expect(page.getByText(/active sessions/i)).toBeVisible();
  });

  test('should show current session indicator', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Click advanced tab
    await page.getByRole('tab', { name: /advanced/i }).click();

    // Verify current session is indicated
    await expect(page.getByText(/current session/i)).toBeVisible();
  });

  test('should show logout all devices option', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Click advanced tab
    await page.getByRole('tab', { name: /advanced/i }).click();

    // Verify logout all devices button exists
    const logoutAllButton = page.getByRole('button', { name: /logout all devices/i });
    await expect(logoutAllButton).toBeVisible();
  });
});

// ============================================================================
// Test Suite: Error Handling
// ============================================================================

test.describe('Profile Settings - Error Handling', () => {
  test('should redirect to login if not authenticated', async ({ page }) => {
    // Mock unauthenticated response
    await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Unauthorized' }),
      });
    });

    await page.goto('/settings');

    // Should redirect to login
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/\/login/);
  });

  test('should show error alert on API failure', async ({ page }) => {
    await setupSettingsMocks(page);

    // Override profile update to fail
    await page.route(`${API_BASE}/api/v1/auth/profile`, async (route) => {
      if (route.request().method() === 'PUT' || route.request().method() === 'PATCH') {
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Internal server error' }),
        });
      }
    });

    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Try to update profile
    const displayNameInput = page.getByLabel(/display name/i);
    await displayNameInput.clear();
    await displayNameInput.fill('New Name');

    const updateButton = page.getByTestId('save-profile-button').or(
      page.getByRole('button', { name: /update profile/i })
    );
    await updateButton.click();

    // Should show error message
    await expect(page.getByRole('alert').or(page.getByText(/error|failed/i))).toBeVisible({
      timeout: 5000,
    });
  });
});
