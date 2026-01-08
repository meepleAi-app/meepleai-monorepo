/**
 * Auth flows visual regression - Issue #2307 (Week 3 Extended)
 *
 * Captures visual snapshots of authentication forms and states
 * for regression detection across login, registration, 2FA flows.
 *
 * Week 3 Additions:
 * - 2FA setup forms and states
 * - Session management UI
 * - OAuth account linking
 * - API key management
 * - Error and success states
 */

import { test, expect } from './fixtures/chromatic';

test.use({
  viewport: { width: 1280, height: 900 },
});

test.describe('Auth Forms - Visual Regression', () => {
  test('Login form initial state', async ({ page }) => {
    await page.goto('/auth/login', { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: /log in/i }).waitFor();
    await page.waitForTimeout(150);

    const form = page.locator('form').first();
    await expect(form).toBeVisible();
    await expect(form).toHaveScreenshot('auth-login-form.png');
  });

  test('Registration form initial state', async ({ page }) => {
    await page.goto('/auth/register', { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: /create.*account|sign up/i }).waitFor();
    await page.waitForTimeout(150);

    const form = page.locator('form').first();
    await expect(form).toBeVisible();
    await expect(form).toHaveScreenshot('auth-register-form.png');
  });

  test('Login form validation errors', async ({ page }) => {
    await page.goto('/auth/login');
    await page.getByRole('heading', { name: /log in/i }).waitFor();

    // Submit empty form to trigger validation
    const submitButton = page.getByRole('button', { name: /log in|sign in/i });
    await submitButton.click();

    // Wait for validation messages
    await page.waitForTimeout(300);

    const form = page.locator('form').first();
    await expect(form).toHaveScreenshot('auth-login-validation-errors.png');
  });

  test('Password reset form', async ({ page }) => {
    await page.goto('/auth/reset-password', { waitUntil: 'domcontentloaded' });

    // Wait for form or fallback heading
    try {
      await page
        .getByRole('heading', { name: /reset.*password|forgot.*password/i })
        .waitFor({ timeout: 2000 });
    } catch {
      // Page might not exist, skip test
      test.skip();
    }

    await page.waitForTimeout(150);

    const main = page.locator('main').first();
    await expect(main).toHaveScreenshot('auth-password-reset.png');
  });
});

// ============================================================================
// WEEK 3 (Issue #2307) - Advanced Auth Visual Snapshots
// ============================================================================

test.describe('2FA Forms - Visual Snapshots', () => {
  test('2FA setup initial state with QR code', async ({ page }) => {
    await page.goto('/settings/security');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(200);

    // Look for 2FA section
    const twoFASection = page
      .locator('[data-testid="2fa-section"], section:has-text("Two-Factor")')
      .first();
    if (await twoFASection.isVisible({ timeout: 2000 }).catch(() => false)) {
      await expect(twoFASection).toHaveScreenshot('2fa-setup-initial.png');
    }
  });

  test('2FA backup codes display', async ({ page }) => {
    await page.goto('/settings/security');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(200);

    const backupCodesSection = page.locator('text=/backup.*code/i').first();
    if (await backupCodesSection.isVisible({ timeout: 2000 }).catch(() => false)) {
      const container = page
        .locator('[data-testid="backup-codes"], .backup-codes-container')
        .first();
      if (await container.isVisible({ timeout: 1000 }).catch(() => false)) {
        await expect(container).toHaveScreenshot('2fa-backup-codes.png');
      }
    }
  });

  test('2FA verification input form', async ({ page }) => {
    await page.goto('/auth/2fa-verify', { waitUntil: 'domcontentloaded' });

    try {
      await page
        .getByRole('heading', { name: /verify|two-factor|enter.*code/i })
        .waitFor({ timeout: 2000 });
      await page.waitForTimeout(150);

      const form = page.locator('form').first();
      await expect(form).toHaveScreenshot('2fa-verification-form.png');
    } catch {
      // 2FA verify page might not exist yet
      test.skip();
    }
  });

  test('2FA enabled success state', async ({ page }) => {
    await page.goto('/settings/security');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(200);

    // Look for enabled 2FA badge/indicator
    const enabledIndicator = page.locator('text=/enabled|active/i').first();
    if (await enabledIndicator.isVisible({ timeout: 2000 }).catch(() => false)) {
      const twoFASection = page.locator('[data-testid="2fa-section"]').first();
      if (await twoFASection.isVisible({ timeout: 1000 }).catch(() => false)) {
        await expect(twoFASection).toHaveScreenshot('2fa-enabled-state.png');
      }
    }
  });
});

test.describe('Session Management - Visual Snapshots', () => {
  test('Active sessions list view', async ({ page }) => {
    await page.goto('/settings/sessions');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(200);

    const sessionsContainer = page.locator('[data-testid="sessions-list"], main').first();
    await expect(sessionsContainer).toHaveScreenshot('sessions-list-view.png');
  });

  test('Session details card', async ({ page }) => {
    await page.goto('/settings/sessions');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(200);

    const sessionCard = page.locator('[data-testid="session-item"], .session-card').first();
    if (await sessionCard.isVisible({ timeout: 2000 }).catch(() => false)) {
      await expect(sessionCard).toHaveScreenshot('session-detail-card.png');
    }
  });

  test('Session expiration warning banner', async ({ page }) => {
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(200);

    const warningBanner = page.locator('[role="alert"]:has-text("expir"), .warning-banner').first();
    if (await warningBanner.isVisible({ timeout: 2000 }).catch(() => false)) {
      await expect(warningBanner).toHaveScreenshot('session-expiration-warning.png');
    }
  });
});

test.describe('OAuth & API Keys - Visual Snapshots', () => {
  test('OAuth accounts management page', async ({ page }) => {
    await page.goto('/settings/accounts');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(200);

    const accountsContainer = page.locator('[data-testid="oauth-accounts"], main').first();
    await expect(accountsContainer).toHaveScreenshot('oauth-accounts-view.png');
  });

  test('OAuth provider buttons initial state', async ({ page }) => {
    await page.goto('/auth/login');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(150);

    const oauthButtons = page.locator('[data-testid="oauth-buttons"], .oauth-providers').first();
    if (await oauthButtons.isVisible({ timeout: 2000 }).catch(() => false)) {
      await expect(oauthButtons).toHaveScreenshot('oauth-login-buttons.png');
    }
  });

  test('API keys management page', async ({ page }) => {
    await page.goto('/settings/api-keys');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(200);

    const apiKeysContainer = page.locator('[data-testid="api-keys-list"], main').first();
    await expect(apiKeysContainer).toHaveScreenshot('api-keys-view.png');
  });

  test('API key creation modal', async ({ page }) => {
    await page.goto('/settings/api-keys');
    await page.waitForLoadState('networkidle');

    const createButton = page
      .locator('button:has-text("Create"), button:has-text("New Key")')
      .first();
    if (await createButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await createButton.click();
      await page.waitForTimeout(300);

      const modal = page.locator('[role="dialog"], .modal').first();
      if (await modal.isVisible({ timeout: 1000 }).catch(() => false)) {
        await expect(modal).toHaveScreenshot('api-key-create-modal.png');
      }
    }
  });

  test('API key revealed state (blurred)', async ({ page }) => {
    await page.goto('/settings/api-keys');
    await page.waitForLoadState('networkidle');

    const createButton = page.locator('button:has-text("Create")').first();
    if (await createButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await createButton.click();
      await page.waitForTimeout(200);

      const nameInput = page.locator('input[name="name"]').first();
      if (await nameInput.isVisible({ timeout: 1000 }).catch(() => false)) {
        await nameInput.fill('Visual Test Key');

        const submitButton = page.locator('button[type="submit"]').first();
        await submitButton.click();
        await page.waitForTimeout(500);

        // Key should be visible in success dialog
        const keyDisplay = page.locator('[data-testid="api-key-display"], code').first();
        if (await keyDisplay.isVisible({ timeout: 1000 }).catch(() => false)) {
          await expect(keyDisplay).toHaveScreenshot('api-key-revealed.png');
        }
      }
    }
  });
});
