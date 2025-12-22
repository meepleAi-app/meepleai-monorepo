/**
 * Auth Password Reset E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/helpers/AuthHelper.ts
 */

import { test, expect, Page } from './fixtures/chromatic';
import {
  setupMockEmailService,
  setupMockTokenVerification,
  setupMockPasswordResetConfirm,
  getResetTokenForEmail,
  wasEmailSentTo,
  clearCapturedEmails,
} from './fixtures/email';
import { AuthPage } from './pages/auth/AuthPage';

const API_BASE = 'http://localhost:8080';

// Test user data
const TEST_USER = {
  email: 'testuser@meepleai.dev',
  password: 'OldPassword123!',
  newPassword: 'NewPassword456!',
};

/**
 * Helper: Setup mock authentication endpoints for login after reset
 */
async function setupMockLogin(page: Page, email: string): Promise<void> {
  const userResponse = {
    user: {
      id: 'test-user-id',
      email,
      displayName: 'Test User',
      role: 'User',
    },
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  };

  await page.route(`${API_BASE}/api/v1/auth/login`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(userResponse),
    });
  });

  await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(userResponse),
    });
  });
}

/**
 * Helper: Setup rate limiting error
 */
async function setupRateLimitError(page: Page): Promise<void> {
  await page.route(`${API_BASE}/api/v1/auth/password-reset/request`, async route => {
    await route.fulfill({
      status: 429,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Too many password reset requests. Please try again later.' }),
    });
  });
}

test.describe('Password Reset - Request Flow', () => {
  let authPage: AuthPage;

  test.beforeEach(async ({ page }) => {
    authPage = new AuthPage(page);
    clearCapturedEmails();
    await setupMockEmailService(page);
  });

  test('should access forgot password page', async ({ page }) => {
    await authPage.gotoPasswordReset();

    // Verify page elements
    await expect(page.getByRole('heading', { name: /reset password/i })).toBeVisible();
    await expect(page.getByText(/enter your email address.*send you instructions/i)).toBeVisible();
    await expect(page.getByLabel(/email address/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /send reset instructions/i })).toBeVisible();
  });

  test('should validate email field is required', async ({ page }) => {
    await authPage.gotoPasswordReset();

    // Try to submit without email
    const submitButton = page.getByRole('button', { name: /send reset instructions/i });
    await expect(submitButton).toBeDisabled();

    // Enter email, button should enable
    await page.getByLabel(/email address/i).fill(TEST_USER.email);
    await expect(submitButton).toBeEnabled();
  });

  test('should validate email format', async ({ page }) => {
    await authPage.gotoPasswordReset();

    // Enter invalid email
    const emailInput = page.getByLabel(/email address/i);
    await emailInput.fill('invalid-email');

    // HTML5 validation should prevent submission
    const isValid = await emailInput.evaluate((el: HTMLInputElement) => el.checkValidity());
    expect(isValid).toBe(false);

    // Enter valid email
    await emailInput.fill(TEST_USER.email);
    const isValidNow = await emailInput.evaluate((el: HTMLInputElement) => el.checkValidity());
    expect(isValidNow).toBe(true);
  });

  test('should show success message for valid email', async ({ page }) => {
    await authPage.gotoPasswordReset();

    // Submit reset request
    await authPage.requestPasswordReset(TEST_USER.email);

    // Wait for success message
    await authPage.waitForResetRequestSuccess();

    // Verify success UI
    await expect(page.getByText(/check your email/i)).toBeVisible();
    await expect(page.getByText(TEST_USER.email)).toBeVisible();
    await expect(page.getByText(/didn't receive the email/i)).toBeVisible();

    // Verify email was "sent"
    expect(wasEmailSentTo(TEST_USER.email)).toBe(true);
  });

  test('should handle rate limiting (prevent spam)', async ({ page }) => {
    // Setup rate limit error
    await setupRateLimitError(page);
    await authPage.gotoPasswordReset();

    // Submit reset request
    await authPage.requestPasswordReset(TEST_USER.email);

    // Wait for error message
    await expect(
      page.getByText(/too many password reset requests.*try again later/i)
    ).toBeVisible();
  });
});

test.describe('Password Reset - Token Verification', () => {
  let authPage: AuthPage;

  test.beforeEach(async ({ page }) => {
    authPage = new AuthPage(page);
    clearCapturedEmails();
  });

  test('should extract and use reset token from mock email', async ({ page }) => {
    // Setup mocks
    await setupMockEmailService(page);
    await setupMockTokenVerification(page, true);

    // Request reset
    await authPage.gotoPasswordReset();
    await authPage.requestPasswordReset(TEST_USER.email);
    await authPage.waitForResetRequestSuccess();

    // Extract token
    const resetToken = getResetTokenForEmail(TEST_USER.email);
    expect(resetToken).toBeTruthy();
    expect(resetToken).toMatch(/^[A-Za-z0-9_-]{43}$/); // URL-safe base64, 43 chars

    // Navigate to reset page with token
    await authPage.gotoPasswordResetWithToken(resetToken!);

    // Verify token verification was triggered
    await expect(page.getByRole('heading', { name: /set new password/i })).toBeVisible();
  });

  test('should show error for invalid token', async ({ page }) => {
    // Setup mock for invalid token
    await setupMockTokenVerification(page, false);

    // Navigate with invalid token
    await authPage.gotoPasswordResetWithToken('invalid-token-xyz123');

    // Wait for error message (heading first, specific selector to avoid strict mode)
    await authPage.waitForInvalidTokenMessage();
    await expect(page.getByRole('heading', { name: /invalid or expired link/i })).toBeVisible();
    await expect(page.getByText(/password reset link is invalid or has expired/i)).toBeVisible();
  });

  test('should show error for expired token', async ({ page }) => {
    // Setup mock for expired token
    await setupMockTokenVerification(page, false);

    // Navigate with expired token
    const expiredToken = 'expired-token-abcdefghijklmnopqrstuvwxyz123456';
    await authPage.gotoPasswordResetWithToken(expiredToken);

    // Wait for error message (heading specific to avoid strict mode)
    await authPage.waitForInvalidTokenMessage();
    await expect(page.getByRole('heading', { name: /invalid or expired link/i })).toBeVisible();
    await expect(page.getByRole('link', { name: /request new reset link/i })).toBeVisible();
  });
});

test.describe('Password Reset - New Password Submission', () => {
  let authPage: AuthPage;
  const validToken = 'test-token-abcdefghijklmnopqrstuvwxyz123456789';

  test.beforeEach(async ({ page }) => {
    authPage = new AuthPage(page);
    clearCapturedEmails();

    // Mock /auth/me to return unauthenticated (password reset doesn't require auth)
    await page.route(
      url => url.href.includes('/api/v1/auth/me'),
      async route => {
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Unauthorized' }),
        });
      }
    );

    // Setup mocks BEFORE navigation (no need to actually request reset for these tests)
    await setupMockTokenVerification(page, true);
    await setupMockPasswordResetConfirm(page, true);
    await setupMockLogin(page, TEST_USER.email);

    // Navigate directly to reset page with valid token
    await authPage.gotoPasswordResetWithToken(validToken);

    // Wait for page to load and token verification to complete
    // Try multiple selectors as the UI might vary
    try {
      await expect(page.getByRole('heading', { name: /set new password/i })).toBeVisible({
        timeout: 15000,
      });
    } catch (e) {
      // If heading not found, try waiting for form elements as fallback
      await expect(page.getByLabel(/^new password$/i)).toBeVisible({ timeout: 10000 });
    }
  });

  test('should validate password requirements (min 8 chars, uppercase, lowercase, digit)', async ({
    page,
  }) => {
    // Wait for form to be fully loaded
    const newPasswordInput = page.getByLabel(/^new password$/i);
    await expect(newPasswordInput).toBeVisible({ timeout: 10000 });

    // Test weak password (too short)
    await newPasswordInput.fill('Pass1');
    await expect(page.getByText(/at least 8 characters/i)).toHaveClass(/text-slate-500/);

    // Test weak password (no uppercase)
    await newPasswordInput.fill('password123');
    await expect(page.getByText(/at least 1 uppercase letter/i)).toHaveClass(/text-slate-500/);

    // Test weak password (no lowercase)
    await newPasswordInput.fill('PASSWORD123');
    await expect(page.getByText(/at least 1 lowercase letter/i)).toHaveClass(/text-slate-500/);

    // Test weak password (no digit)
    await newPasswordInput.fill('Password');
    await expect(page.getByText(/at least 1 number/i)).toHaveClass(/text-slate-500/);

    // Test strong password (all requirements met)
    await newPasswordInput.fill(TEST_USER.newPassword);
    await expect(page.getByText(/at least 8 characters/i)).toHaveClass(/text-green-400/);
    await expect(page.getByText(/at least 1 uppercase letter/i)).toHaveClass(/text-green-400/);
    await expect(page.getByText(/at least 1 lowercase letter/i)).toHaveClass(/text-green-400/);
    await expect(page.getByText(/at least 1 number/i)).toHaveClass(/text-green-400/);
  });

  test('should show password strength indicator', async ({ page }) => {
    // Wait for form to be fully loaded
    const newPasswordInput = page.getByLabel(/^new password$/i);
    await expect(newPasswordInput).toBeVisible({ timeout: 10000 });

    // Weak password
    await newPasswordInput.fill('pass');
    await expect(page.getByText(/password strength:/i)).toBeVisible();
    await expect(page.getByText(/weak/i)).toBeVisible();

    // Medium password
    await newPasswordInput.fill('Password1');
    await expect(page.getByText(/medium|strong/i)).toBeVisible();

    // Strong password
    await newPasswordInput.fill(TEST_USER.newPassword);
    await expect(page.getByText(/strong/i)).toBeVisible();
  });

  test('should validate password confirmation matches', async ({ page }) => {
    // Wait for form to be fully loaded
    const newPasswordInput = page.getByLabel(/^new password$/i);
    const confirmPasswordInput = page.getByLabel(/confirm password/i);
    await expect(newPasswordInput).toBeVisible({ timeout: 10000 });

    // Enter valid password
    await newPasswordInput.fill(TEST_USER.newPassword);

    // Enter non-matching confirmation
    await confirmPasswordInput.fill('DifferentPassword123!');

    // Verify error message
    await expect(page.getByText(/passwords do not match/i)).toBeVisible();

    // Enter matching confirmation
    await confirmPasswordInput.fill(TEST_USER.newPassword);

    // Error should disappear
    await expect(page.getByText(/passwords do not match/i)).not.toBeVisible();
  });

  test('should disable submit button when password invalid', async ({ page }) => {
    // Wait for form to be fully loaded
    const submitButton = page.getByRole('button', { name: /reset password/i });
    await expect(submitButton).toBeVisible({ timeout: 10000 });

    // Button should be disabled initially
    await expect(submitButton).toBeDisabled();

    // Enter valid password but no confirmation
    await page.getByLabel(/^new password$/i).fill(TEST_USER.newPassword);
    await expect(submitButton).toBeDisabled();

    // Enter mismatched confirmation
    await page.getByLabel(/confirm password/i).fill('DifferentPassword123!');
    await expect(submitButton).toBeDisabled();

    // Enter matching confirmation
    await page.getByLabel(/confirm password/i).fill(TEST_USER.newPassword);
    await expect(submitButton).toBeEnabled();
  });

  test('should successfully reset password and redirect', async ({ page }) => {
    // Wait for form to be fully loaded before submitting
    await expect(page.getByLabel(/^new password$/i)).toBeVisible({ timeout: 10000 });

    // Submit new password
    await authPage.submitNewPassword(TEST_USER.newPassword, TEST_USER.newPassword);

    // Wait for success message
    await authPage.waitForResetSuccess();
    await expect(page.getByText(/password reset successful/i)).toBeVisible();
    await expect(page.getByText(/your password has been successfully reset/i)).toBeVisible();

    // Should show redirecting message
    await expect(page.getByText(/redirecting to chat/i)).toBeVisible();
  });

  test('should handle reset failure gracefully', async ({ page }) => {
    // Setup mock to fail
    await setupMockPasswordResetConfirm(page, false);

    // Wait for form to be fully loaded
    await expect(page.getByLabel(/^new password$/i)).toBeVisible({ timeout: 10000 });

    // Submit new password
    await authPage.submitNewPassword(TEST_USER.newPassword, TEST_USER.newPassword);

    // Wait for error message
    await expect(page.getByText(/failed to reset password.*try again/i)).toBeVisible();
  });
});

test.describe('Password Reset - Security', () => {
  let authPage: AuthPage;

  test.beforeEach(async ({ page }) => {
    authPage = new AuthPage(page);
    clearCapturedEmails();
  });

  test('should not reveal if email exists (prevent enumeration)', async ({ page }) => {
    // Setup mock
    await setupMockEmailService(page);
    await authPage.gotoPasswordReset();

    // Request reset for non-existent email
    await authPage.requestPasswordReset('nonexistent@example.com');

    // Should still show success message (security measure)
    await authPage.waitForResetRequestSuccess();
    await expect(page.getByText(/check your email/i)).toBeVisible();

    // Note: In production, no email is actually sent, but UI behaves the same
  });

  test('should prevent token reuse', async ({ page }) => {
    const token = 'used-token-abcdefghijklmnopqrstuvwxyz123456789';

    // Mock /auth/me to return unauthenticated
    await page.route(
      url => url.href.includes('/api/v1/auth/me'),
      async route => {
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Unauthorized' }),
        });
      }
    );

    // First use: valid token
    await setupMockTokenVerification(page, true);
    await setupMockPasswordResetConfirm(page, true);
    await setupMockLogin(page, TEST_USER.email);

    await authPage.gotoPasswordResetWithToken(token);
    // Wait for token verification and form load
    await expect(page.getByRole('heading', { name: /set new password/i })).toBeVisible({
      timeout: 10000,
    });
    await expect(page.getByLabel(/^new password$/i)).toBeVisible({ timeout: 10000 });

    // Submit reset (this should mark token as used)
    await authPage.submitNewPassword(TEST_USER.newPassword, TEST_USER.newPassword);
    await authPage.waitForResetSuccess();

    // Second use: token should be invalid now
    await setupMockTokenVerification(page, false); // Simulate backend rejecting reused token
    await authPage.gotoPasswordResetWithToken(token);
    await authPage.waitForInvalidTokenMessage();
    await expect(page.getByText(/password reset link is invalid or has expired/i)).toBeVisible();
  });
});

test.describe('Password Reset - Edge Cases', () => {
  let authPage: AuthPage;

  test.beforeEach(async ({ page }) => {
    authPage = new AuthPage(page);
    clearCapturedEmails();
  });

  test('should allow requesting new reset if email not received', async ({ page }) => {
    await setupMockEmailService(page);
    await authPage.gotoPasswordReset();

    // Request reset
    await authPage.requestPasswordReset(TEST_USER.email);
    await authPage.waitForResetRequestSuccess();

    // Click "try again" link
    await page.getByRole('button', { name: /try again/i }).click();

    // Should return to request form
    await expect(page.getByLabel(/email address/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /send reset instructions/i })).toBeVisible();
  });

  test('should navigate back to login from reset page', async ({ page }) => {
    await authPage.gotoPasswordReset();

    // Click back to login link
    await page.getByRole('link', { name: /back to login/i }).click();

    // Should navigate to home page
    await expect(page).toHaveURL('/');
  });

  test('should handle network errors gracefully', async ({ page }) => {
    // Setup network error (500 Internal Server Error)
    await page.route(`${API_BASE}/api/v1/auth/password-reset/request`, async route => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Internal server error' }),
      });
    });

    await authPage.gotoPasswordReset();
    await authPage.requestPasswordReset(TEST_USER.email);

    // Should show error message (check for generic error or API error with increased timeout)
    const errorVisible =
      (await page
        .getByText(/failed to send reset email/i)
        .isVisible({ timeout: 5000 })
        .catch(() => false)) ||
      (await page
        .getByText(/internal server error/i)
        .isVisible({ timeout: 5000 })
        .catch(() => false)) ||
      (await page
        .getByText(/something went wrong/i)
        .isVisible({ timeout: 5000 })
        .catch(() => false)) ||
      (await page
        .getByText(/error/i)
        .isVisible({ timeout: 5000 })
        .catch(() => false));

    expect(errorVisible).toBeTruthy();
  });
});
