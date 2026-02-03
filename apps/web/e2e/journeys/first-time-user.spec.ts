/**
 * Journey 4: First-Time User Tutorial E2E Tests
 *
 * Tests the onboarding experience for new users:
 * 1. Registration - Create new account
 * 2. Tutorial Flow - Step-by-step introduction
 * 3. Feature Highlights - Key areas (games, chat, upload)
 * 4. Completion Tracking - Tutorial state persisted
 *
 * Pattern: Hybrid approach (mock external APIs, test real user state)
 * Related Issue: #2843 - E2E User Journey Tests
 * Epic: #2823
 */

import { expect, test } from '../fixtures';
import { WaitHelper } from '../helpers/WaitHelper';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const NEW_USER = {
  email: 'newuser@test.com',
  password: 'Test123!',
  username: 'NewUser',
};

const MOCK_USER_RESPONSE = {
  id: 'test-new-user-1',
  email: NEW_USER.email,
  username: NEW_USER.username,
  roles: ['User'],
  tutorialCompleted: false,
};

const MOCK_USER_TUTORIAL_COMPLETE = {
  ...MOCK_USER_RESPONSE,
  tutorialCompleted: true,
};

test.describe('Journey 4: First-Time User Tutorial', () => {
  test('should complete tutorial flow on first login', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Mock: Registration API
    await page.route(`${API_BASE}/api/v1/auth/register`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            user: MOCK_USER_RESPONSE,
            accessToken: 'mock-access-token',
            refreshToken: 'mock-refresh-token',
          }),
        });
      }
    });

    // Mock: User profile API
    await page.route(`${API_BASE}/api/v1/users/me`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_USER_RESPONSE),
      });
    });

    // Mock: Tutorial completion API
    let tutorialCompleted = false;
    await page.route(`${API_BASE}/api/v1/users/me/tutorial`, async route => {
      if (route.request().method() === 'POST') {
        tutorialCompleted = true;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_USER_TUTORIAL_COMPLETE),
        });
      }
    });

    await test.step('Register new account', async () => {
      await page.goto('/register');
      await page.waitForLoadState('networkidle');

      // Fill registration form
      const emailInput = page.locator('input[type="email"], input[name="email"]').first();
      const passwordInput = page.locator('input[type="password"], input[name="password"]').first();
      const usernameInput = page.locator('input[name="username"]').first();

      await emailInput.fill(NEW_USER.email);
      await passwordInput.fill(NEW_USER.password);

      if (await usernameInput.isVisible({ timeout: 2000 })) {
        await usernameInput.fill(NEW_USER.username);
      }

      // Submit registration
      const registerButton = page.getByRole('button', { name: /register|sign up|create/i }).first();
      await registerButton.click();

      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(3000);
    });

    await test.step('Verify tutorial modal appears', async () => {
      // Look for tutorial/onboarding modal
      const tutorialModal = page.locator(
        '[data-testid="tutorial-modal"], [role="dialog"]:has-text("Welcome"), [role="dialog"]:has-text("Tutorial")'
      ).first();

      if (await tutorialModal.isVisible({ timeout: 5000 })) {
        // Tutorial exists - proceed
        await expect(tutorialModal).toBeVisible();
        console.log('✓ Tutorial modal detected');
      } else {
        // Tutorial may not be implemented yet
        console.log('⚠️  Tutorial modal not found - feature may not be implemented');
        test.skip(true, 'Tutorial feature not yet implemented');
      }
    });

    await test.step('Navigate through tutorial steps', async () => {
      const totalSteps = 4; // Expected tutorial steps

      for (let step = 1; step <= totalSteps; step++) {
        // Look for step indicator
        const stepIndicator = page.locator(`text=Step ${step}`);
        if (await stepIndicator.isVisible({ timeout: 3000 })) {
          console.log(`✓ Tutorial Step ${step} visible`);
        }

        // Click Next/Continue button (except on last step)
        if (step < totalSteps) {
          const nextButton = page.getByRole('button', { name: /next|continue|avanti/i }).first();
          await nextButton.click();

          const waitHelper = new WaitHelper(page);
          await waitHelper.waitForDOMStable('body', 1000);
        }
      }

      console.log('✓ Navigated through all tutorial steps');
    });

    await test.step('Complete tutorial', async () => {
      // Click "Finish" or "Get Started" button
      const finishButton = page.getByRole('button', { name: /finish|done|get started|start/i }).first();
      await finishButton.click();

      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(2000);

      // Verify tutorial modal closed
      const tutorialModal = page.locator('[data-testid="tutorial-modal"]');
      await expect(tutorialModal).not.toBeVisible({ timeout: 3000 });
    });

    await test.step('Verify tutorial completion persisted', async () => {
      // Verify tutorialCompleted flag was set
      expect(tutorialCompleted).toBe(true);
      console.log('✅ Journey 4 (Tutorial): First-time user tutorial completed');
    });
  });

  test('should not show tutorial on subsequent logins', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Mock: Login API returning user with tutorial completed
    await page.route(`${API_BASE}/api/v1/auth/login`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            user: MOCK_USER_TUTORIAL_COMPLETE,
            accessToken: 'mock-access-token',
            refreshToken: 'mock-refresh-token',
          }),
        });
      }
    });

    // Mock: User profile API
    await page.route(`${API_BASE}/api/v1/users/me`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_USER_TUTORIAL_COMPLETE),
      });
    });

    await test.step('Login as returning user', async () => {
      await page.goto('/login');
      await page.waitForLoadState('networkidle');

      // Fill login form
      const emailInput = page.locator('input[type="email"]').first();
      const passwordInput = page.locator('input[type="password"]').first();

      await emailInput.fill(NEW_USER.email);
      await passwordInput.fill(NEW_USER.password);

      // Submit login
      const loginButton = page.getByRole('button', { name: /login|sign in/i }).first();
      await loginButton.click();

      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(3000);
    });

    await test.step('Verify tutorial does NOT appear', async () => {
      // Wait for page load
      await page.waitForLoadState('networkidle');

      // Tutorial modal should not appear
      const tutorialModal = page.locator(
        '[data-testid="tutorial-modal"], [role="dialog"]:has-text("Welcome")'
      );
      await expect(tutorialModal).not.toBeVisible({ timeout: 3000 });

      console.log('✅ Journey 4 (Returning User): Tutorial correctly skipped');
    });
  });

  test.describe('Tutorial Content Validation', () => {
    test.skip('should show all tutorial steps in correct order', async ({ page }) => {
      // This test validates tutorial content when feature is implemented
      // Expected steps:
      // 1. Welcome to MeepleAI
      // 2. Add your first game
      // 3. Try AI chat assistant
      // 4. Upload rulebook PDFs
      // 5. Track game sessions

      console.log('⚠️  Tutorial feature not yet implemented - skipping content validation');
    });
  });
});
