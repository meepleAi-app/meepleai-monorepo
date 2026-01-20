/**
 * Authentication Flow - Visual Documentation
 *
 * Captures visual documentation for all authentication flows:
 * - Registration
 * - Login (with and without 2FA)
 * - OAuth buttons
 * - Password reset
 *
 * @see docs/08-user-flows/user-role/01-authentication.md
 */

import { test } from '../../fixtures/chromatic';
import { AuthHelper, USER_FIXTURES } from '../../pages';
import {
  ScreenshotHelper,
  USER_FLOWS,
  disableAnimations,
  waitForStableState,
  ANNOTATION_COLORS,
} from '../fixtures/screenshot-helpers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Authentication Flow - Visual Documentation', () => {
  let helper: ScreenshotHelper;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    helper = new ScreenshotHelper({
      outputDir: USER_FLOWS.authentication.outputDir,
      flow: USER_FLOWS.authentication.name,
      role: USER_FLOWS.authentication.role,
    });
    authHelper = new AuthHelper(page);
    await disableAnimations(page);
  });

  test('login flow - complete journey', async ({ page }) => {
    // Setup mocks
    await authHelper.mockLoginEndpoint(true, USER_FIXTURES.user);

    // Step 1: Navigate to login page
    await page.goto('/login');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Login Page Initial',
      description: 'Login page showing email and password fields with OAuth options',
      annotations: [
        { selector: 'input[type="email"], [data-testid="email-input"]', label: 'Email Input', color: ANNOTATION_COLORS.primary },
        { selector: 'input[type="password"], [data-testid="password-input"]', label: 'Password Input', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Enter email address',
    });

    // Step 2: Enter email
    const emailInput = page.getByLabel(/email/i);
    if (await emailInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await emailInput.fill('user@meepleai.dev');
      await waitForStableState(page);

      await helper.capture(page, {
        step: 2,
        title: 'Email Entered',
        description: 'User enters their email address in the login form',
        annotations: [
          { selector: 'input[type="email"], [data-testid="email-input"]', label: 'Email Filled', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'Fill email field',
        nextAction: 'Enter password',
      });

      // Step 3: Enter password
      const passwordInput = page.getByLabel(/password/i);
      if (await passwordInput.isVisible({ timeout: 2000 }).catch(() => false)) {
        await passwordInput.fill('SecurePass123!');
        await waitForStableState(page);

        await helper.capture(page, {
          step: 3,
          title: 'Password Entered',
          description: 'User enters their password (masked)',
          annotations: [
            { selector: 'input[type="password"], [data-testid="password-input"]', label: 'Password Filled', color: ANNOTATION_COLORS.success },
          ],
          previousAction: 'Fill password field',
          nextAction: 'Click login button',
        });
      }
    }

    // Step 4: OAuth buttons visible
    const oauthSection = page.locator('[data-testid="oauth-buttons"], .oauth-providers').first();
    if (await oauthSection.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 4,
        title: 'OAuth Options',
        description: 'Alternative login options via Google, GitHub, and Discord',
        annotations: [
          { selector: '[data-testid="oauth-google"], button:has-text("Google")', label: 'Google OAuth', color: ANNOTATION_COLORS.info },
          { selector: '[data-testid="oauth-github"], button:has-text("GitHub")', label: 'GitHub OAuth', color: ANNOTATION_COLORS.info },
          { selector: '[data-testid="oauth-discord"], button:has-text("Discord")', label: 'Discord OAuth', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View OAuth options',
        nextAction: 'Submit login form',
      });
    }

    // Step 5: Submit button
    const submitButton = page.locator('[data-testid="login-submit"], button[type="submit"]').first();
    if (await submitButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 5,
        title: 'Ready to Submit',
        description: 'Login form filled and ready for submission',
        annotations: [
          { selector: '[data-testid="login-submit"], button[type="submit"]', label: 'Login Button', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'Form filled',
        nextAction: 'Click to login',
      });
    }

    helper.setTotalSteps(5);
    console.log(`\n✅ Authentication flow captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('registration flow - new user signup', async ({ page }) => {
    // Setup mock for registration
    await page.route(`${API_BASE}/api/v1/auth/register`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'new-user-id',
          email: 'newuser@example.com',
          displayName: 'newuser',
          role: 'User',
          tier: 'Free',
        }),
      });
    });

    // Step 1: Navigate to register page
    await page.goto('/register');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Registration Page',
      description: 'New user registration form',
      annotations: [
        { selector: 'input[type="email"]', label: 'Email', color: ANNOTATION_COLORS.primary },
        { selector: 'input[type="password"]', label: 'Password', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Fill registration form',
    });

    // Step 2: Fill email
    const emailInput = page.getByLabel(/email/i).first();
    if (await emailInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await emailInput.fill('newuser@example.com');
      await waitForStableState(page);

      await helper.capture(page, {
        step: 2,
        title: 'Email Entered',
        description: 'New user enters email address',
        annotations: [
          { selector: 'input[type="email"]', label: 'Email Filled', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'Fill email',
        nextAction: 'Enter password',
      });

      // Step 3: Fill password
      const passwordInput = page.locator('input[type="password"]').first();
      if (await passwordInput.isVisible({ timeout: 1000 }).catch(() => false)) {
        await passwordInput.fill('SecureNewPass123!');
        await waitForStableState(page);

        await helper.capture(page, {
          step: 3,
          title: 'Password Created',
          description: 'User creates a secure password meeting requirements',
          annotations: [
            { selector: 'input[type="password"]', label: 'Password', color: ANNOTATION_COLORS.success },
          ],
          previousAction: 'Fill password',
          nextAction: 'Confirm password',
        });
      }

      // Step 4: Confirm password (if exists)
      const confirmInput = page.locator('input[type="password"]').nth(1);
      if (await confirmInput.isVisible({ timeout: 1000 }).catch(() => false)) {
        await confirmInput.fill('SecureNewPass123!');
        await waitForStableState(page);

        await helper.capture(page, {
          step: 4,
          title: 'Password Confirmed',
          description: 'User confirms password by re-entering',
          annotations: [
            { selector: 'input[type="password"]:nth-of-type(2)', label: 'Confirm', color: ANNOTATION_COLORS.success },
          ],
          previousAction: 'Confirm password',
          nextAction: 'Submit registration',
        });
      }
    }

    helper.setTotalSteps(4);
    console.log(`\n✅ Registration flow captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('password reset flow', async ({ page }) => {
    // Step 1: Navigate to reset page
    // Note: Route is /reset-password (not /auth/reset-password) per Next.js route groups
    await page.goto('/reset-password');
    await waitForStableState(page);

    const heading = page.getByRole('heading', { name: /reset.*password|forgot.*password/i });
    if (await heading.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 1,
        title: 'Password Reset Request',
        description: 'User requests password reset by entering email',
        annotations: [
          { selector: 'input[type="email"]', label: 'Email', color: ANNOTATION_COLORS.primary },
        ],
        nextAction: 'Enter email for reset',
      });

      // Step 2: Fill email
      const emailInput = page.locator('input[type="email"]').first();
      if (await emailInput.isVisible({ timeout: 1000 }).catch(() => false)) {
        await emailInput.fill('user@example.com');
        await waitForStableState(page);

        await helper.capture(page, {
          step: 2,
          title: 'Email Entered for Reset',
          description: 'User enters email to receive password reset link',
          annotations: [
            { selector: 'input[type="email"]', label: 'Reset Email', color: ANNOTATION_COLORS.success },
            { selector: 'button[type="submit"]', label: 'Send Reset', color: ANNOTATION_COLORS.primary },
          ],
          previousAction: 'Fill email',
          nextAction: 'Submit reset request',
        });
      }
    } else {
      console.log('⚠️ Password reset page not found, skipping');
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Password reset flow captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test.skip('2FA verification flow', async ({ page }) => {
    // SKIP: Page /auth/2fa-verify not yet implemented
    // TODO: Create 2FA verification page and enable this test
    await page.goto('/auth/2fa-verify');
    await waitForStableState(page);

    const twoFAHeading = page.getByRole('heading', { name: /verify|two-factor|enter.*code|2fa/i });
    if (await twoFAHeading.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 1,
        title: '2FA Verification',
        description: 'User enters TOTP code from authenticator app',
        annotations: [
          { selector: 'input[type="text"], input[inputmode="numeric"]', label: 'TOTP Code', color: ANNOTATION_COLORS.primary },
        ],
        nextAction: 'Enter 6-digit code',
      });

      // Fill TOTP code
      const codeInput = page.locator('input[type="text"], input[inputmode="numeric"]').first();
      if (await codeInput.isVisible({ timeout: 1000 }).catch(() => false)) {
        await codeInput.fill('123456');
        await waitForStableState(page);

        await helper.capture(page, {
          step: 2,
          title: 'Code Entered',
          description: 'User enters 6-digit verification code',
          annotations: [
            { selector: 'input[type="text"], input[inputmode="numeric"]', label: 'Code Filled', color: ANNOTATION_COLORS.success },
            { selector: 'button[type="submit"]', label: 'Verify', color: ANNOTATION_COLORS.primary },
          ],
          previousAction: 'Enter TOTP code',
          nextAction: 'Verify code',
        });
      }
    } else {
      console.log('⚠️ 2FA verification page not found, skipping');
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ 2FA flow captured: ${helper.getCapturedSteps().length} screenshots`);
  });
});
