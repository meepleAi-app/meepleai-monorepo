/**
 * E2E Email Verification Flow Tests (Issue #3076)
 *
 * Tests the complete email verification user journey:
 * 1. Registration → Redirect to verification-pending
 * 2. Verification pending page states (resend, cooldown, error)
 * 3. Verify email page (token validation)
 * 4. Success page with auto-redirect
 * 5. Error page scenarios (expired, invalid, already_verified)
 *
 * Real APIs Used:
 * - POST /api/v1/auth/register (user registration)
 * - POST /api/v1/auth/email/verify (token verification)
 * - POST /api/v1/auth/email/resend (resend verification)
 *
 * @see Issue #3076 - Email Verification Frontend Flow
 */

import { test, expect } from './fixtures';
import { AuthHelper, USER_FIXTURES } from './pages';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Email Verification Flow', () => {
  test.describe('Registration → Verification Pending', () => {
    test('should redirect to verification-pending page after successful registration', async ({
      page,
    }) => {
      const authHelper = new AuthHelper(page);

      // Start unauthenticated
      await authHelper.mockUnauthenticatedSession();

      // Mock successful registration that requires email verification
      await page.route(`${apiBase}/api/v1/auth/register`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            requiresEmailVerification: true,
            email: 'newuser@test.com',
            message: 'Registration successful. Please verify your email.',
          }),
        });
      });

      // Navigate to registration page
      await page.goto('/register');
      await expect(page.locator('[data-testid="auth-modal"]')).toBeVisible({ timeout: 10000 });

      // Fill registration form
      await page.locator('[data-testid="register-email"]').fill('newuser@test.com');
      await page.locator('[data-testid="register-password"]').fill('TestPassword123!');
      await page.locator('[data-testid="register-confirm-password"]').fill('TestPassword123!');

      // Submit registration
      await page.locator('[data-testid="register-submit"]').click();

      // Should redirect to verification-pending
      await page.waitForURL('**/verification-pending**', { timeout: 10000 });
      expect(page.url()).toContain('verification-pending');
    });

    test('should display masked email on verification-pending page', async ({ page }) => {
      // Navigate directly with email parameter
      await page.goto('/verification-pending?email=newuser@test.com');

      // Wait for page to load
      await page.waitForLoadState('networkidle');

      // Should show masked email (n***r@test.com)
      await expect(page.locator('text=/n\\*\\*\\*r@test\\.com/')).toBeVisible({ timeout: 5000 });
    });
  });

  test.describe('Verification Pending Page', () => {
    test('should show resend button and allow clicking', async ({ page }) => {
      // Mock resend endpoint
      await page.route(`${apiBase}/api/v1/auth/email/resend`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ok: true,
            message: 'Verification email sent',
            cooldownSeconds: 60,
          }),
        });
      });

      await page.goto('/verification-pending?email=test@example.com');
      await page.waitForLoadState('networkidle');

      // Find and click resend button
      const resendButton = page.locator('[data-testid="resend-verification-button"]');
      await expect(resendButton).toBeVisible();
      await expect(resendButton).toBeEnabled();

      await resendButton.click();

      // Should show loading or cooldown state
      await expect(resendButton).toBeDisabled({ timeout: 5000 });
    });

    test('should display cooldown timer after resend', async ({ page }) => {
      // Mock resend endpoint with cooldown
      await page.route(`${apiBase}/api/v1/auth/email/resend`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ok: true,
            message: 'Verification email sent',
            cooldownSeconds: 60,
          }),
        });
      });

      await page.goto('/verification-pending?email=test@example.com');
      await page.waitForLoadState('networkidle');

      // Click resend
      await page.locator('[data-testid="resend-verification-button"]').click();

      // Wait for cooldown to appear
      await expect(page.locator('text=/Resend in \\d+s/')).toBeVisible({ timeout: 5000 });
    });

    test('should show error message when resend fails', async ({ page }) => {
      // Mock resend endpoint with error
      await page.route(`${apiBase}/api/v1/auth/email/resend`, async route => {
        await route.fulfill({
          status: 429,
          contentType: 'application/json',
          body: JSON.stringify({
            ok: false,
            error: 'rate_limited',
            message: 'Too many requests. Please wait before trying again.',
            cooldownSeconds: 120,
          }),
        });
      });

      await page.goto('/verification-pending?email=test@example.com');
      await page.waitForLoadState('networkidle');

      // Click resend
      await page.locator('[data-testid="resend-verification-button"]').click();

      // Should show error message
      await expect(page.locator('[role="alert"]')).toBeVisible({ timeout: 5000 });
    });
  });

  test.describe('Verify Email Page', () => {
    test('should successfully verify email with valid token', async ({ page }) => {
      // Mock verify endpoint - success
      await page.route(`${apiBase}/api/v1/auth/email/verify`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ok: true,
            message: 'Email verified successfully',
          }),
        });
      });

      // Navigate to verify page with token
      await page.goto('/verify-email?token=valid-token-123');
      await page.waitForLoadState('networkidle');

      // Should show success state or redirect
      await expect(page.locator('text=/verified|success/i').first()).toBeVisible({ timeout: 10000 });
    });

    test('should show expired error for expired token', async ({ page }) => {
      // Mock verify endpoint - expired
      await page.route(`${apiBase}/api/v1/auth/email/verify`, async route => {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            ok: false,
            error: 'expired',
            message: 'This verification link has expired.',
          }),
        });
      });

      await page.goto('/verify-email?token=expired-token');
      await page.waitForLoadState('networkidle');

      // Should show expired error
      await expect(page.locator('text=/expired|link expired/i').first()).toBeVisible({ timeout: 5000 });

      // Should show resend button
      await expect(page.locator('[data-testid="resend-verification-button"]')).toBeVisible();
    });

    test('should show invalid error for malformed token', async ({ page }) => {
      // Mock verify endpoint - invalid
      await page.route(`${apiBase}/api/v1/auth/email/verify`, async route => {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            ok: false,
            error: 'invalid',
            message: 'This verification link is invalid.',
          }),
        });
      });

      await page.goto('/verify-email?token=invalid-token');
      await page.waitForLoadState('networkidle');

      // Should show invalid error
      await expect(page.locator('text=/invalid/i').first()).toBeVisible({ timeout: 5000 });

      // Should show retry button
      await expect(page.locator('[data-testid="retry-button"]')).toBeVisible();
    });

    test('should show already verified message', async ({ page }) => {
      // Mock verify endpoint - already verified
      await page.route(`${apiBase}/api/v1/auth/email/verify`, async route => {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            ok: false,
            error: 'already_verified',
            message: 'This email has already been verified.',
          }),
        });
      });

      await page.goto('/verify-email?token=already-verified-token');
      await page.waitForLoadState('networkidle');

      // Should show already verified message
      await expect(page.locator('text=/already verified/i').first()).toBeVisible({ timeout: 5000 });

      // Should show go to login button
      await expect(page.locator('[data-testid="go-to-login-button"]')).toBeVisible();
    });

    test('should handle missing token gracefully', async ({ page }) => {
      await page.goto('/verify-email');
      await page.waitForLoadState('networkidle');

      // Should show error or redirect
      await expect(page.locator('text=/invalid|missing|error/i').first()).toBeVisible({
        timeout: 5000,
      });
    });
  });

  test.describe('Verification Success Page', () => {
    test('should display success message and countdown', async ({ page }) => {
      await page.goto('/verification-success');
      await page.waitForLoadState('networkidle');

      // Should show success title
      await expect(page.locator('text=/verified|success/i').first()).toBeVisible({ timeout: 5000 });

      // Should show countdown
      await expect(page.locator('text=/Redirecting in \\d+ seconds/i')).toBeVisible({
        timeout: 5000,
      });

      // Should show continue button
      await expect(page.locator('[data-testid="continue-to-dashboard-button"]')).toBeVisible();
    });

    test('should redirect to dashboard on button click', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      // Mock authenticated session for dashboard access
      await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

      await page.goto('/verification-success');
      await page.waitForLoadState('networkidle');

      // Click continue button
      await page.locator('[data-testid="continue-to-dashboard-button"]').click();

      // Should navigate to dashboard
      await page.waitForURL('**/dashboard', { timeout: 10000 });
    });

    test('should display verified email when provided', async ({ page }) => {
      await page.goto('/verification-success?email=verified@example.com');
      await page.waitForLoadState('networkidle');

      // Should display the email
      await expect(page.locator('text=verified@example.com')).toBeVisible({ timeout: 5000 });
    });
  });

  test.describe('Accessibility', () => {
    test('verification-pending page should have proper ARIA attributes', async ({ page }) => {
      await page.goto('/verification-pending?email=test@example.com');
      await page.waitForLoadState('networkidle');

      // Should have status role
      await expect(page.locator('[role="status"]')).toBeVisible();

      // Should have aria-live
      await expect(page.locator('[aria-live="polite"]')).toBeVisible();
    });

    test('verification error page should have alert role', async ({ page }) => {
      // Mock verify endpoint - error
      await page.route(`${apiBase}/api/v1/auth/email/verify`, async route => {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            ok: false,
            error: 'expired',
            message: 'Token expired',
          }),
        });
      });

      await page.goto('/verify-email?token=expired');
      await page.waitForLoadState('networkidle');

      // Should have alert role for errors
      await expect(page.locator('[role="alert"]')).toBeVisible({ timeout: 5000 });

      // Should have assertive aria-live
      await expect(page.locator('[aria-live="assertive"]')).toBeVisible();
    });

    test('verification success page should have status role', async ({ page }) => {
      await page.goto('/verification-success');
      await page.waitForLoadState('networkidle');

      // Should have status role
      await expect(page.locator('[role="status"]')).toBeVisible();
    });
  });

  test.describe('Rate Limiting', () => {
    test('should handle rate limit error from resend endpoint', async ({ page }) => {
      // Mock rate limit error
      await page.route(`${apiBase}/api/v1/auth/email/resend`, async route => {
        await route.fulfill({
          status: 429,
          contentType: 'application/json',
          body: JSON.stringify({
            ok: false,
            error: 'rate_limited',
            message: 'Too many requests',
            cooldownSeconds: 60,
          }),
        });
      });

      await page.goto('/verification-pending?email=test@example.com');
      await page.waitForLoadState('networkidle');

      // Click resend
      await page.locator('[data-testid="resend-verification-button"]').click();

      // Should show rate limit message
      await expect(page.locator('text=/too many|rate limit|wait/i').first()).toBeVisible({
        timeout: 5000,
      });
    });

    test('should handle rate limit error from verify endpoint', async ({ page }) => {
      // Mock rate limit error
      await page.route(`${apiBase}/api/v1/auth/email/verify`, async route => {
        await route.fulfill({
          status: 429,
          contentType: 'application/json',
          body: JSON.stringify({
            ok: false,
            error: 'rate_limited',
            message: 'Too many verification attempts',
            cooldownSeconds: 120,
          }),
        });
      });

      await page.goto('/verify-email?token=some-token');
      await page.waitForLoadState('networkidle');

      // Should show rate limit error
      await expect(page.locator('text=/too many|requests/i').first()).toBeVisible({ timeout: 5000 });

      // Should show cooldown message
      await expect(page.locator('text=/seconds/i')).toBeVisible();
    });
  });

  test.describe('Complete User Journey', () => {
    test('should complete full registration → verify → dashboard flow', async ({ page }) => {
      const authHelper = new AuthHelper(page);
      const testEmail = `e2e-${Date.now()}@test.com`;

      // Step 1: Start unauthenticated
      await authHelper.mockUnauthenticatedSession();

      // Step 2: Mock registration that requires verification
      await page.route(`${apiBase}/api/v1/auth/register`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            requiresEmailVerification: true,
            email: testEmail,
            message: 'Please verify your email',
          }),
        });
      });

      // Step 3: Register
      await page.goto('/register');
      await expect(page.locator('[data-testid="auth-modal"]')).toBeVisible({ timeout: 10000 });

      await page.locator('[data-testid="register-email"]').fill(testEmail);
      await page.locator('[data-testid="register-password"]').fill('TestPassword123!');
      await page.locator('[data-testid="register-confirm-password"]').fill('TestPassword123!');
      await page.locator('[data-testid="register-submit"]').click();

      // Step 4: Should be on verification-pending
      await page.waitForURL('**/verification-pending**', { timeout: 10000 });

      // Step 5: Mock successful verification
      await page.route(`${apiBase}/api/v1/auth/email/verify`, async route => {
        // After verification, set authenticated session
        await authHelper.mockAuthenticatedSession({
          id: 'verified-user-1',
          email: testEmail,
          displayName: 'Verified User',
          role: 'User',
        });

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ok: true,
            message: 'Email verified successfully',
          }),
        });
      });

      // Step 6: Simulate clicking verification link
      await page.goto('/verify-email?token=valid-verification-token');
      await page.waitForLoadState('networkidle');

      // Step 7: Should show success
      await expect(page.locator('text=/verified|success/i').first()).toBeVisible({ timeout: 10000 });

      // Step 8: Click continue to dashboard
      await page.locator('[data-testid="continue-to-dashboard-button"]').click();

      // Step 9: Should be on dashboard
      await page.waitForURL('**/dashboard', { timeout: 10000 });
    });
  });
});
