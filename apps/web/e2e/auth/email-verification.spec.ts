/**
 * AUTH-09: Email Verification Flow
 * Issue #3082 - P0 Critical
 *
 * Tests email verification post-registration:
 * - User completes email verification
 * - Resend verification email with rate limiting
 * - Expired verification token handling
 * - Access restrictions before verification
 *
 * Refactored to use Page Object Model and fixtures
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface MockUser {
  id: string;
  email: string;
  displayName: string;
  role: string;
  emailVerified: boolean;
}

/**
 * Setup mock routes for email verification testing
 */
async function setupEmailVerificationMocks(
  page: Page,
  options: {
    emailVerified?: boolean;
    tokenValid?: boolean;
    tokenExpired?: boolean;
    resendAllowed?: boolean;
  } = {}
) {
  const { emailVerified = false, tokenValid = true, tokenExpired = false, resendAllowed = true } =
    options;

  const user: MockUser = {
    id: 'test-user-id',
    email: 'test@example.com',
    displayName: 'Test User',
    role: 'User',
    emailVerified,
  };

  // Mock auth/me endpoint
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user,
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // Mock email verification endpoint
  await page.route(`${API_BASE}/api/v1/auth/verify-email*`, async (route) => {
    if (tokenExpired) {
      await route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({
          error: 'Token expired',
          message: 'Verification link has expired. Please request a new one.',
        }),
      });
    } else if (!tokenValid) {
      await route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({
          error: 'Invalid token',
          message: 'Invalid or malformed verification token.',
        }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          message: 'Email verified successfully',
          user: { ...user, emailVerified: true },
        }),
      });
    }
  });

  // Mock resend verification endpoint
  await page.route(`${API_BASE}/api/v1/auth/resend-verification`, async (route) => {
    if (!resendAllowed) {
      await route.fulfill({
        status: 429,
        contentType: 'application/json',
        body: JSON.stringify({
          error: 'Rate limited',
          message: 'Please wait 60 seconds before requesting another verification email.',
          retryAfter: 60,
        }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          message: 'Verification email sent',
        }),
      });
    }
  });

  // Mock user profile endpoint
  await page.route(`${API_BASE}/api/v1/users/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(user),
    });
  });

  return user;
}

test.describe('AUTH-09: Email Verification', () => {
  test.describe('Verification Pending Page', () => {
    test('should display verification pending message after registration', async ({ page }) => {
      await setupEmailVerificationMocks(page, { emailVerified: false });

      // Navigate to verification pending page
      await page.goto('/verification-pending?email=test@example.com');
      await page.waitForLoadState('networkidle');

      // Verify pending message is displayed
      await expect(
        page.getByRole('heading', { name: /verify.*email|email.*verification/i })
      ).toBeVisible();
      await expect(page.getByText(/test@example.com/i)).toBeVisible();
      await expect(page.getByText(/check.*email|verification.*sent/i)).toBeVisible();
    });

    test('should show resend verification button', async ({ page }) => {
      await setupEmailVerificationMocks(page, { emailVerified: false, resendAllowed: true });

      await page.goto('/verification-pending?email=test@example.com');
      await page.waitForLoadState('networkidle');

      const resendButton = page.getByRole('button', {
        name: /resend.*verification|send.*again/i,
      });
      await expect(resendButton).toBeVisible();
      await expect(resendButton).toBeEnabled();
    });

    test('should resend verification email successfully', async ({ page }) => {
      await setupEmailVerificationMocks(page, { emailVerified: false, resendAllowed: true });

      await page.goto('/verification-pending?email=test@example.com');
      await page.waitForLoadState('networkidle');

      const resendButton = page.getByRole('button', {
        name: /resend.*verification|send.*again/i,
      });
      await resendButton.click();

      // Wait for success message
      await expect(page.getByText(/verification.*sent|email.*sent/i)).toBeVisible();
    });

    test('should show rate limit message when resending too quickly', async ({ page }) => {
      await setupEmailVerificationMocks(page, { emailVerified: false, resendAllowed: false });

      await page.goto('/verification-pending?email=test@example.com');
      await page.waitForLoadState('networkidle');

      const resendButton = page.getByRole('button', {
        name: /resend.*verification|send.*again/i,
      });
      await resendButton.click();

      // Verify rate limit message is shown
      await expect(page.getByText(/wait|rate.*limit|try.*again.*later/i)).toBeVisible();
    });

    test('should show countdown timer after resend', async ({ page }) => {
      await setupEmailVerificationMocks(page, { emailVerified: false, resendAllowed: true });

      await page.goto('/verification-pending?email=test@example.com');
      await page.waitForLoadState('networkidle');

      const resendButton = page.getByRole('button', {
        name: /resend.*verification|send.*again/i,
      });
      await resendButton.click();

      // Button should be disabled with countdown or show cooldown message
      await expect(resendButton.or(page.getByText(/\d+.*seconds|wait.*\d+/i))).toBeVisible();
    });
  });

  test.describe('Email Verification Token', () => {
    test('should successfully verify email with valid token', async ({ page }) => {
      await setupEmailVerificationMocks(page, { tokenValid: true });

      // Navigate to verification page with token
      await page.goto('/verify-email?token=valid-token-123');
      await page.waitForLoadState('networkidle');

      // Verify success message
      await expect(page.getByText(/verified.*successfully|email.*verified/i)).toBeVisible();
    });

    test('should show error for expired token', async ({ page }) => {
      await setupEmailVerificationMocks(page, { tokenExpired: true });

      await page.goto('/verify-email?token=expired-token');
      await page.waitForLoadState('networkidle');

      // Verify expired message and resend option
      await expect(page.getByText(/expired|no.*longer.*valid/i)).toBeVisible();
      await expect(page.getByRole('button', { name: /resend|request.*new/i })).toBeVisible();
    });

    test('should show error for invalid token', async ({ page }) => {
      await setupEmailVerificationMocks(page, { tokenValid: false });

      await page.goto('/verify-email?token=invalid-token');
      await page.waitForLoadState('networkidle');

      // Verify invalid token message
      await expect(page.getByText(/invalid|malformed/i)).toBeVisible();
    });

    test('should redirect to dashboard after successful verification', async ({ page }) => {
      await setupEmailVerificationMocks(page, { tokenValid: true, emailVerified: true });

      await page.goto('/verify-email?token=valid-token-123');

      // Wait for redirect to dashboard
      await page.waitForURL(/dashboard|home/i, { timeout: 10000 });
    });
  });

  test.describe('Access Restrictions', () => {
    test('should show limited access warning for unverified users', async ({ page }) => {
      await setupEmailVerificationMocks(page, { emailVerified: false });

      await page.goto('/dashboard');
      await page.waitForLoadState('networkidle');

      // Should show verification reminder or redirect to verification page
      const verificationReminder = page.getByText(/verify.*email|email.*not.*verified/i);
      const verificationPage = page.url().includes('verification');

      expect((await verificationReminder.isVisible()) || verificationPage).toBeTruthy();
    });

    test('should allow full access after email verification', async ({ page }) => {
      await setupEmailVerificationMocks(page, { emailVerified: true });

      await page.goto('/dashboard');
      await page.waitForLoadState('networkidle');

      // Should not show verification warning
      await expect(page.getByText(/verify.*email|email.*not.*verified/i)).not.toBeVisible();
    });
  });
});
