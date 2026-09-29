/**
 * Profile/Settings E2E Tests — rewritten for the /settings hub (Issues #3938 / #3946 / #3961)
 *
 * Addressing model (current):
 *   - `/settings` and `/settings/<section>` are REAL routes
 *     (`src/app/(authenticated)/settings/page.tsx` + `[section]/page.tsx`) and they
 *     must NOT redirect. The `/settings*` redirects that used to live in
 *     `next.config.js` were removed: Next resolves config redirects BEFORE
 *     filesystem routing, so they shadowed the hub entirely.
 *   - The redirect now goes the OTHER way: `/profile?tab=settings[&section=<id>]`
 *     is forwarded client-side to `/settings[/<id>]` by `ProfilePageContent`, so
 *     bookmarks of the legacy address keep working.
 *   - `/settings/<unknown>` degrades to the default section (`profile`) instead of
 *     404-ing, because the backend emits section links that have no section yet
 *     (e.g. `NotificationRoutes.SettingsSubscription`).
 *
 * DOM structure:
 *   - `/profile` has THREE role="tab" buttons — Panoramica | Achievement | Attività.
 *     There is no "Settings" tab any more (#3938 moved it out), and the labels are
 *     Italian (#2201), so `getByRole('tab', { name: /settings|overview/i })` finds
 *     nothing by design.
 *   - The hub renders `SettingsSubNav`: a `<nav aria-label="Settings sections">` of
 *     `SettingsRow` buttons. The active row carries `aria-current="page"` — NOT
 *     `role="tab"`, NOT `aria-selected`.
 *   - Only `services` is still a placeholder. `notifications` renders the real
 *     `NotificationPreferences` panel (#3961) — it is the target of the footer link
 *     in every outgoing email, so it gets real coverage here.
 *
 * ─── DELETED COVERAGE (I3 — explicit documentation of removed tests) ────────────────────
 *
 * 1. "Change Password" (6 tests)
 *    DELETED — There is NO password-change UI in the settings hub. Password change
 *    was never part of issue #1608 scope. The API endpoint
 *    /api/v1/auth/change-password may still exist server-side, but no FE section
 *    exposes it. If a password-change flow is added later, create a dedicated spec
 *    file (e.g. e2e/settings/change-password.spec.ts).
 *
 * 2. "Delete Account / danger zone" (2 tests)
 *    DELETED — There is NO delete-account UI in the settings hub. The danger-zone
 *    section was removed from scope entirely. If implemented later, it belongs in its
 *    own spec or a dedicated describe block in this file.
 *
 * 3. "should change data retention period" (1 test)
 *    DELETED — PreferencesSection does NOT include a data-retention period field.
 *    The DTO for preferences covers only theme/language/emailNotifications.
 *    Re-add if data-retention is introduced to PreferencesSection in a future PR.
 *
 * 4. "should show email is disabled for changes" (1 test)
 *    DELETED — ProfileSection ALLOWS email editing (the field is an editable
 *    <Input type="email"> without a disabled attribute). The old "email is disabled"
 *    assertion is no longer valid. Email editing is in scope per Issue #1608.
 *
 * 5. The "should follow redirect from /settings/<section>" tests
 *    DELETED as standalone tests — the redirect they asserted no longer exists, and
 *    its replacement (the URL must STAY where the user asked) is now an assertion
 *    inside each section's own rendering test, so the deep link and the rendering
 *    are verified by a single navigation instead of two.
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
 * The SettingsSubNav row of the section currently rendered by the hub.
 *
 * Scoped to the sub-nav landmark on purpose: `aria-current="page"` is also used by
 * AppTopBar / SideDrawer / MiniNavSlot, so an unscoped `[aria-current="page"]`
 * would be a strict-mode violation.
 */
function activeSubNavRow(page: Page) {
  return page
    .getByRole('navigation', { name: 'Settings sections' })
    .locator('[aria-current="page"]');
}

/**
 * Setup mocks for the settings hub.
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

  // Notification preferences — feeds the real `NotificationPreferences` panel that
  // the `notifications` section renders since #3961. The catch-all above answers
  // `[]`, which fails `NotificationPreferencesSchema` and drops the panel into its
  // error state, so this route is load-bearing. `userId` MUST be a UUID for the
  // same reason (`z.string().uuid()`), which is why it is not `user.id`.
  await page.route(`${API_BASE}/api/v1/notifications/preferences`, async route => {
    if (route.request().method() !== 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        userId: '00000000-0000-4000-8000-000000000001',
        emailOnDocumentReady: true,
        emailOnDocumentFailed: true,
        emailOnRetryAvailable: false,
        pushOnDocumentReady: false,
        pushOnDocumentFailed: false,
        pushOnRetryAvailable: false,
        inAppOnDocumentReady: true,
        inAppOnDocumentFailed: true,
        inAppOnRetryAvailable: true,
        hasPushSubscription: false,
      }),
    });
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
// /profile — three-tab structure (#3938 moved settings out)
// ============================================================================

test.describe('Profile Page - Tab Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page);
  });

  test('should show three profile tabs and no Settings tab', async ({ page }) => {
    await page.goto('/profile');
    await page.waitForLoadState('networkidle');

    // Labels are Italian (#2201): Panoramica / Achievement / Attività.
    await expect(page.getByRole('tab', { name: /panoramica/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /achievement/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /attività/i })).toBeVisible();

    // #3938: settings left /profile for its own route — no tab may resurrect it.
    await expect(page.getByRole('tab', { name: /impostazioni|settings/i })).toHaveCount(0);
  });

  test('should navigate between profile tabs', async ({ page }) => {
    await page.goto('/profile');
    await page.waitForLoadState('networkidle');

    // Default is Panoramica (overview)
    await expect(page.getByRole('tab', { name: /panoramica/i })).toHaveAttribute(
      'aria-selected',
      'true'
    );

    await page.getByRole('tab', { name: /achievement/i }).click();
    await expect(page).toHaveURL(/[?&]tab=achievements/);

    await page.getByRole('tab', { name: /attività/i }).click();
    await expect(page).toHaveURL(/[?&]tab=activity/);

    await page.getByRole('tab', { name: /panoramica/i }).click();
    await expect(page).toHaveURL(/[?&]tab=overview/);
  });
});

// ============================================================================
// Settings hub — addressing
// ============================================================================

test.describe('Settings hub - addressing', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page);
  });

  test('should render the hub at /settings without redirecting anywhere', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // The whole point of #3938/#3946: the address the user asked for is the
    // address they keep. The `$` anchor also rules out a `?tab=`/`?section=`
    // query being bolted on.
    await expect(page).toHaveURL(/\/settings$/);
    await expect(page.getByRole('heading', { level: 1, name: 'Impostazioni' })).toBeVisible();
    // Bare `/settings` renders DEFAULT_SECTION.
    await expect(activeSubNavRow(page)).toContainText('Profile');
  });

  test('should forward the legacy /profile?tab=settings address to the hub', async ({ page }) => {
    // The redirect survives, but in the opposite direction: `ProfilePageContent`
    // forwards the moved tab (and its `?section=`) to the canonical sub-route.
    await page.goto('/profile?tab=settings&section=security');

    await page.waitForURL(/\/settings\/security$/);
    await expect(activeSubNavRow(page)).toContainText('Security');
  });
});

// ============================================================================
// Settings sections — Profile
// ============================================================================

test.describe('Settings - Update Profile', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page, { displayName: 'Original Name' });
  });

  test('should display the profile form at /settings/profile', async ({ page }) => {
    await page.goto('/settings/profile');
    await page.waitForLoadState('networkidle');

    // Deep link resolves in place — no redirect, no query rewrite.
    await expect(page).toHaveURL(/\/settings\/profile$/);
    await expect(activeSubNavRow(page)).toContainText('Profile');

    // Profile section form elements visible
    await expect(page.getByLabel(/display name/i)).toBeVisible();
    await expect(page.getByTestId('save-profile-button')).toBeVisible();
  });

  test('should update display name successfully', async ({ page }) => {
    await page.goto('/settings/profile');
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
    // ProfileSection allows email editing — opposite of the old disabled assertion
    await page.goto('/settings/profile');
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

  test('should display preferences section at /settings/preferences', async ({ page }) => {
    await page.goto('/settings/preferences');
    await page.waitForLoadState('networkidle');

    await expect(page).toHaveURL(/\/settings\/preferences$/);
    await expect(activeSubNavRow(page)).toContainText('Preferences');

    // Section header — the heading, not `getByText(/preferences/i)`: the word also
    // appears on the sub-nav row and on the "Save preferences" button, which makes
    // the loose matcher a strict-mode violation.
    await expect(page.getByRole('heading', { name: 'Preferences' })).toBeVisible();
    await expect(page.getByLabel(/theme/i)).toBeVisible();
    await expect(page.getByLabel(/language/i)).toBeVisible();
    await expect(page.getByTestId('save-preferences-button')).toBeVisible();
  });

  test('should change theme preference and save', async ({ page }) => {
    await page.goto('/settings/preferences');
    await page.waitForLoadState('networkidle');

    const themeSelect = page.getByLabel(/theme/i);
    await themeSelect.selectOption('dark');

    await page.getByTestId('save-preferences-button').click();

    await expect(page.getByRole('alert')).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('alert')).toContainText(/preferences updated|saved|success/i);
  });

  test('should change language preference and save', async ({ page }) => {
    await page.goto('/settings/preferences');
    await page.waitForLoadState('networkidle');

    const languageSelect = page.getByLabel(/language/i);
    await languageSelect.selectOption('en');

    await page.getByTestId('save-preferences-button').click();

    await expect(page.getByRole('alert')).toBeVisible({ timeout: 5000 });
  });

  test('should toggle email notifications and save', async ({ page }) => {
    await page.goto('/settings/preferences');
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

  test('should display 2FA status card and enable button at /settings/security', async ({
    page,
  }) => {
    await page.goto('/settings/security');
    await page.waitForLoadState('networkidle');

    await expect(page).toHaveURL(/\/settings\/security$/);
    await expect(activeSubNavRow(page)).toContainText('Security');

    // TwoFactorStatusCard renders data-testid="2fa-status"
    await expect(page.getByTestId('2fa-status')).toBeVisible();
    // When 2FA is off, the enable button is visible
    await expect(page.getByTestId('enable-2fa')).toBeVisible();
  });

  test('should display active sessions list with current session indicator', async ({ page }) => {
    await page.goto('/settings/security');
    await page.waitForLoadState('networkidle');

    // ActiveSessionsCard header
    await expect(page.getByText(/active sessions/i)).toBeVisible();

    // Current session is labelled CURRENT (most-recently-seen session)
    await expect(page.getByText(/current/i)).toBeVisible();
  });

  test('should show "Sign out all other sessions" when multiple sessions exist', async ({
    page,
  }) => {
    await page.goto('/settings/security');
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

  test('should display API keys section at /settings/api-keys', async ({ page }) => {
    await page.goto('/settings/api-keys');
    await page.waitForLoadState('networkidle');

    await expect(page).toHaveURL(/\/settings\/api-keys$/);
    await expect(activeSubNavRow(page)).toContainText('API keys');

    await expect(page.getByTestId('api-key-name-input')).toBeVisible();
    await expect(page.getByTestId('create-api-key-button')).toBeVisible();
  });

  test('should create an API key and show plaintext key dialog', async ({ page }) => {
    await page.goto('/settings/api-keys');
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

  test('should display AI consent section at /settings/ai-consent', async ({ page }) => {
    await page.goto('/settings/ai-consent');
    await page.waitForLoadState('networkidle');

    await expect(page).toHaveURL(/\/settings\/ai-consent$/);
    await expect(activeSubNavRow(page)).toContainText('AI & data consent');

    await expect(page.getByTestId('save-ai-consent')).toBeVisible();
    await expect(page.getByTestId('ai-processing-toggle')).toBeVisible();
    await expect(page.getByTestId('external-providers-toggle')).toBeVisible();
  });
});

// ============================================================================
// Settings sections — Notifications (the address every email footer links to)
// ============================================================================

test.describe('Settings - Notifications', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page);
  });

  test('should render the real notification preferences panel at /settings/notifications', async ({
    page,
  }) => {
    // `EmailTemplateService.WrapInBaseTemplate` puts this exact URL in the footer
    // of every outgoing email, so both halves matter: the address must resolve in
    // place, and what it renders must be the real panel — it was the placeholder
    // until #3961.
    await page.goto('/settings/notifications');
    await page.waitForLoadState('networkidle');

    await expect(page).toHaveURL(/\/settings\/notifications$/);
    await expect(activeSubNavRow(page)).toContainText('Notifications');

    await expect(page.getByTestId('pref-category-document-ready')).toBeVisible();
    await expect(page.getByTestId('pref-quietHoursEnabled')).toBeVisible();
    await expect(page.getByTestId('save-preferences')).toBeVisible();
    await expect(page.getByText(/settings ui in development/i)).toHaveCount(0);
  });
});

// ============================================================================
// Settings sections — Placeholder (Connected services is the only one left)
// ============================================================================

test.describe('Settings - Placeholder sections', () => {
  test.beforeEach(async ({ page }) => {
    await setupSettingsMocks(page);
  });

  test('should show placeholder text for Connected services section', async ({ page }) => {
    await page.goto('/settings/services');
    await page.waitForLoadState('networkidle');

    await expect(page).toHaveURL(/\/settings\/services$/);
    await expect(page.getByText(/settings ui in development/i)).toBeVisible();
  });
});

// ============================================================================
// Error Handling
// ============================================================================

test.describe('Settings - Error Handling', () => {
  test('should send an anonymous visitor to login, preserving the settings deep link', async ({
    page,
  }) => {
    // Belt and braces: the edge guard fires first (`/settings` is in
    // PROTECTED_ROUTES and no session cookie is present), but if it ever let the
    // request through, the client would see this 401 and bounce to /login too.
    await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Unauthorized' }),
      });
    });

    // The email-footer link is what an anonymous recipient actually clicks, and
    // `?from=` is the only thing that gets them back to it after logging in. Pin
    // the whole path, section included: `from=/settings` would already be a lost
    // deep link.
    await page.goto('/settings/notifications');

    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/\/login\?from=(%2F|\/)settings(%2F|\/)notifications$/);
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

    // Straight to the canonical address: going through `/profile?tab=settings`
    // would race `networkidle` against the client-side forward.
    await page.goto('/settings/profile');
    await page.waitForLoadState('networkidle');

    const displayNameInput = page.getByLabel(/display name/i);
    await displayNameInput.clear();
    await displayNameInput.fill('New Name');

    await page.getByTestId('save-profile-button').click();

    // Should show error feedback
    await expect(page.getByRole('alert')).toBeVisible({ timeout: 5000 });
  });
});
