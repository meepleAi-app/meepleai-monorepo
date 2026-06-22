/**
 * Profile/Settings E2E Tests — rewritten for /profile?tab=settings consolidation (Issue #1608)
 *
 * The standalone /settings/* pages were DELETED and consolidated into the 4th tab of /profile.
 * Server-side redirects in next.config.js map /settings/* → /profile?tab=settings&section=<id>.
 *
 * New DOM structure:
 *   - 4 role="tab" buttons: Overview | Achievements | Activity | Settings
 *   - Sub-sections are SettingsRow sidebar items (NOT role="tab"); switching updates ?section=
 *   - Navigate sections via page.goto('/profile?tab=settings&section=<id>') or sidebar click
 *
 * ─── DELETED COVERAGE (I3 — explicit documentation of removed tests) ────────────────────
 *
 * 1. "Change Password" (was lines ~294-410, 6 tests)
 *    DELETED — There is NO password-change UI in the new /profile settings design.
 *    Password change was never part of issue #1608 scope. The API endpoint
 *    /api/v1/auth/change-password may still exist server-side, but no FE section
 *    exposes it in the consolidated settings tab. If a password-change flow is added
 *    later, create a dedicated spec file (e.g. e2e/settings/change-password.spec.ts).
 *
 * 2. "Delete Account / danger zone" (was lines ~568-604, 2 tests)
 *    DELETED — There is NO delete-account UI in the new settings design. The danger-zone
 *    section was removed from scope entirely. If implemented later, it belongs in its
 *    own spec or a dedicated describe block in this file.
 *
 * 3. "should change data retention period" (was lines ~503-523, 1 test)
 *    DELETED — PreferencesSection in the new design does NOT include a data-retention
 *    period field. The DTO for preferences covers only theme/language/emailNotifications.
 *    Re-add if data-retention is introduced to PreferencesSection in a future PR.
 *
 * 4. "should show email is disabled for changes" (was line ~277, 1 test)
 *    DELETED — The new ProfileSection ALLOWS email editing (the field is an editable
 *    <Input type="email"> without a disabled attribute). The old "email is disabled" assertion
 *    is no longer valid. Email editing is in scope per Issue #1608.
 *
 * ─── TAB NAVIGATION NOTE ────────────────────────────────────────────────────────────────
 * The old spec tested 4 OLD tabs (Profile/Preferences/Privacy/Advanced). The NEW /profile
 * page has 4 DIFFERENT tabs: Overview / Achievements / Activity / Settings. Tests below
 * verify the new tab set.
 *
 * Tests: 17 (rewritten from original 44 — 10 tests deleted per above rationale; remaining
 *             were rewritten or merged to reflect new URL/DOM; 3 new tests added for
 *             AI-consent and API-keys sections new to this design).
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ============================================================================
// Mock Setup
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
  avatarUrl?: string | null;
}

/**
 * Setup mocks for the consolidated /profile settings-tab page.
 * Mirrors the mock structure from the shared auth fixture but extended with
 * settings-specific endpoints.
 */
async function setupSettingsMocks(
  page: Page,
  initialUser: Partial<MockUserProfile> = {}
): Promise<{
  getDisplayName: () => string;
  updateUser: (updates: Partial<MockUserProfile>) => void;
}> {
  const user: MockUserProfile = {
    id: 'test-user-id',
    email: initialUser.email ?? 'test@meepleai.dev',
    displayName: initialUser.displayName ?? 'Test User',
    role: initialUser.role ?? 'User',
    createdAt: initialUser.createdAt ?? new Date().toISOString(),
    language: initialUser.language ?? 'it',
    theme: initialUser.theme ?? 'system',
    emailNotifications: initialUser.emailNotifications ?? true,
    avatarUrl: initialUser.avatarUrl ?? null,
  };

  // Catch-all — prevents unmocked calls from reaching real backends in CI
  await page.route(`${API_BASE}/api/**`, async route => {
    const method = route.request().method();
    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    } else if (['POST', 'PUT', 'PATCH'].includes(method)) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    } else if (method === 'DELETE') {
      await route.fulfill({ status: 204 });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({}),
      });
    }
  });

  // Auth identity
  await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user,
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    });
  });

  // Profile GET + PUT/PATCH
  await page.route(`${API_BASE}/api/v1/auth/profile`, async route => {
    const method = route.request().method();
    if (method === 'PUT' || method === 'PATCH') {
      const body = await route.request().postDataJSON();
      if (body.displayName !== undefined) user.displayName = body.displayName;
      if (body.email !== undefined) user.email = body.email;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ ok: true, message: 'Profile updated' }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(user),
      });
    }
  });

  // Preferences GET + PUT/PATCH
  await page.route(`${API_BASE}/api/v1/auth/preferences`, async route => {
    const method = route.request().method();
    if (method === 'PUT' || method === 'PATCH') {
      const body = await route.request().postDataJSON();
      if (body.theme !== undefined) user.theme = body.theme;
      if (body.language !== undefined) user.language = body.language;
      if (typeof body.emailNotifications === 'boolean')
        user.emailNotifications = body.emailNotifications;
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
          theme: user.theme,
          language: user.language,
          emailNotifications: user.emailNotifications,
        }),
      });
    }
  });

  // 2FA status
  await page.route(`${API_BASE}/api/v1/users/me/2fa/status`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ isTwoFactorEnabled: false, backupCodesCount: 0 }),
    });
  });

  // 2FA setup / confirm / disable (keep for SecuritySection flows)
  await page.route(`${API_BASE}/api/v1/users/me/2fa/setup`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        secret: 'JBSWY3DPEHPK3PXP',
        qrCodeUrl: 'otpauth://totp/test',
        backupCodes: ['A1B2C3', 'D4E5F6'],
      }),
    });
  });

  // Active sessions
  await page.route(`${API_BASE}/api/v1/auth/sessions`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          id: 'session-1',
          userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120',
          ipAddress: '127.0.0.1',
          createdAt: new Date().toISOString(),
          lastSeenAt: new Date().toISOString(),
          expiresAt: new Date(Date.now() + 86_400_000).toISOString(),
          revokedAt: null,
        },
        {
          id: 'session-2',
          userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0) Safari/604',
          ipAddress: '192.168.1.50',
          createdAt: new Date(Date.now() - 3_600_000).toISOString(),
          lastSeenAt: new Date(Date.now() - 600_000).toISOString(),
          expiresAt: new Date(Date.now() + 86_400_000).toISOString(),
          revokedAt: null,
        },
      ]),
    });
  });

  // Revoke single session + revoke-all
  await page.route(`${API_BASE}/api/v1/auth/sessions/**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true }),
    });
  });

  // API keys list
  await page.route(`${API_BASE}/api/v1/auth/api-keys`, async route => {
    const method = route.request().method();
    if (method === 'POST') {
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'key-1',
          keyName: 'Test Key',
          keyPrefix: 'mk_test',
          plaintextKey: 'mk_test_abc123secret',
        }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [] }),
      });
    }
  });

  // AI consent
  await page.route(`${API_BASE}/api/v1/users/me/ai-consent`, async route => {
    const method = route.request().method();
    if (method === 'PUT') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          userId: user.id,
          consentedToAiProcessing: true,
          consentedToExternalProviders: false,
          consentedAt: new Date().toISOString(),
          consentVersion: '1.0.0',
        }),
      });
    }
  });

  // Library stats (needed by OverviewTab)
  await page.route(`${API_BASE}/api/v1/users/me/library/stats`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ totalGames: 0 }),
    });
  });

  return {
    getDisplayName: () => user.displayName,
    updateUser: updates => Object.assign(user, updates),
  };
}

// ============================================================================
// Tab Navigation — new 4-tab structure
// ============================================================================

test.describe('Profile Page - Tab Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page);
  });

  test('should show all four profile tabs', async ({ page }) => {
    await page.goto('/profile');
    await page.waitForLoadState('networkidle');

    // Verify all 4 tabs (new structure: Overview / Achievements / Activity / Settings)
    await expect(page.getByRole('tab', { name: /overview/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /achievements/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /activity/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /settings/i })).toBeVisible();
  });

  test('should navigate between profile tabs', async ({ page }) => {
    await page.goto('/profile');
    await page.waitForLoadState('networkidle');

    // Default is Overview
    await expect(page.getByRole('tab', { name: /overview/i })).toHaveAttribute(
      'aria-selected',
      'true'
    );

    // Navigate to Settings tab → URL updates
    await page.getByRole('tab', { name: /settings/i }).click();
    await expect(page).toHaveURL(/[?&]tab=settings/);

    // Navigate to Achievements tab
    await page.getByRole('tab', { name: /achievements/i }).click();
    await expect(page).toHaveURL(/[?&]tab=achievements/);

    // Navigate back to Overview
    await page.getByRole('tab', { name: /overview/i }).click();
    await expect(page).toHaveURL(/\/profile/);
  });

  test('should reach Settings tab via /settings redirect', async ({ page }) => {
    // /settings should redirect to /profile?tab=settings (permanent redirect)
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/\/profile(\?.*)?/);
  });
});

// ============================================================================
// Settings sections — Profile
// ============================================================================

test.describe('Settings - Update Profile', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page, { displayName: 'Original Name' });
  });

  test('should display profile form at /profile?tab=settings&section=profile', async ({ page }) => {
    await page.goto('/profile?tab=settings&section=profile');
    await page.waitForLoadState('networkidle');

    // Settings tab is active
    await expect(page.getByRole('tab', { name: /settings/i })).toHaveAttribute(
      'aria-selected',
      'true'
    );

    // Profile section form elements visible
    await expect(page.getByLabel(/display name/i)).toBeVisible();
    await expect(page.getByTestId('save-profile-button')).toBeVisible();
  });

  test('should follow redirect from /settings/profile to new section URL', async ({ page }) => {
    await page.goto('/settings/profile');
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/[?&]section=profile/);
  });

  test('should update display name successfully', async ({ page }) => {
    await page.goto('/profile?tab=settings&section=profile');
    await page.waitForLoadState('networkidle');

    const displayNameInput = page.getByLabel(/display name/i);
    await displayNameInput.clear();
    await displayNameInput.fill('Updated Name');

    await page.getByTestId('save-profile-button').click();

    // Feedback alert — success
    await expect(page.getByRole('alert')).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('alert')).toContainText(/profile updated|updated|saved/i);
  });

  test('should show email input as editable (not disabled)', async ({ page }) => {
    // New ProfileSection allows email editing — opposite of old disabled assertion
    await page.goto('/profile?tab=settings&section=profile');
    await page.waitForLoadState('networkidle');

    // email field exists and is editable
    const emailInput = page.getByLabel(/email/i).first();
    await expect(emailInput).toBeVisible();
    await expect(emailInput).not.toBeDisabled();
  });
});

// ============================================================================
// Settings sections — Preferences
// ============================================================================

test.describe('Settings - Preferences', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page, { language: 'it', theme: 'system', emailNotifications: true });
  });

  test('should display preferences section', async ({ page }) => {
    await page.goto('/profile?tab=settings&section=preferences');
    await page.waitForLoadState('networkidle');

    // Section header
    await expect(page.getByText(/preferences/i)).toBeVisible();
    await expect(page.getByLabel(/theme/i)).toBeVisible();
    await expect(page.getByLabel(/language/i)).toBeVisible();
    await expect(page.getByTestId('save-preferences-button')).toBeVisible();
  });

  test('should follow redirect from /settings/preferences', async ({ page }) => {
    await page.goto('/settings/preferences');
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/[?&]section=preferences/);
  });

  test('should change theme preference and save', async ({ page }) => {
    await page.goto('/profile?tab=settings&section=preferences');
    await page.waitForLoadState('networkidle');

    const themeSelect = page.getByLabel(/theme/i);
    await themeSelect.selectOption('dark');

    await page.getByTestId('save-preferences-button').click();

    await expect(page.getByRole('alert')).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('alert')).toContainText(/preferences updated|saved|success/i);
  });

  test('should change language preference and save', async ({ page }) => {
    await page.goto('/profile?tab=settings&section=preferences');
    await page.waitForLoadState('networkidle');

    const languageSelect = page.getByLabel(/language/i);
    await languageSelect.selectOption('en');

    await page.getByTestId('save-preferences-button').click();

    await expect(page.getByRole('alert')).toBeVisible({ timeout: 5000 });
  });

  test('should toggle email notifications and save', async ({ page }) => {
    await page.goto('/profile?tab=settings&section=preferences');
    await page.waitForLoadState('networkidle');

    // Email-notifications is a checkbox in PreferencesSection
    const emailCheckbox = page.locator('input[type="checkbox"]').first();
    await emailCheckbox.click();

    await page.getByTestId('save-preferences-button').click();

    await expect(page.getByRole('alert')).toBeVisible({ timeout: 5000 });
  });
});

// ============================================================================
// Settings sections — Security (2FA + Active Sessions)
// ============================================================================

test.describe('Settings - Security', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page);
  });

  test('should display 2FA status card and enable button', async ({ page }) => {
    await page.goto('/profile?tab=settings&section=security');
    await page.waitForLoadState('networkidle');

    // TwoFactorStatusCard renders data-testid="2fa-status"
    await expect(page.getByTestId('2fa-status')).toBeVisible();
    // When 2FA is off, the enable button is visible
    await expect(page.getByTestId('enable-2fa')).toBeVisible();
  });

  test('should follow redirect from /settings/security', async ({ page }) => {
    await page.goto('/settings/security');
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/[?&]section=security/);
  });

  test('should display active sessions list with current session indicator', async ({ page }) => {
    await page.goto('/profile?tab=settings&section=security');
    await page.waitForLoadState('networkidle');

    // ActiveSessionsCard header
    await expect(page.getByText(/active sessions/i)).toBeVisible();

    // Current session is labelled CURRENT (most-recently-seen session)
    await expect(page.getByText(/current/i)).toBeVisible();
  });

  test('should show "Sign out all other sessions" when multiple sessions exist', async ({
    page,
  }) => {
    await page.goto('/profile?tab=settings&section=security');
    await page.waitForLoadState('networkidle');

    // Mocked 2 sessions → button visible
    await expect(page.getByRole('button', { name: /sign out all other sessions/i })).toBeVisible();
  });
});

// ============================================================================
// Settings sections — API Keys
// ============================================================================

test.describe('Settings - API Keys', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page);
  });

  test('should display API keys section with create form', async ({ page }) => {
    await page.goto('/profile?tab=settings&section=api-keys');
    await page.waitForLoadState('networkidle');

    await expect(page.getByTestId('api-key-name-input')).toBeVisible();
    await expect(page.getByTestId('create-api-key-button')).toBeVisible();
  });

  test('should follow redirect from /settings/api-keys', async ({ page }) => {
    await page.goto('/settings/api-keys');
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/[?&]section=api-keys/);
  });

  test('should create an API key and show plaintext key dialog', async ({ page }) => {
    await page.goto('/profile?tab=settings&section=api-keys');
    await page.waitForLoadState('networkidle');

    await page.getByTestId('api-key-name-input').fill('My test key');
    await page.getByTestId('create-api-key-button').click();

    // After creation the dialog shows the plaintext key once
    await expect(page.getByTestId('api-key-plaintext')).toBeVisible({ timeout: 5000 });
    // Key should be non-empty
    const keyText = await page.getByTestId('api-key-plaintext').textContent();
    expect(keyText?.trim().length).toBeGreaterThan(0);
  });
});

// ============================================================================
// Settings sections — AI Consent
// ============================================================================

test.describe('Settings - AI Consent', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page);
  });

  test('should display AI consent section with toggles and save button', async ({ page }) => {
    await page.goto('/profile?tab=settings&section=ai-consent');
    await page.waitForLoadState('networkidle');

    await expect(page.getByTestId('save-ai-consent')).toBeVisible();
    await expect(page.getByTestId('ai-processing-toggle')).toBeVisible();
    await expect(page.getByTestId('external-providers-toggle')).toBeVisible();
  });

  test('should follow redirect from /settings/ai-consent', async ({ page }) => {
    await page.goto('/settings/ai-consent');
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/[?&]section=ai-consent/);
  });
});

// ============================================================================
// Settings sections — Placeholder sections (Notifications, Connected services)
// ============================================================================

test.describe('Settings - Placeholder sections', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page);
  });

  test('should show placeholder text for Notifications section', async ({ page }) => {
    await page.goto('/profile?tab=settings&section=notifications');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/settings ui in development/i)).toBeVisible();
  });

  test('should show placeholder text for Connected services section', async ({ page }) => {
    await page.goto('/profile?tab=settings&section=services');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/settings ui in development/i)).toBeVisible();
  });
});

// ============================================================================
// Error Handling
// ============================================================================

test.describe('Settings - Error Handling', () => {
  test('should redirect to login if not authenticated', async ({ page }) => {
    // Mock unauthenticated response
    await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Unauthorized' }),
      });
    });

    await page.goto('/profile?tab=settings');

    // Next.js middleware or page should redirect to login
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/\/login/);
  });

  test('should show error alert when profile save fails', async ({ page }) => {
    await setupSettingsMocks(page);

    // Override profile PUT to return 500
    await page.route(`${API_BASE}/api/v1/auth/profile`, async route => {
      if (['PUT', 'PATCH'].includes(route.request().method())) {
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Internal server error' }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ displayName: 'Test User', email: 'test@meepleai.dev' }),
        });
      }
    });

    await page.goto('/profile?tab=settings&section=profile');
    await page.waitForLoadState('networkidle');

    const displayNameInput = page.getByLabel(/display name/i);
    await displayNameInput.clear();
    await displayNameInput.fill('New Name');

    await page.getByTestId('save-profile-button').click();

    // Should show error feedback
    await expect(page.getByRole('alert')).toBeVisible({ timeout: 5000 });
  });
});
